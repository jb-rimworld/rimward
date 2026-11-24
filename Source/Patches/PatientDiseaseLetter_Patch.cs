// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    /// <summary>
    /// Patients are guests of the colony, so vanilla sends its "new disease" letter (Disease: Infection, Flu...) for every illness they bring
    /// or catch, and the letters pile up. For a patient being made (the illness IS the case: it is in the arrival or offer letter) nothing is
    /// sent; for an admitted patient a message replaces the letter. Colonists and other guests are untouched.
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_Discoverable), "CheckDiscovered")]
    public static class PatientDiseaseLetter_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(HediffComp_Discoverable __instance, ref bool ___discovered)
        {
            if (___discovered || __instance?.parent == null) return true;
            Pawn pawn = __instance.Pawn;
            if (pawn == null || pawn.Faction == Faction.OfPlayer) return true;
            bool preparing = IncidentHelper.InPreparation(pawn);
            bool patient = !preparing && pawn.IsPatient(out _, true);
            if (!preparing && !patient) return true;
            if (!__instance.parent.Visible) return false;
            ___discovered = true;
            if (patient && __instance.Props.sendLetterWhenDiscovered && Current.ProgramState == ProgramState.Playing)
                Messages.Message("RimWard.Msg.PatientNewDisease".Translate(pawn.LabelShortCap, __instance.Def.label), pawn, MessageTypeDefOf.NegativeHealthEvent, false);
            return false;
        }
    }
}
