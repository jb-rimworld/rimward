using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    // "Hospital Policies" dialog. Edits the save's policy (shared by every map and kept across gravship landings),
    // or this map's own copy when "This map has its own policies" is ticked. Never touches game-wide play settings.
    public class Dialog_MapSettings : Window
    {
        private readonly Map map;
        private readonly HospitalMapComponent hospital;
        private Vector2 scroll;

        public Dialog_MapSettings(Map map)
        {
            this.map = map;
            this.hospital = HospitalMapComponent.For(map);
            this.forcePause = true;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(480f, 640f);

        public override void DoWindowContents(Rect inRect)
        {
            HospitalPolicy pol = this.hospital.Policy;
            Listing_Standard listing = new Listing_Standard { ColumnWidth = inRect.width };
            listing.Begin(inRect);
            listing.Label("HospitalSettings".Translate());
            listing.Gap(6f);
            bool own = this.hospital.HasOwnPolicy;
            bool ownBefore = own;
            listing.CheckboxLabeled("RimWard.Policy.OwnMap".Translate(), ref own, "RimWard.Policy.OwnMapTip".Translate());
            if (own != ownBefore) { this.hospital.SetOwnPolicy(own); pol = this.hospital.Policy; }
            listing.Gap(6f);
            listing.CheckboxLabeled("AcceptPatients".Translate(), ref pol.open, "AcceptPatientsTooltip".Translate());
            listing.CheckboxLabeled("MassCasualties".Translate(), ref pol.massCasualties, "MassCasualtiesTooltip".Translate());
            listing.CheckboxLabeled("AcceptSurgery".Translate(), ref pol.acceptSurgery, "AcceptSurgeryTooltip".Translate());
            listing.CheckboxLabeled("AcceptDanger".Translate(), ref pol.acceptDanger, "AcceptDangerTooltip".Translate());
            float y = listing.CurHeight + 6f;
            listing.End();

            pol.Repair();
            TimetableUtility.DoHeader(new Rect(0f, y, inRect.width, 30f));
            y += 34f;
            TimetableUtility.DoCell(new Rect(0f, y, inRect.width, 20f), pol.openingHours, this.map);
            y += 34f;

            Rect labelRect = new Rect(0f, y, 170f, 28f);
            Rect ctrlRect = new Rect(175f, y, 140f, 28f);
            Widgets.LabelFit(labelRect, "DefaultMedicineSettings".Translate());
            MedicalCareUtility.MedicalCareSetter(ctrlRect, ref pol.medCare);
            y += 34f;

            Rect foodLabel = new Rect(0f, y, 170f, 23f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(foodLabel, "FoodPolicy".Translate());
            GenUI.ResetLabelAlign();
            if (Widgets.ButtonText(new Rect(175f, y, 160f, 23f), pol.FoodPolicy?.label ?? "-"))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (FoodPolicy policy in Current.Game.foodRestrictionDatabase.AllFoodRestrictions)
                {
                    FoodPolicy local = policy;
                    options.Add(new FloatMenuOption(local.label, () => this.hospital.SetPatientFoodPolicy(local)));
                }
                options.Add(new FloatMenuOption("ManageFoodPolicies".Translate(), () => Find.WindowStack.Add(new Dialog_ManageFoodPolicies(null))));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += 30f;

            Widgets.Label(new Rect(0f, y, inRect.width, 24f), "RefusedOperations".Translate());
            y += 24f;
            Rect outRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);
            List<RecipeDef> refused = pol.refusedOperations.Where(r => r != null).ToList();
            Rect view = new Rect(0f, 0f, outRect.width - 20f, Mathf.Max(outRect.height, refused.Count * 24f));
            Widgets.BeginScrollView(outRect, ref this.scroll, view);
            float ry = 0f;
            if (refused.Count == 0) Widgets.Label(new Rect(0f, 0f, view.width, 24f), "RimWard.Policy.NoneRefused".Translate());
            foreach (RecipeDef recipe in refused)
            {
                Widgets.Label(new Rect(0f, ry, view.width - 30f, 24f), recipe.LabelCap);
                if (Widgets.ButtonImage(new Rect(view.width - 24f, ry + 2f, 20f, 20f), TexButton.CloseXSmall))
                {
                    this.hospital.UnRefuseOperation(recipe);
                }
                ry += 24f;
            }
            Widgets.EndScrollView();
        }

        public override void PostClose()
        {
            base.PostClose();
            // apply food and medicine policy to the patients on every map that uses this policy
            foreach (Map m in Find.Maps) HospitalMapComponent.For(m)?.ApplyPolicyToAll();
        }
    }
}
