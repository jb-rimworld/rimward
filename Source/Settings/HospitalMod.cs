using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimWard
{
    [StaticConstructorOnStartup]
    public class HospitalMod : Mod
    {
        public static Harmony harmonyInstance;
        public static Settings Settings;

        public HospitalMod(ModContentPack content) : base(content)
        {
            HospitalMod.Settings = base.GetSettings<Settings>();
            HospitalMod.harmonyInstance = new Harmony("juliusberger.rimward");
            HospitalMod.harmonyInstance.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            HospitalMod.Settings.DoWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimWard";
        }
    }
}
