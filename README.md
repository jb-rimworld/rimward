# RimWard — Anesthesia Not Included
### *A hospital overhaul for RimWorld*

> *"First, do no harm. Second, do submit an itemized invoice."*

---

## What is this?

The Rim is full of people who are about to die in interesting ways. **RimWard** turns that
inexhaustible supply of human misfortune into a business model. Hang out a sign, lay down some beds,
and total strangers will crawl, limp, and get drop-podded to your door begging you to fix the holes
in them — and then *pay you for it*.

You are now a frontier hospital administrator. You decide who gets a bed, who gets a bill, and who
gets a polite "have you considered dying somewhere else?" Whether you run a saintly charity ward or a
for-profit organ-adjacent meat shop is entirely between you and your ideoligion.

We provide the patients. We provide the paperwork. The anesthesia, famously, is **not included.**

---

## The pitch

The old Hospital mod let patients show up and get healed. Lovely. But it also:

- punished you with lost goodwill **every** time a patient died — including on the operating table,
  including while they were *walking home cured*, including when they arrived already 90% dead;
- gave up the moment a surgery botched, leaving the patient face-down in a bed forever, slowly
  developing a personality made entirely of untended wounds;
- buried you under popups; and
- had roughly four settings.

RimWard fixes all of that, then keeps going until it becomes a hospital management sim with a body count.

---

## Features

**🩺 Triage, your way.** Run the **firehose** (patients just appear, no questions, good luck) or
flip on **triage** and vet every case from a single clean inbox: name, diagnosis, odds of survival,
and *exactly* how much silver is involved before you commit a bed. Mass casualty inbound? You get
**one** offer — *"Plane crash. 30 patients in 6 hours. Yes / absolutely not"* — not thirty letters.

**⚖️ A fair malpractice system.** We only blame you when it's *actually your fault*. Surgeon's hand
slipped? Clinical risk, no penalty. Patient bled out jogging to the exit after you cured them? Not
your problem. Showed up with a disease three days past terminal? You can't save everyone, and the
faction knows it. Let a patient rot in a hallway untended for a week, though — that one's on you.
Every line of it is a slider.

**🔁 Surgeries that don't give up.** Botch an operation and RimWard simply schedules another one.
And another. (Up to a configurable number — we're not *monsters*. That setting is right next to the
one that says we are.)

**📊 The discharge report card.** Every patient leaves with a fully itemized verdict. Removed their
infected leg without asking? Minus points. *Replaced* it with a bionic so they walk out faster than
they walked in? Big plus. Survived against the odds? Bonus. Picked up a fresh luciferium addiction
on your watch? ...we've all been there. Their faction tallies the whole story and pays — or sulks —
accordingly. **You are free to do whatever surgery you think is right.** Nobody gets to second-guess
the doctor until the patient's standing at the door.

**🏥 Renown & clinic tiers.** Save enough lives and your bloodstained shack becomes a name people
say with respect across the planet. Higher renown means richer patients, fatter payouts, and the
occasional "thank you" care package. Level up and the galaxy literally ships you medicine.

**🛏️ Location, location, location.** A private room with a real bed pays like a private room. A bunk
in a 12-bed barracks pays like a bunk in a 12-bed barracks. Capitalism, but with bedpans.

**👑 Difficult clientele.** Nobles who refuse to recover in anything less than a throne-adjacent
suite. Tribals who want herbal medicine and recoil at your glittertech. Modern factions who recoil
at your *herbs*. Patients on a drug clock who'll die mid-cure without their fix. Xenotypes with
needs, genes with opinions, and at least one person who will not stop asking for beer.

**💸 Money from "almost nothing" to "frankly a crime."** A central payment dial runs from charity
ward (you eat the cost, your ideoligion approves) through honest break-even, all the way up to
cheat-rich tycoon mode. One slider does the same for difficulty.

