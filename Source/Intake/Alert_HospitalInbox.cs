using RimWorld;
using Verse;

namespace RimWard
{
    // Subtle "X open patient offers" readout in the vanilla alert strip (auto-registered, no def needed).
    public class Alert_HospitalInbox : Alert
    {
        public Alert_HospitalInbox()
        {
            this.defaultPriority = AlertPriority.Medium;
        }

        private static int Count()
        {
            int n = 0;
            foreach (Map m in Find.Maps)
            {
                HospitalMapComponent c = HospitalMapComponent.For(m);
                if (c != null) n += c.PendingOfferCount;
            }
            return n;
        }

        public override string GetLabel() => "RimWard.Alert.Count".Translate(Count());

        public override TaggedString GetExplanation() => "RimWard.Alert.Explanation".Translate();

        public override AlertReport GetReport() => Count() > 0 ? AlertReport.Active : AlertReport.Inactive;
    }
}
