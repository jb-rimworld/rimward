using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    // Per-map "Hospital Policies" dialog (parity). Multiplayer sync wrapping is added back in Step 11;
    // this Step-1 version edits per-map state directly (correct in singleplayer).
    public class Dialog_MapSettings : Window
    {
        private readonly Map map;
        private readonly HospitalMapComponent hospital;

        public Dialog_MapSettings(Map map)
        {
            this.map = map;
            this.hospital = map.GetComponent<HospitalMapComponent>();
            this.forcePause = true;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(450f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard { ColumnWidth = inRect.width };
            listing.Begin(inRect);
            listing.Label("HospitalSettings".Translate());
            listing.Gap(12f);
            listing.CheckboxLabeled("AcceptPatients".Translate(), ref this.hospital.openForBusiness, "AcceptPatientsTooltip".Translate());
            listing.CheckboxLabeled("MassCasualties".Translate(), ref this.hospital.MassCasualties, "MassCasualtiesTooltip".Translate());
            listing.CheckboxLabeled("AcceptSurgery".Translate(), ref this.hospital.AcceptSurgery, "AcceptSurgeryTooltip".Translate());
            listing.CheckboxLabeled("AcceptDanger".Translate(), ref this.hospital.AcceptDanger, "AcceptDangerTooltip".Translate());
            listing.End();

            int y = 120;
            TimetableUtility.DoHeader(new Rect(0f, y, inRect.width, 50f));
            y += 60;
            TimetableUtility.DoCell(new Rect(0f, y, inRect.width, 20f), this.hospital.openingHours, this.map);
            y += 34;

            Rect labelRect = new Rect(inRect.x, y, 170f, 28f);
            Rect ctrlRect = new Rect(170f, y, 140f, 28f);
            Widgets.LabelFit(labelRect, "DefaultMedicineSettings".Translate());
            MedicalCareUtility.MedicalCareSetter(ctrlRect, ref Find.PlaySettings.defaultCareForNeutralFaction);
            labelRect.y += 34f;

            Rect foodLabel = new Rect(0f, labelRect.y, 140f, 23f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(foodLabel, Text.TinyFontSupported ? "FoodPolicy".Translate() : "FoodPolicyShort".Translate());
            GenUI.ResetLabelAlign();
            if (Widgets.ButtonText(new Rect(foodLabel.width, labelRect.y, 140f, 23f), this.hospital.PatientFoodPolicy.label))
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
            labelRect.y += 23f;

            if (this.hospital.refusedOperations == null) return;
            Widgets.Label(labelRect, "RefusedOperations".Translate());
            labelRect.y += 20f;
            Rect xRect = new Rect(350f, labelRect.y, 23f, 23f);
            foreach (RecipeDef recipe in this.hospital.refusedOperations.ToList())
            {
                if (recipe == null) continue;
                Widgets.Label(labelRect, recipe.LabelCap);
                if (Widgets.ButtonText(xRect, "X"))
                {
                    this.hospital.UnRefuseOperation(recipe);
                }
                labelRect.y += 20f;
                xRect.y += 20f;
                if (labelRect.y > 600f) break;
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            Find.PlaySettings.defaultCareForFriendlyFaction = Find.PlaySettings.defaultCareForNeutralFaction;
        }
    }
}