**🧟 Plays well with others.** Soft-compatible with More Injuries, Organ Failure, Zombieland (yes,
you can take in the freshly bitten and race the clock), Vanilla Psycasts & Words of Healing (psychic
surgery counts as medicine and gets paid), VFE Empire, Royalty, Ideology, Combat Extended, and
Multiplayer. Doesn't depend on any of them. Future injury mods just work, because RimWard judges
wounds by what they *are*, not by their name tag.

---

## What it is not

- A guarantee that everyone lives. (See: name of mod.)
- A reason to feel bad. The penalties are fair now. Probably.
- Responsible for what your colonists do with the leftover corpses.

---

## Compatibility & requirements

- **Requires:** Harmony (and Prepatcher on RimWorld 1.6 only; Prepatcher does not run on 1.7 and is not needed there).
- **Replaces:** the *Hospital* mod and its patches — please disable those; RimWard is the whole
  hospital now and will politely refuse to share the building.
- **Multiplayer:** supported.

---

## Works with Hearth & Harbor

Optional and soft (reflection only, no dependency in either direction). RimWorld 1.7 only: the 1.6 assembly is final and has none of this.
Both features are switches under *RimWard settings > Hearth & Harbor* and do nothing when Hearth & Harbor is not loaded.

**One compact bed button.** Instead of Hospital Bed, Surgery Bed and (from Hearth & Harbor) Guest Bed toggles piling up, a colony bed shows
a single button, *Use: Colonist / Guest / Patient / Surgery patient*, with a menu. Impossible entries are greyed out with the reason
(Patient and Surgery need the vanilla Medical switch; Guest is not available on medical beds). Prisoner and slave beds keep vanilla's buttons.
Medical stays vanilla's own switch. Several selected beds are changed together.

Protocol (both sides check, so exactly one set of buttons is ever shown):
- `RimWard.BedSelector.Draws(Building_Bed)` is true when RimWard draws the selector for that bed. Hearth & Harbor then hides its own "Guest bed" toggle
  (`CompHH_GuestBed.OtherDrawsSelector`).
- RimWard only takes over when Hearth & Harbor speaks the protocol: static `Hearth.Guests.CompHH_GuestBed.BedSelectorVersion` >= 1, instance
  `SelectorAvailable` / `SelectorBlockedReason`, plus the existing `IsGuestBed` and `SetGuestBed(bool, bool)`. With an older Hearth & Harbor both mods keep their own buttons.
- If Hearth & Harbor (or a shared foundation) ever draws the selector itself, it sets static `CompHH_GuestBed.DrawsBedSelector = true`; RimWard then only hides
  its toggles and the other side drives the bed through `RimWard.BedApi` (`Apply(bed, BedUse)`, `IsHospital/SetHospital`, `IsSurgery/SetSurgery`).

**Patients use guest leisure.** RimWard exposes `RimWard.PatientApi` (version `InteropVersion` = 1):
`IsPatient(Pawn)`, `IsWalkingPatient(Pawn)` (up and about: awake, can walk, not bleeding, nothing urgent to tend, no operation waiting; they may be in a bed right now),
`NeedsBedRest(Pawn)` and `Stage(Pawn)` = `Acute | Recovering | Observation | Discharged | None`. The patient duty gets a leisure node after "go to bed" (1.7 patch
`1.7/Patches/PatientLeisureDuty.xml`): a walking patient asks Hearth & Harbor's spa joy givers for something to do (hot tub, sauna, massage), at most once every ~2500 ticks.
Hearth & Harbor applies its own house rules on top (no sauna after fresh surgery, spa switched off, ...). Patients are not guests of the inn and are never billed by it;
RimWard stays the owner of the stay.

## Credits & license

RimWard is a GPL-3.0 fork built on the foundation of **Adamas's *Hospital*** and the fixes from
**assssssqwww's *Hospital Fix And Tweak***. Enormous thanks to both — we kept the parts that worked
and rebuilt the rest. Full source is available; do with it what the GPL lets you.

*Side effects of running a frontier hospital may include: wealth, fame, goodwill, sudden goodwill
loss, mild war crimes, and the persistent feeling that the storyteller is sending you these people
on purpose. Anesthesia not included.*
