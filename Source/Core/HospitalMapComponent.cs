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
    // Central per-map hospital state (parity fork). Payment/goodwill logic here is replaced by the
    // unified Outcome Score in Step 4/10; payout caps are raised to 20000 (HospitalTweak fix folded in)
    // and faction calls are null-guarded so faction-independent patients never NRE.
    public class HospitalMapComponent : MapComponent
    {
        public const float BillCap = 20000f;

        public Dictionary<Pawn, PatientData> Patients;
        private List<Pawn> _patientsKeysWorkingList;
        private List<PatientData> _patientsValuesWorkingList;

        public bool openForBusiness;
        public List<bool> openingHours = DefaultOpeningHours();
        public List<RecipeDef> refusedOperations = new List<RecipeDef>();
        public FoodPolicy PatientFoodPolicy;
        public bool MassCasualties;
        public bool AcceptSurgery;
        public bool AcceptDanger;

        public List<PatientOffer> pendingOffers = new List<PatientOffer>();
        private int nextOfferId = 1;
        public int PendingOfferCount => this.pendingOffers.Count;

        public HospitalMapComponent(Map map) : base(map)
        {
            this.Patients = new Dictionary<Pawn, PatientData>();
            this.PatientFoodPolicy = Current.Game.foodRestrictionDatabase.DefaultFoodRestriction();
        }

        private static List<bool> DefaultOpeningHours()
        {
            return new List<bool>
            {
                false, false, false, false, false, false, false, true, false, false,
                false, true, false, false, false, true, false, false, false, true,
                false, false, false, false
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (this.openingHours == null) this.openingHours = DefaultOpeningHours();
            if (this.refusedOperations == null) this.refusedOperations = new List<RecipeDef>();
            Scribe_Collections.Look(ref this.openingHours, "openingHours", LookMode.Undefined);
            Scribe_Collections.Look(ref this.refusedOperations, "refusedOperations", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.refusedOperations?.RemoveAll(r => r == null);
            }
            Scribe_Values.Look(ref this.openForBusiness, "openForBusiness", false);
            if (this.Patients == null) this.Patients = new Dictionary<Pawn, PatientData>();
            Scribe_Collections.Look(ref this.Patients, "patients", LookMode.Reference, LookMode.Deep, ref this._patientsKeysWorkingList, ref this._patientsValuesWorkingList);
            Scribe_References.Look(ref this.PatientFoodPolicy, "PatientFoodPolicy");
            if (this.PatientFoodPolicy == null && Current.Game != null)
            {
                this.PatientFoodPolicy = Current.Game.foodRestrictionDatabase.DefaultFoodRestriction();
            }
            Scribe_Values.Look(ref this.MassCasualties, "massCasualties", false);
            Scribe_Values.Look(ref this.AcceptSurgery, "acceptSurgery", true);
            Scribe_Values.Look(ref this.AcceptDanger, "AcceptDanger", false);
            Scribe_Collections.Look(ref this.pendingOffers, "pendingOffers", LookMode.Deep);
            Scribe_Values.Look(ref this.nextOfferId, "nextOfferId", 1);
            if (this.pendingOffers == null) this.pendingOffers = new List<PatientOffer>();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager.TicksGame % 250 != 0) return;
            int now = Find.TickManager.TicksGame;

            // Expire stale offers (auto-decline, no penalty) + scan for injured allies on the map.
            for (int i = this.pendingOffers.Count - 1; i >= 0; i--)
            {
                PatientOffer o = this.pendingOffers[i];
                if (now >= o.expiresTick || o.pawn == null || o.pawn.Dead || (o.kind == OfferKind.Ambient && !o.pawn.Spawned))
                {
                    this.pendingOffers.RemoveAt(i);
                    DiscardOffer(o);
                }
            }
            if (HospitalMod.Settings.AmbientContractsEnabled && now % 1000 == 0) AmbientScan();

            foreach (KeyValuePair<Pawn, PatientData> kv in this.Patients)
            {
                Pawn pawn = kv.Key;
                PatientData data = kv.Value;
                if (data == null || data.Dismissed || pawn == null || !pawn.Spawned) continue;

                // Track the best room the patient actually rested in (used at discharge for pay/goodwill,
                // since by discharge they may be up and walking, which would read as a hallway).
                if (pawn.InBed())
                {
                    RoomTier t = RoomTierUtility.Classify(pawn.CurrentBed(), pawn);
                    if ((int)t > (int)data.BestRoomTier) data.BestRoomTier = t;
                }

                // Auto-rescue: a downed patient on the floor is carried to a hospital bed by a free doctor,
                // exactly like a downed colonist.
                if (pawn.Downed && !pawn.InBed()) TryAutoRescue(pawn);

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
                            HealthCardUtility.CreateSurgeryBill(pawn, data.CureRecipe, ResolveTargetPart(pawn, data.CureRecipe), null, false);
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

        private static bool CureAchieved(Pawn pawn, PatientData data)
        {
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
            return this.openForBusiness && this.openingHours[GenLocalDate.HourOfDay(this.map)];
        }

        public void PatientArrived(Pawn pawn, PatientData data)
        {
            data.AdmitHealth = pawn.health.summaryHealth.SummaryHealthPercent;
            this.Patients.Add(pawn, data);
            MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();
        }

        // === Triage / intake offers ===

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
                if (!pawn.Destroyed) pawn.Discard(true);
                return;
            }
            info.openDelay = 60;
            info.leaveSlag = false;
            info.despawnPodBeforeSpawningThing = true;
            info.spawnWipeMode = WipeMode.Vanish;
            DropPodUtility.MakeDropPodAt(cell, this.map, info);
            this.PatientArrived(pawn, data);
            pawn.jobs.StopAll();
            pawn.foodRestriction = new Pawn_FoodRestrictionTracker(pawn) { CurrentFoodPolicy = this.PatientFoodPolicy };
            LordMaker.MakeNewLord(faction, new LordJob_VisitColonyAsPatient(faction), this.map, new List<Pawn> { pawn });
        }

        // Hold a generated patient as a world pawn and present an accept/decline letter.
        public void CreateDropPodOffer(Pawn pawn, PatientData data, Faction faction)
        {
            if (!pawn.IsWorldPawn()) Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
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
            float estPay = PatientUtility.CalculateSilverToReceive(offer.pawn, offer.data);
            string faction = offer.faction != null ? offer.faction.Name : "RimWard.NoFaction".Translate().ToString();
            TaggedString text = "RimWard.Offer.Text".Translate(offer.pawn.LabelShortCap, faction, offer.data.Diagnosis ?? "?", Mathf.RoundToInt(estPay));
            ChoiceLetter_PatientOffer letter = (ChoiceLetter_PatientOffer)LetterMaker.MakeLetter("RimWard.Offer.Label".Translate(), text, RimWardDefOf.RimWard_PatientOffer);
            letter.offerId = offer.id;
            letter.mapId = this.map.uniqueID;
            Find.LetterStack.ReceiveLetter(letter);
        }

        public bool ResolveOffer(int id, bool accept)
        {
            PatientOffer offer = this.pendingOffers.FirstOrDefault(o => o.id == id);
            if (offer == null) return false;
            this.pendingOffers.Remove(offer);
            if (offer.pawn == null) return true;
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
            if (offer.kind == OfferKind.DropPod && offer.pawn != null && !offer.pawn.Spawned)
            {
                if (offer.pawn.IsWorldPawn()) Find.WorldPawns.RemovePawn(offer.pawn);
                offer.pawn.Discard(true);
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

        public void PatientLeaves(Pawn pawn)
        {
            if (this.Patients.TryGetValue(pawn, out PatientData patientData) && !patientData.Dismissed)
            {
                float baseSilver = PatientUtility.CalculateSilverToReceive(pawn, patientData);
                float num = Mathf.Clamp(baseSilver * OutcomeUtility.PaymentMultiplier(pawn, patientData), 0f, HospitalMod.Settings.BillCap);
                HospitalGameComponent.Get?.RecordDischarge(true, OutcomeUtility.DischargeScore(pawn, patientData, true), num);
                if (num > 0f)
                {
                    if (pawn.Faction != null && !pawn.Faction.IsPlayer)
                    {
                        int num2 = OutcomeUtility.GoodwillFor(pawn, patientData);
                        Messages.Message($"{pawn.NameFullColored} leaves: +" + num.ToStringMoney() + ", goodwill change: " + num2 + " " + pawn.Faction.Name, MessageTypeDefOf.NeutralEvent, true);
                        pawn.Faction.TryAffectGoodwillWith(Faction.OfPlayer, num2, false, true, null, null);
                    }
                    else
                    {
                        Messages.Message($"{pawn.NameFullColored} leaves: +" + num.ToStringMoney(), MessageTypeDefOf.NeutralEvent, true);
                    }
                    Thing thing = ThingMaker.MakeThing(ThingDefOf.Silver);
                    thing.stackCount = (int)num;
                    GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
                this.RemoveFromPatientList(pawn);
            }
        }

        // Fault-classified death penalty. Default: no goodwill loss for surgery deaths, deaths while
        // leaving, or patients who arrived already doomed. Only genuine neglect costs goodwill.
        public void PatientDied(Pawn pawn)
        {
            if (!this.Patients.TryGetValue(pawn, out PatientData data)) return;
            Settings s = HospitalMod.Settings;

            bool surgeryContext = data.HasPendingSurgeryBill
                || (data.NoFaultUntilTick >= 0 && Find.TickManager.TicksGame <= data.NoFaultUntilTick);
            bool diedFast = Find.TickManager.TicksGame - data.ArrivedAtTick < 2500; // ~1h: lethal on arrival

            bool noFault = !s.DeathPenaltyEnabled
                || (s.NoFaultSurgery && surgeryContext)
                || (s.NoFaultLeaving && data.Dismissed)
                || (s.NoFaultUnwinnable && diedFast);

            int penalty = noFault ? 0 : -s.DeathPenaltyAmount;
            HospitalGameComponent.Get?.RecordDischarge(false, 0f, 0f);

            if (penalty != 0 && pawn.Faction != null && !pawn.Faction.IsPlayer)
            {
                Messages.Message($"{pawn.NameFullColored} died: {penalty} " + pawn.Faction.Name, MessageTypeDefOf.PawnDeath, true);
                pawn.Faction.TryAffectGoodwillWith(Faction.OfPlayer, penalty, false, true, null, null);
            }
            else
            {
                string suffix = noFault ? " " + "RimWard.NoGoodwillLost".Translate() : "";
                Messages.Message($"{pawn.NameFullColored} died." + suffix, MessageTypeDefOf.PawnDeath, true);
            }
            this.RemoveFromPatientList(pawn);
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

        public void DismissPatient(Pawn pawn)
        {
            if (this.Patients.TryGetValue(pawn, out PatientData patientData) && !patientData.Dismissed)
            {
                bool uncured = patientData.HasPendingSurgeryBill || pawn.health.surgeryBills.Count > 0
                    || (pawn.Spawned && (pawn.health.HasHediffsNeedingTend(false) || HealthAIUtility.ShouldSeekMedicalRest(pawn)));
                patientData.HasPendingSurgeryBill = false;
                Messages.Message($"{pawn.NameFullColored} dismissed.", MessageTypeDefOf.NeutralEvent, true);
                if (uncured) IdeologyCompat.NotifyPatientTurnedAway(this.map);
                this.RemoveFromPatientList(pawn);
            }
        }

        private void RemoveFromPatientList(Pawn pawn)
        {
            if (this.Patients.TryGetValue(pawn, out PatientData patientData))
            {
                patientData.Dismissed = true;
            }
            if (pawn.playerSettings != null) pawn.playerSettings.selfTend = true;
            if (pawn.guest != null) pawn.guest.SetGuestStatus(null, GuestStatus.Guest);
            MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();
        }

        public void PatientLeftTheMap(Pawn pawn)
        {
            this.Patients.Remove(pawn);
        }

        public bool IsSurgeryRecipeAllowed(RecipeDef recipe)
        {
            return !this.refusedOperations.Any(def => def != null && def == recipe);
        }

        public void RefuseOperation(Pawn pawn, RecipeDef recipe)
        {
            if (!this.refusedOperations.Any(def => def != null && def == recipe))
            {
                this.refusedOperations.Add(recipe);
                Messages.Message($"{recipe.LabelCap} blacklisted.", MessageTypeDefOf.NeutralEvent, true);
            }
            this.DismissPatient(pawn);
        }

        public void UnRefuseOperation(RecipeDef recipe)
        {
            this.refusedOperations.RemoveAll(def => def == null || def == recipe);
        }

        public void SetPatientFoodPolicy(FoodPolicy localRestriction)
        {
            this.PatientFoodPolicy = localRestriction;
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

        public IEnumerable<Pawn> ActivePatientsList()
        {
            return this.Patients.Where(pair => !pair.Value.Dismissed).Select(pair => pair.Key);
        }

        public bool GetPatientData(Pawn pawn, out PatientData patientData)
        {
            return this.Patients.TryGetValue(pawn, out patientData);
        }

        public void AddSurgeryBill(Pawn pawn, float amount)
        {
            if (pawn == null) return;
            if (!this.Patients.TryGetValue(pawn, out PatientData patientData)) return;
            patientData.Bill += amount;
            patientData.Bill = Mathf.Clamp(patientData.Bill, 0f, BillCap);
        }
    }
}
