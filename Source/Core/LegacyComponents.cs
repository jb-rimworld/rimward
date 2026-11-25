// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using RimWorld;
using Verse;

namespace RimWard
{
    // The component classes that saves of RimWard 1.x and of the 2.0 betas contain. Registered with KitRegistry.LegacyComponent: they only
    // READ their old fields into HospitalSave / HospitalMap while a save loads; the framework never writes them into a save again and drops
    // their instances once the load (or a new game) finished. Their names are in old saves: never rename them. They do nothing else.

    /// <summary>Old per-save component: its fields are exactly <see cref="HospitalSave"/>'s, read into the running game's state.</summary>
    public sealed class HospitalGameComponent : GameComponent
    {
        public HospitalGameComponent(Game game) { }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) return;   // never written (the framework strips it before saving anyway)
            HospitalSave.ExposeCurrent();
        }
    }

    /// <summary>Old per-map component: its fields are exactly <see cref="HospitalMap"/>'s (without the weekly flag), read into that map's state.</summary>
    public sealed class HospitalMapComponent : MapComponent
    {
        public HospitalMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) return;
            HospitalMap.For(this.map)?.ExposeData(false);
        }
    }

    /// <summary>Old per-map arrival scheduler: only its weekly mass-casualty flag was saved.</summary>
    public sealed class IncidentScheduler : MapComponent
    {
        public IncidentScheduler(Map map) : base(map) { }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) return;
            HospitalMap h = HospitalMap.For(this.map);
            if (h != null) Scribe_Values.Look(ref h.mceTriggered, "MCEtriggered", false);
        }
    }
}
