using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    // Parity arrival cadence (auto-instantiated MapComponent). Replaced by the target-concurrency
    // volume controller in Step 6; kept faithful here (bug fixed: uses this.map, not Find.CurrentMap).
    public class IncidentScheduler : MapComponent
    {
        private bool MCEtriggered;

        public IncidentScheduler(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.MCEtriggered, "MCEtriggered", false);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (GenTicks.TicksGame % 42 != 0) return;

            int free = this.map.GetComponent<HospitalMapComponent>().FreeMedicalBeds();
            float chance = (GenDate.DaysPassed % 2 == 0) ? 0.008f : 0.004f;
            chance *= Mathf.InverseLerp(0f, 20f, free);
            if (Rand.Chance(chance))
            {
                IncidentParms parms = new IncidentParms { target = this.map };
                DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives").Worker.TryExecute(parms);
            }

            if (GenDate.DaysPassed % 7 == 0 && GenDate.DaysPassed > 0)
            {
                if (!this.MCEtriggered)
                {
                    IncidentParms parms2 = new IncidentParms { target = this.map };
                    this.MCEtriggered = DefDatabase<IncidentDef>.GetNamed("RimWard_MassCasualtyEvent").Worker.TryExecute(parms2);
                }
            }
            else
            {
                this.MCEtriggered = false;
            }
        }
    }
}
