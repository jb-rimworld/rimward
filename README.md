# RimWard – Hospital for Profit (Anesthesia Not Included)
### *A hospital overhaul for RimWorld*

Source: https://github.com/jb-rimworld/rimward (GPL-3.0)

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

The old Hospital mod let patients show up and get healed. Lovely. But it also punished you for deaths on the operating table,
gave up after one botched surgery, let patients sit in your beds forever, and charged for operations that failed.
RimWard fixes that and adds the paperwork.

---

## Features (built and tested on RimWorld 1.7; the 1.6 build is best effort and untested: the same features, compiled for 1.6 but never run there)

**Triage, your way.** Three intake modes. *Firehose*: patients just arrive. *Prompt*: every case is a letter with name, faction,
diagnosis, the estimated payment and, for an operation, what it uses from your stock. *Hybrid* (the default): letters only for surgery cases
and titled nobles. Unanswered offers expire (1 to 24 hours, default 6). Declining is free. Accepting with no free patient bed is
refused and the offer stays open.

**One invoice.** The Bill column, the offer letter and the silver you receive come from the same calculation: what was done
(admission, each tend with medicine, each meal fed, each operation and the materials it used) times the payment scale, the room
tier, a chief physician, the faction's medicine preference, a noble's room demand and your renown, capped at the maximum payout.
Hover a row for the itemised bill. Large payouts arrive as a letter with the full invoice (threshold is a setting).

**Every treatment is billed, failures are not.** Any recipe on a patient, vanilla or modded, is billed when it completes,
except what the patient's faction counts as a violation (harvesting an organ): that costs goodwill as on any guest and pays nothing.
A failed operation charges no fee; the medicine and parts it used up are reimbursed (two settings). A botched cure is retried
up to 3 times (setting), and the patient cannot walk out between the botch and the retry.

**Nobody gets stuck.** A caretaker checks every patient every few seconds:
- dismissed patients leave and pay their share of the bill so far (default 50%);
- an operation that cannot be done (part gone, nobody skilled enough, missing ingredient, refused) sends a letter after 24 hours
  (setting) and discharges the patient after another 24, with no penalty;
- a maximum stay (default 15 days, 0 = off) discharges with the bill so far;
- cured patients wait for an active threat to pass for at most 24 hours (setting); dormant mech clusters and weather never hold
  anyone, and weather only stops new arrivals;
- arrested, enslaved or recruited patients are dropped from the list without undoing their new status;
- vanished patients are cleaned out before saving (no "null key" on load).
Each patient has a readable reason why they are still here, shown on the Hospital tab.

**A fair malpractice system.** You only lose goodwill when it is your fault. Death in surgery or right after a botch, after dismissal,
or within an hour of arrival: not on you. A no-fault death can still pay its share of the bill (setting). Neglect costs goodwill.

**Policies per save.** Open/closed, opening hours, what the hospital accepts, food and medicine for patients and refused operations
are stored once per save and apply to every map, including the new one after a gravship landing. A map can have its own policies
(checkbox). New colonies start with the hospital open (setting); nobody comes until a medical bed is set to Patient.

**Room-quality pay, renown, difficult clientele.** A private room pays 135%, a two-bed room 110%, a ward 85%, a hallway less. A chief
physician (Medicine 12+) adds 25%. Four renown tiers: +5%, +10%, +15% above Field Aid. Nobles pay 40% more for a private room and 25% less for a shared one.
Tribal factions dislike glitterworld medicine, modern ones dislike herbs. Patients arrive with extra illnesses or chronic conditions
(never anything that kills them or sends them berserk). They keep their faction's own xenotype; hemogen and deathrest xenotypes are
not sent unless you allow them.

**Mass casualty events** (opt-in per save): a pandemic, raid, crash or fire sends a group of patients, at most as many as you have free
patient beds. No triage letter.

**Contracts:** injured visitors and allies on your map offer to pay for treatment.

**English and German.**

---

## What it is not

- A guarantee that everyone lives. (See: name of mod.)
- Multiplayer-ready. Not supported, not tested.
- Responsible for what your colonists do with the leftover corpses.

---

## Compatibility & requirements

- **Requires:** Harmony and JB Framework (same author), on RimWorld 1.7 and 1.6.
- If JB Framework is missing, the game offers to install it when it starts (RimWard stays switched off until then).
- **Replaces:** the *Hospital* mod and its patches. Please disable those.
- **Multiplayer:** not supported.
- **Combat Extended:** CE's "tend yourself" tactic is switched off for patients (soft patch, only when CE is loaded).
- **Alien races / other languages:** body parts are found by their definition and tags, not by their (translated) names.
- **Injury and medicine mods:** wounds, diseases and operations are read from the game's health system. Modded operations are billed
  like vanilla ones. Nothing mod-specific beyond what is listed here.
- **Hospital Beds Gen 2:** its beds are medical beds, so RimWard uses them. MedPod-style beds (detected by their building class) are not
  patient beds by default and have no surgery use.
- If a patch fails after a game or mod update, only that feature is off and the log names it.

---

## Adding and removing

- **Adding to a running colony** works: the hospital opens (setting "Open the hospital in new colonies") and patients come once a medical bed takes patients.
- **Removing:** *Mod settings > RimWard > Patient intake > Prepare for removal* (also on the Diagnosis page), then save. Patients are sent home on
  vanilla's "leave the map" (paid like an early discharge), offers and hospital letters are withdrawn, "turned a patient away" memories and landing
  spots are removed, intake is switched off. That save loads without RimWard with no error from it. RimWard's state (policies, renown, invoices,
  patients) is stored in JB Framework's save data and comes back if you add RimWard again.
- **Removing without preparing:** the first load shows red errors for every patient still on the map and every open hospital letter (the game
  cannot read their classes any more); the colony plays on; those patients lose
  their visit, and the ones who can walk leave while one still in bed stays as an ordinary guest (vanilla follow-up errors from their lost lords
  stop after the first ticks).

---

## Works with Hearth & Harbor

Optional and soft (reflection only, no dependency in either direction).
Both features are switches under *RimWard settings > Hearth & Harbor bridge* and do nothing when Hearth & Harbor is not loaded.

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
