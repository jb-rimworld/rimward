using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard
{
    // Central per-map hospital state: the patients on this map, pending offers and (optionally) a map-only policy override.
    // The policies themselves live per save in HospitalGameComponent, so a new map (gravship landing) keeps them.
    public class HospitalMapComponent : MapComponent
    {
        public Dictionary<Pawn, PatientData> Patients;
        private List<Pawn> _patientsKeysWorkingList;
        private List<PatientData> _patientsValuesWorkingList;

        /// <summary>Map-only policy; null = this map uses the save's policy.</summary>
        private HospitalPolicy ownPolicy;

        public List<PatientOffer> pendingOffers = new List<PatientOffer>();
        private int nextOfferId = 1;
        public int PendingOfferCount => this.pendingOffers.Count;

        // pre-2.0 per-map fields, read only while loading an older save and then migrated
        private HospitalPolicy legacyPolicy;

        private static readonly Dictionary<Map, HospitalMapComponent> cache = new Dictionary<Map, HospitalMapComponent>();

        public HospitalMapComponent(Map map) : base(map)
        {
            this.Patients = new Dictionary<Pawn, PatientData>();
            if (map != null) cache[map] = this;
        }

        /// <summary>The hospital of a map without the linear component search (hot paths).</summary>
        public static HospitalMapComponent For(Map map)
        {
            if (map == null) return null;
            if (cache.TryGetValue(map, out HospitalMapComponent c) && c != null && c.map == map) return c;
            c = map.GetComponent<HospitalMapComponent>();
            if (c != null) cache[map] = c;
            return c;
        }

        public static void ClearCache()
        {
            cache.Clear();
            Threats.ClearCache();
        }

        // ---------------------------------------------------------------- policy

        /// <summary>The policy in force on this map: its own override, else the save's policy.</summary>
        public HospitalPolicy Policy => this.ownPolicy ?? HospitalGameComponent.Get?.Policy ?? (this.ownPolicy = HospitalPolicy.NewDefault(false));

        public bool HasOwnPolicy => this.ownPolicy != null;

        /// <summary>Give this map its own copy of the policy (true) or go back to the save's policy (false).</summary>
        public void SetOwnPolicy(bool own)
        {
            if (own && this.ownPolicy == null) this.ownPolicy = (HospitalGameComponent.Get?.Policy ?? HospitalPolicy.NewDefault(false)).Copy();
            else if (!own) this.ownPolicy = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) this.CleanForSave();
            if (this.Patients == null) this.Patients = new Dictionary<Pawn, PatientData>();
            Scribe_Collections.Look(ref this.Patients, "patients", LookMode.Reference, LookMode.Deep, ref this._patientsKeysWorkingList, ref this._patientsValuesWorkingList);
            Scribe_Deep.Look(ref this.ownPolicy, "ownPolicy");
            Scribe_Collections.Look(ref this.pendingOffers, "pendingOffers", LookMode.Deep);
            Scribe_Values.Look(ref this.nextOfferId, "nextOfferId", 1);

            if (Scribe.mode == LoadSaveMode.LoadingVars) this.ReadLegacyPolicy();
            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs && this.legacyPolicy != null)
                Scribe_References.Look(ref this.legacyPolicy.foodPolicy, "PatientFoodPolicy");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.Patients == null) this.Patients = new Dictionary<Pawn, PatientData>();
                List<Pawn> bad = this.Patients.Where(kv => kv.Key == null || kv.Value == null).Select(kv => kv.Key).ToList();
                foreach (Pawn p in bad) if (p != null) this.Patients.Remove(p);
                if (this.pendingOffers == null) this.pendingOffers = new List<PatientOffer>();
                this.pendingOffers.RemoveAll(o => o == null || o.pawn == null || o.pawn.Discarded);
                cache[this.map] = this;
            }
        }

        /// <summary>Saves written before 2.0 kept the policies per map. Read them once so behaviour does not change.</summary>
        private void ReadLegacyPolicy()
        {
            System.Xml.XmlNode node = Scribe.loader?.curXmlParent;
            if (node == null || node["openingHours"] == null && node["openForBusiness"] == null) return;
            HospitalPolicy p = new HospitalPolicy();
            Scribe_Values.Look(ref p.open, "openForBusiness", false);
            Scribe_Collections.Look(ref p.openingHours, "openingHours", LookMode.Undefined);
            Scribe_Collections.Look(ref p.refusedOperations, "refusedOperations", LookMode.Def);
            Scribe_References.Look(ref p.foodPolicy, "PatientFoodPolicy");
            Scribe_Values.Look(ref p.massCasualties, "massCasualties", false);
            Scribe_Values.Look(ref p.acceptSurgery, "acceptSurgery", true);
            Scribe_Values.Look(ref p.acceptDanger, "AcceptDanger", false);
            this.legacyPolicy = p;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cache[this.map] = this;
            if (this.legacyPolicy != null)
            {
                // Resolve the cross-reference (food policy) read during LoadingVars, then migrate:
                // the first map's values become the save policy, other maps that differ keep their own.
                this.legacyPolicy.Repair();
                HospitalGameComponent gc = HospitalGameComponent.Get;
                if (gc != null)
                {
                    if (!gc.policyMigrated)
                    {
                        gc.Policy = this.legacyPolicy;
                        gc.policyMigrated = true;
                    }
                    else if (!gc.Policy.SameAs(this.legacyPolicy))
                    {
                        this.ownPolicy = this.legacyPolicy;
                    }
                }
                this.legacyPolicy = null;
            }
            foreach (PatientData d in this.Patients.Values) d?.EnsureLines(true);
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            cache.Remove(this.map);
        }

        /// <summary>Drops entries that would come back as null keys ("Null key while loading dictionary"): gone, discarded, dead or off this map.</summary>
        private void CleanForSave()
        {
            if (this.Patients == null) return;
            List<Pawn> drop = new List<Pawn>();
            foreach (KeyValuePair<Pawn, PatientData> kv in this.Patients)
            {
                Pawn p = kv.Key;
                // a pawn nobody holds (not spawned, not in a pod, transporter or caravan, not a world pawn) is not written to the save:
                // its entry would come back as a null key ("Null key while loading dictionary")
                bool unsaved = p != null && !p.Spawned && p.ParentHolder == null && !Find.WorldPawns.Contains(p);
                if (kv.Value == null || Caretaker.IsGone(p) || unsaved) drop.Add(p);
            }
            foreach (Pawn p in drop)
            {
                if (p != null) this.Patients.Remove(p);
                Caretaker.Log(this, Caretaker.Fix.StaleEntry, p);
            }
            this.pendingOffers.RemoveAll(o => o == null || o.pawn == null || o.pawn.Discarded);
        }

        // ---------------------------------------------------------------- tick

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager.TicksGame % 250 != 0) return;
            int now = Find.TickManager.TicksGame;

            // Expire stale offers (auto-decline, no penalty) + scan for injured allies on the map.
            for (int i = this.pendingOffers.Count - 1; i >= 0; i--)
            {
                PatientOffer o = this.pendingOffers[i];
                if (now >= o.expiresTick || o.pawn == null || o.pawn.Dead || o.pawn.Discarded || (o.kind == OfferKind.Ambient && !o.pawn.Spawned))
                {
                    this.pendingOffers.RemoveAt(i);
                    DiscardOffer(o);
                }
            }
            if (HospitalMod.Settings.AmbientContractsEnabled && now % 1000 == 0) AmbientScan();

            Caretaker.Run(this);

            foreach (KeyValuePair<Pawn, PatientData> kv in this.Patients.ToList())
            {
                Pawn pawn = kv.Key;
                PatientData data = kv.Value;
                if (data == null || pawn == null || !pawn.Spawned) continue;

                // Auto-rescue: a downed patient on the floor is carried to a hospital bed by a free doctor, exactly like a downed colonist.
                // Also for dismissed patients who collapsed before they could leave.
                if (pawn.Downed && !pawn.InBed()) TryAutoRescue(pawn);
                if (data.Dismissed) continue;

                // Track the best room the patient actually rested in (used at discharge for pay/goodwill,
                // since by discharge they may be up and walking, which would read as a hallway).
                if (pawn.InBed())
                {
                    RoomTier t = RoomTierUtility.Classify(pawn.CurrentBed(), pawn);
                    if ((int)t > (int)data.BestRoomTier) data.BestRoomTier = t;
                }

                // Scheduled surgery retry: OnSurgeryBotched defers recreation until AFTER vanilla deletes the
                // failed bill, otherwise the new bill would collide with / be cleaned up alongside the old one.
                if (data.RetryAfterTick >= 0 && now >= data.RetryAfterTick)
                {
                    data.RetryAfterTick = -1;
                    if (CureAchieved(pawn, data)) { data.HasPendingSurgeryBill = false; }
                    else if (data.CureRecipe != null)
                    {
                        if (!pawn.health.surgeryBills.Bills.Any(b => b is Bill_Medical bm && bm.recipe == data.CureRecipe))
                        {
                            BodyPartRecord part = ResolveTargetPart(pawn, data.CureRecipe);
                            if (part != null || !data.CureRecipe.targetsBodyPart)
                                HealthCardUtility.CreateSurgeryBill(pawn, data.CureRecipe, part, null, false);
                        }
                        data.HasPendingSurgeryBill = true;
                    }
                    continue;
                }

                if (!data.HasPendingSurgeryBill) continue;

                // Heal-by-any-means: a patient cured WITHOUT our surgery (psycast, another mod, natural) must not
                // stay forever. Cure target gone -> delete its bill properly + clear pending.
                if (CureAchieved(pawn, data))
                {
                    foreach (Bill b in pawn.health.surgeryBills.Bills.ToList())
                    {
                        if (b is Bill_Medical bm && bm.recipe == data.CureRecipe) pawn.health.surgeryBills.Delete(b);
                    }
                    data.HasPendingSurgeryBill = false;
                }
                else if (pawn.health.surgeryBills.Count == 0
                    && !pawn.health.HasHediffsNeedingTend(false)
                    && !HealthAIUtility.ShouldSeekMedicalRest(pawn))
                {
                    data.HasPendingSurgeryBill = false;
                }
            }
        }

        public static bool CureAchieved(Pawn pawn, PatientData data)
        {
            if (data.CriticalCase) return CriticalCase.Done(pawn);
            RecipeDef r = data.CureRecipe;
            if (r == null) return false;
            if (r.removesHediff != null) return pawn.health.hediffSet.GetFirstHediffOfDef(r.removesHediff) == null;
            if (r.addsHediff != null) return pawn.health.hediffSet.GetFirstHediffOfDef(r.addsHediff) != null;
            return false;
        }

        // Carry a downed patient to a hospital bed using a free, capable doctor (mirrors colonist rescue).
        private void TryAutoRescue(Pawn patient)
        {
            // already being rescued or tended?
            foreach (Pawn c in this.map.mapPawns.FreeColonistsSpawned)
            {
                if (c.CurJob != null && c.CurJob.targetA.Thing == patient
                    && (c.CurJobDef == JobDefOf.Rescue || c.CurJobDef == JobDefOf.TendPatient))
                {
                    return;
                }
            }
            // pick the nearest idle, manipulation-capable doctor (never interrupt important work)
            Pawn doctor = null;
            float best = float.MaxValue;
            foreach (Pawn c in this.map.mapPawns.FreeColonistsSpawned)
            {
                if (c == patient || c.Downed || c.Dead) continue;
                if (c.workSettings == null || !c.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)) continue;
                if (!c.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) continue;
                bool free = (c.mindState?.IsIdle ?? false) || c.CurJobDef == JobDefOf.Wait_Wander || c.CurJobDef == JobDefOf.GotoWander || c.CurJob == null;
                if (!free) continue;
                if (!c.CanReserveAndReach(patient, PathEndMode.Touch, Danger.Deadly)) continue;
                float d = c.Position.DistanceToSquared(patient.Position);
                if (d < best) { best = d; doctor = c; }
            }
            if (doctor == null) return;
            Building_Bed bed = RestUtility.FindBedFor(patient, doctor, false, false, GuestStatus.Guest);
            if (bed == null || !doctor.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Deadly)) return;
            Job job = JobMaker.MakeJob(JobDefOf.Rescue, patient, bed);
            job.count = 1;
            doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public bool IsOpen()
        {
            return this.Policy.IsOpenAtHour(GenLocalDate.HourOfDay(this.map));
        }

        public bool AcceptSurgery => this.Policy.acceptSurgery;
        public bool AcceptDanger => this.Policy.acceptDanger;
        public bool MassCasualties => this.Policy.massCasualties && HospitalMod.Settings.MassCasualtiesEnabled;
        public FoodPolicy PatientFoodPolicy => this.Policy.FoodPolicy;

        public void PatientArrived(Pawn pawn, PatientData data)
        {
            data.AdmitHealth = pawn.health.summaryHealth.SummaryHealthPercent;
            data.EnsureLines(false);
            this.Patients[pawn] = data;
            this.ApplyPolicyTo(pawn);
            MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();
        }

        /// <summary>Food policy and medicine level of the hospital policy, applied to one patient.</summary>
        public void ApplyPolicyTo(Pawn pawn)
        {
            if (pawn == null) return;
            HospitalPolicy pol = this.Policy;
            FoodPolicy fp = pol.FoodPolicy;
            if (fp != null)
            {
                if (pawn.foodRestriction == null) pawn.foodRestriction = new Pawn_FoodRestrictionTracker(pawn);
                pawn.foodRestriction.CurrentFoodPolicy = fp;
            }
            if (pawn.playerSettings != null) pawn.playerSettings.medCare = pol.medCare;
        }

        /// <summary>Re-applies the policy to every current patient (after the policy dialog closes).</summary>
        public void ApplyPolicyToAll()
        {
            foreach (Pawn p in this.ActivePatientsList().ToList()) this.ApplyPolicyTo(p);
        }

        // ---------------------------------------------------------------- triage / intake offers

        // Deliver a (held) patient via drop pod and admit them.
        public void DeliverPatient(Pawn pawn, PatientData data, Faction faction)
        {
            Building landing = this.map.listerBuildings.AllBuildingsColonistOfDef(ThingDef.Named("RimWard_PatientLandingSpot")).RandomElementWithFallback();
            IntVec3 cell = DropCellFinder.TryFindSafeLandingSpotCloseToColony(this.map, IntVec2.Two, null, 2);
            if (landing != null) cell = landing.Position;
            if (pawn.IsWorldPawn()) Find.WorldPawns.RemovePawn(pawn);
            ActiveTransporterInfo info = new ActiveTransporterInfo();
            if (!info.innerContainer.TryAdd(pawn, true))
            {
                Log.Warning("[RimWard] Could not load patient into drop pod; aborting delivery.");
                IncidentHelper.DiscardGenerated(pawn);
                return;
            }
            info.openDelay = 60;
            info.leaveSlag = false;
            info.despawnPodBeforeSpawningThing = true;
            info.spawnWipeMode = WipeMode.Vanish;
            DropPodUtility.MakeDropPodAt(cell, this.map, info);
            this.PatientArrived(pawn, data);
            pawn.jobs.StopAll();
            LordMaker.MakeNewLord(faction, new LordJob_VisitColonyAsPatient(faction), this.map, new List<Pawn> { pawn });
        }

        // Hold a generated patient as a world pawn and present an accept/decline letter.
        public void CreateDropPodOffer(Pawn pawn, PatientData data, Faction faction)
        {
            if (!pawn.IsWorldPawn()) Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            data.EnsureLines(false);
            PatientOffer offer = new PatientOffer
            {
                id = this.nextOfferId++,
                pawn = pawn,
                data = data,
                faction = faction,
                kind = OfferKind.DropPod,
                expiresTick = Find.TickManager.TicksGame + (int)(HospitalMod.Settings.TriageDecisionHours * 2500f)
            };
            this.pendingOffers.Add(offer);
            SendOfferLetter(offer);
        }

        private void SendOfferLetter(PatientOffer offer)
        {
            Invoice est = Invoice.Build(offer.pawn, offer.data, DischargeReason.Estimate);
            string faction = offer.faction != null ? offer.faction.Name : "RimWard.NoFaction".Translate().ToString();
            TaggedString text = "RimWard.Offer.Text".Translate(offer.pawn.LabelShortCap, faction, offer.data.Diagnosis ?? "?", est.TotalSilver);
            string uses = OfferStockNote(offer.data);
            if (!uses.NullOrEmpty()) text += "\n\n" + uses;
            ChoiceLetter_PatientOffer letter = (ChoiceLetter_PatientOffer)LetterMaker.MakeLetter("RimWard.Offer.Label".Translate(), text, RimWardDefOf.RimWard_PatientOffer);
            letter.offerId = offer.id;
            letter.mapId = this.map.uniqueID;
            Find.LetterStack.ReceiveLetter(letter);
        }

        /// <summary>"Uses from your stock: ..." for a surgery case (the ingredients the operation consumes and their value).</summary>
        internal static string OfferStockNote(PatientData data)
        {
            RecipeDef r = data?.CureRecipe;
            if (r?.ingredients == null || r.ingredients.Count == 0) return null;
            List<string> parts = new List<string>();
            float value = 0f;
            foreach (IngredientCount ic in r.ingredients)
            {
                ThingDef d = ic.IsFixedIngredient ? ic.FixedIngredient : ic.filter?.AllowedThingDefs?.OrderBy(x => x.BaseMarketValue).FirstOrDefault();
                if (d == null) continue;
                float n = ic.GetBaseCount();
                parts.Add(n > 1f ? d.label + " x" + n : d.label);
                value += d.BaseMarketValue * n;
            }
            if (parts.Count == 0) return null;
            return "RimWard.Offer.UsesStock".Translate(string.Join(", ", parts), value.ToStringMoney());
        }

        public bool ResolveOffer(int id, bool accept)
        {
            PatientOffer offer = this.pendingOffers.FirstOrDefault(o => o.id == id);
            if (offer == null) return false;
            if (accept && this.IsFull())
            {
                // L6: never over capacity. The offer stays open until it expires or a bed frees up.
                Messages.Message("RimWard.Offer.NoBed".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            this.pendingOffers.Remove(offer);
            if (offer.pawn == null || offer.pawn.Discarded) return true;
            if (accept)
            {
                if (offer.kind == OfferKind.Ambient) AcceptAmbient(offer);
                else this.DeliverPatient(offer.pawn, offer.data, offer.faction);
            }
            else
            {
                DiscardOffer(offer);
            }
            return true;
        }

        private void DiscardOffer(PatientOffer offer)
        {
            // No goodwill penalty for declining. Discard a held drop-pod pawn; leave ambient pawns be.
            if (offer.kind == OfferKind.DropPod && offer.pawn != null && !offer.pawn.Spawned && !offer.pawn.Discarded)
            {
                IncidentHelper.DiscardGenerated(offer.pawn);
            }
        }

        private void AcceptAmbient(PatientOffer offer)
        {
            Pawn pawn = offer.pawn;
            if (pawn == null || !pawn.Spawned) return;
            if (pawn.playerSettings != null) pawn.playerSettings.selfTend = false;
            this.PatientArrived(pawn, offer.data);
            Lord old = pawn.GetLord();
            old?.RemovePawn(pawn);
            LordMaker.MakeNewLord(offer.faction, new LordJob_VisitColonyAsPatient(offer.faction), this.map, new List<Pawn> { pawn });
        }

        private void AmbientScan()
        {
            if (!IsOpen() || IsFull()) return;
            foreach (Pawn p in this.map.mapPawns.AllPawnsSpawned)
            {
                if (p == null || !p.RaceProps.Humanlike || p.Faction == null || p.Faction.IsPlayer) continue;
                if (p.IsPrisonerOfColony || p.IsSlaveOfColony || p.Dead || p.InAggroMentalState) continue;
                if (p.Faction.HostileTo(Faction.OfPlayer)) continue;
                if (IncidentHelper.IsWeirdPawn(p)) continue;
                if (QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p)) continue;
                Lord lord = p.GetLord();
                if (lord == null || !(lord.LordJob is LordJob_VisitColony)) continue; // only safe social visitors
                if (p.IsPatient(out _, true)) continue;
                if (this.pendingOffers.Any(o => o.pawn == p)) continue;
                if (!p.Downed && !p.health.HasHediffsNeedingTend(false) && !HealthAIUtility.ShouldSeekMedicalRest(p)) continue;

                PatientData data = new PatientData(Find.TickManager.TicksGame, p.MarketValue, p.needs?.mood?.CurLevel ?? 0f, PatientType.Wounds)
                {
                    Diagnosis = "RimWard.AmbientDiagnosis".Translate(),
                    Cure = "CureWounds".Translate(),
                    Bill = 10f
                };
                PatientOffer offer = new PatientOffer
                {
                    id = this.nextOfferId++,
                    pawn = p,
                    data = data,
                    faction = p.Faction,
                    kind = OfferKind.Ambient,
                    expiresTick = Find.TickManager.TicksGame + 2500 * 6
                };
                this.pendingOffers.Add(offer);
                SendOfferLetter(offer);
                break; // at most one new ambient offer per scan (anti-spam)
            }
        }

        // ---------------------------------------------------------------- discharge, death, dismissal

        /// <summary>Lord toil cleanup: the patient is on the way out. Cured patients pay here (dismissed ones paid when dismissed).</summary>
        public void PatientLeaves(Pawn pawn)
        {
            if (this.Patients.TryGetValue(pawn, out PatientData patientData) && !patientData.Dismissed)
            {
                this.Discharge(pawn, patientData, DischargeReason.Cured);
            }
        }

        /// <summary>
        /// Ends a stay: builds the invoice, pays, records stats, and sends the patient home. Every way out goes through here
        /// (cured, dismissed, impossible treatment, maximum stay), so the invoice the player sees is the silver they get.
        /// </summary>
        public Invoice Discharge(Pawn pawn, PatientData data, DischargeReason reason)
        {
            if (pawn == null || data == null) return null;
            Invoice inv = Invoice.Build(pawn, data, reason);
            HospitalGameComponent gc = HospitalGameComponent.Get;
            float silver = data.PaidOnDischarge ? 0f : inv.TotalSilver;
            data.PaidOnDischarge = true;
            bool cured = reason == DischargeReason.Cured;
            gc?.RecordDischarge(true, cured ? OutcomeUtility.DischargeScore(pawn, data, true) : 40f, silver);
            if (!cured && gc != null) gc.patientsDischargedEarly++;
            gc?.RecordInvoice(inv);

            int goodwill = 0;
            if (cured && pawn.Faction != null && !pawn.Faction.IsPlayer)
            {
                goodwill = OutcomeUtility.GoodwillFor(pawn, data);
                pawn.Faction.TryAffectGoodwillWith(Faction.OfPlayer, goodwill, false, true, null, null);
            }
            if (silver > 0f && pawn.Spawned)
            {
                Thing thing = ThingMaker.MakeThing(ThingDefOf.Silver);
                thing.stackCount = (int)silver;
                GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
            this.AnnounceDischarge(pawn, inv, reason, goodwill);

            if (!cured)
            {
                // nothing left to do here: cancel the open operations so the patient is free to go
                foreach (Bill b in pawn.health.surgeryBills.Bills.ToList()) pawn.health.surgeryBills.Delete(b);
                data.HasPendingSurgeryBill = false;
                data.RetryAfterTick = -1;
            }
            this.RemoveFromPatientList(pawn, true);
            return inv;
        }

        private void AnnounceDischarge(Pawn pawn, Invoice inv, DischargeReason reason, int goodwill)
        {
            string factionPart = (pawn.Faction != null && !pawn.Faction.IsPlayer && goodwill != 0)
                ? " " + "RimWard.Msg.Goodwill".Translate(goodwill.ToStringWithSign(), pawn.Faction.Name).ToString() : string.Empty;
            string line = ("RimWard.Msg.Discharge." + reason).Translate(pawn.LabelShortCap, ((float)inv.TotalSilver).ToStringMoney()).ToString() + factionPart;
            if (inv.TotalSilver > 0 && inv.TotalSilver >= HospitalMod.Settings.InvoiceLetterThreshold)
            {
                Find.LetterStack.ReceiveLetter("RimWard.Invoice.LetterLabel".Translate(pawn.LabelShortCap), line + "\n\n" + inv.ToText(), LetterDefOf.NeutralEvent, pawn);
            }
            else
            {
                Messages.Message(line, pawn, MessageTypeDefOf.NeutralEvent, true);
            }
        }

        // Fault-classified death penalty. Default: no goodwill loss for surgery deaths, deaths while
        // leaving, or patients who arrived already doomed. Only genuine neglect costs goodwill.
        public void PatientDied(Pawn pawn)
        {
            if (!this.Patients.TryGetValue(pawn, out PatientData data)) return;
            Settings s = HospitalMod.Settings;

            bool surgeryContext = data.HasPendingSurgeryBill || data.RetryAfterTick >= 0
                || (data.NoFaultUntilTick >= 0 && Find.TickManager.TicksGame <= data.NoFaultUntilTick);
            bool diedFast = Find.TickManager.TicksGame - data.ArrivedAtTick < 2500; // ~1h: lethal on arrival

            bool noFault = !s.DeathPenaltyEnabled
                || (s.NoFaultSurgery && surgeryContext)
                || (s.NoFaultLeaving && data.Dismissed)
                || (s.NoFaultUnwinnable && diedFast);

            int penalty = noFault ? 0 : -s.DeathPenaltyAmount;
            HospitalGameComponent gc = HospitalGameComponent.Get;
            gc?.RecordDischarge(false, 0f, 0f);
            if (!noFault && gc != null) gc.patientsDiedAtFault++;

            // "The operation was a success; the patient was not": a no-fault death may still pay its share of the bill.
            if (noFault && s.NoFaultDeathCanProfit && !data.Dismissed && !data.PaidOnDischarge && s.ChargePatients && pawn.Spawned)
            {
                Invoice inv = Invoice.Build(pawn, data, DischargeReason.DiedNoFault);
                data.PaidOnDischarge = true;
                gc?.RecordInvoice(inv);
                if (inv.TotalSilver > 0)
                {
                    Thing thing = ThingMaker.MakeThing(ThingDefOf.Silver);
                    thing.stackCount = inv.TotalSilver;
                    GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    if (gc != null) gc.silverEarned += inv.TotalSilver;
                }
            }

            if (penalty != 0 && pawn.Faction != null && !pawn.Faction.IsPlayer)
            {
                Messages.Message("RimWard.Msg.DiedFault".Translate(pawn.LabelShortCap, penalty.ToString(), pawn.Faction.Name), MessageTypeDefOf.PawnDeath, true);
                pawn.Faction.TryAffectGoodwillWith(Faction.OfPlayer, penalty, false, true, null, null);
            }
            else
            {
                string suffix = noFault ? " " + "RimWard.NoGoodwillLost".Translate() : "";
                Messages.Message("RimWard.Msg.Died".Translate(pawn.LabelShortCap) + suffix, MessageTypeDefOf.PawnDeath, true);
            }
            this.RemoveFromPatientList(pawn, true);
            this.PatientLeftTheMap(pawn);
        }

        // Surgery auto-retry: a botched operation re-attempts the cure (up to a configurable cap) instead of
        // giving up. Exhausted/disabled -> graceful downgrade (dismissable, NO penalty).
        public void OnSurgeryBotched(Pawn pawn)
        {
            if (!this.Patients.TryGetValue(pawn, out PatientData data)) return;
            Settings s = HospitalMod.Settings;
            data.NoFaultUntilTick = Find.TickManager.TicksGame + 2500; // a death right after a botch is no-fault
            if (pawn.Dead || data.CureRecipe == null) { data.HasPendingSurgeryBill = false; data.RetryAfterTick = -1; return; }
            if (CureAchieved(pawn, data)) { data.HasPendingSurgeryBill = false; data.RetryAfterTick = -1; return; }
            if (!s.AutoRetryEnabled || data.SurgeryAttempts >= s.MaxSurgeryRetries)
            {
                data.HasPendingSurgeryBill = false; data.RetryAfterTick = -1; return; // exhausted -> graceful give-up, no penalty
            }
            data.SurgeryAttempts++;
            data.RetryAfterTick = Find.TickManager.TicksGame + 600; // recreate the cure bill once the failed one is cleaned up
            data.HasPendingSurgeryBill = true;
        }

        private static BodyPartRecord ResolveTargetPart(Pawn pawn, RecipeDef recipe)
        {
            if (recipe.removesHediff != null)
            {
                Hediff h = pawn.health.hediffSet.hediffs.FirstOrDefault(x => x.def == recipe.removesHediff);
                if (h?.Part != null) return h.Part;
            }
            return recipe.Worker.GetPartsToApplyOn(pawn, recipe).FirstOrDefault();
        }

        /// <summary>The player sends a patient home early: they pay their share of the bill so far and leave (fixes "dismissed patients never leave").</summary>
        public void DismissPatient(Pawn pawn)
        {
            if (this.Patients.TryGetValue(pawn, out PatientData patientData) && !patientData.Dismissed)
            {
                bool uncured = patientData.HasPendingSurgeryBill || pawn.health.surgeryBills.Count > 0
                    || (pawn.Spawned && (pawn.health.HasHediffsNeedingTend(false) || HealthAIUtility.ShouldSeekMedicalRest(pawn)));
                if (uncured) IdeologyCompat.NotifyPatientTurnedAway(this.map);
                this.Discharge(pawn, patientData, uncured ? DischargeReason.Dismissed : DischargeReason.Cured);
            }
        }

        /// <summary>Marks the entry dismissed (it leaves the table and the lord walks out). resetGuest=false keeps a new prisoner or slave status untouched.</summary>
        public void RemoveFromPatientList(Pawn pawn, bool resetGuest)
        {
            if (this.Patients.TryGetValue(pawn, out PatientData patientData))
            {
                patientData.Dismissed = true;
            }
            if (resetGuest && !pawn.IsPrisoner && !pawn.IsSlave && (pawn.Faction == null || !pawn.Faction.IsPlayer))
            {
                if (pawn.playerSettings != null) pawn.playerSettings.selfTend = true;
                if (pawn.guest != null) pawn.guest.SetGuestStatus(null, GuestStatus.Guest);
            }
            MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();
        }

        /// <summary>Forgets a patient completely, without touching their status (arrested, enslaved, recruited, vanished).</summary>
        public void ForgetPatient(Pawn pawn)
        {
            if (pawn == null) return;
            if (this.Patients.Remove(pawn)) MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();
        }

        public void PatientLeftTheMap(Pawn pawn)
        {
            this.Patients.Remove(pawn);
        }

        public bool IsSurgeryRecipeAllowed(RecipeDef recipe)
        {
            return !this.Policy.refusedOperations.Any(def => def != null && def == recipe);
        }

        public void RefuseOperation(Pawn pawn, RecipeDef recipe)
        {
            List<RecipeDef> refused = this.Policy.refusedOperations;
            if (!refused.Any(def => def != null && def == recipe))
            {
                refused.Add(recipe);
                Messages.Message("RimWard.Msg.Refused".Translate(recipe.LabelCap), MessageTypeDefOf.NeutralEvent, true);
            }
            this.DismissPatient(pawn);
        }

        public void UnRefuseOperation(RecipeDef recipe)
        {
            this.Policy.refusedOperations.RemoveAll(def => def == null || def == recipe);
        }

        public void SetPatientFoodPolicy(FoodPolicy localRestriction)
        {
            this.Policy.FoodPolicy = localRestriction;
            this.ApplyPolicyToAll();
        }

        public bool IsFull()
        {
            return this.FreeMedicalBeds() <= 0;
        }

        public int FreeMedicalBeds()
        {
            return this.BedCount() - this.Patients.Count(pair => !pair.Value.Dismissed);
        }

        public int BedCount()
        {
            return this.map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Count(bed =>
                bed.Medical && !bed.ForPrisoners && bed.def.building.bed_humanlike && !bed.IsBurning() && bed.Spawned
                && bed.TryGetComp<CompHospitalBed>() != null && bed.TryGetComp<CompHospitalBed>().Hospital && bed.Map == this.map);
        }

        public int SurgeryBedCount()
        {
            return this.map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Count(bed =>
                bed.Medical && !bed.ForPrisoners && bed.def.building.bed_humanlike && bed.Spawned
                && (bed.TryGetComp<CompHospitalBed>()?.Surgery ?? false));
        }

        public IEnumerable<Pawn> ActivePatientsList()
        {
            return this.Patients.Where(pair => pair.Key != null && pair.Value != null && !pair.Value.Dismissed).Select(pair => pair.Key);
        }

        public bool GetPatientData(Pawn pawn, out PatientData patientData)
        {
            patientData = null;
            return pawn != null && this.Patients.TryGetValue(pawn, out patientData) && patientData != null;
        }

        /// <summary>Readable "why is this patient still here" (for the Hospital tab column). Empty for unknown pawns.</summary>
        public string WhyStillHere(Pawn pawn)
        {
            return this.GetPatientData(pawn, out PatientData d) ? Caretaker.WhyText(this, pawn, d) : string.Empty;
        }

        /// <summary>Same as <see cref="WhyStillHere"/> but as a status value.</summary>
        public PatientStatus StatusOf(Pawn pawn, out string detail)
        {
            detail = null;
            return this.GetPatientData(pawn, out PatientData d) ? Caretaker.StatusOf(this, pawn, d, out detail) : PatientStatus.None;
        }

        // Called by the caretaker when a patient's entry belongs to another map now (gravship, transport).
        internal void TransferPatientTo(Pawn pawn, HospitalMapComponent other)
        {
            if (other == null || other == this || !this.Patients.TryGetValue(pawn, out PatientData data)) return;
            this.Patients.Remove(pawn);
            other.Patients[pawn] = data;
        }
    }
}
