# Changelog

## 2.0.0 (2026-11-25)

- **Needs JB Framework** (a new required mod, same author; Harmony as before), on RimWorld 1.7 and 1.6. RimWorld 1.6 gets the same 2.0 version; Prepatcher is no longer listed.
- Patients actually come with the default settings: a new hospital is open all day (it used to open only at 7, 11, 15 and 19, which let about one patient a week through), and a hospital with even one free bed gets about one patient a day.
- The default intake is now Hybrid: wound and disease cases just arrive, but a letter asks first before an operation, which may use a bionic part or medicine from your stock. Firehose (everybody just arrives) is still one click away.
- New Hospital tab: an open/closed switch you cannot miss, free beds, patients, offers and income this quadrum at a glance, and a checklist of what is still missing before patients come, most lines with a button that fixes it (open now, use the medical beds, a surgery bed, a doctor, forbidden medicine, a landing spot).
- The tab lists every patient with their state and why they are still here, takes offers with Accept / Turn away (letters are optional now), has the hospital policies as a page and a short guide.
- New settings window (JB Framework): pages per area, search, presets Clinic / Charity / Meat shop. Rows that only matter while a switch above them is on (payment scale, chief physician, retries) grey out with the reason. Your old RimWard settings carry over.
- One welcome card per colony (in English or German): what RimWard does, a preset to start with, and two buttons, "Open the hospital now" and "Use all medical beds for patients", that show green once done.
- Settings now belong to each save; the settings file is the template for new games.
- Switch whole areas off: patient intake, surgery cases, mass casualties, complications, billing extras, malpractice, renown, noble patients, Guilty ideoligions, the Hearth & Harbor bridge and Combat Extended.
- New pages: "Hospital policies (this colony)", "Pricing explained" and "Blacklists" for diseases, operations and factions you never want to see.
- New, off by default and only with Luciferium Overhaul: Luciferium critical cases. A doctor gives one dose from your stock and the patient pays very well.
- Compatibility page lists Hospital Beds Gen 2, Hearth & Harbor, Luciferium Overhaul, Combat Extended and the DLCs, and warns about Hospital and its patches.
- Surgery patients no longer crash on arrival, and their operation lands on the right body part, also with alien races and in other languages.
- Patients never bring a deadly or rage-causing addiction, and never carcinoma or another chronic disease as their case. A second illness from the complications is always a harmless one (gut worms, muscle parasites), never flu, plague or malaria.
- One invoice: the Bill column, the offer letter and the silver you get always match, also when a patient leaves while carried or in a pod (the silver lands at the trade drop spot). Every treatment is billed, failed operations charge no fee and refund what they used.
- Harvesting a patient's organ is a violation again, as on any other guest: their faction is angry and nobody pays for it. Only the treatment the patient came for is exempt.
- Nobody gets stuck: dismissed patients leave, impossible operations end with a letter, there is a maximum stay, and only real danger holds cured patients.
- An operation the hospital's medicine level does not allow (for example herbal only, with no herbal medicine in stock) is no longer offered, and an open one says why it waits. New colonies treat patients with normal medicine or worse.
- Hospital policies belong to the save and come along after a gravship landing; a patient aboard keeps their stay on the new map.
- Arrested, enslaved or recruited patients keep their new status. A patient whose faction turns hostile is let go instead of being made a patient again.
- No more pile of vanilla "Disease: Infection" letters for patients: a short message says what a patient has caught.
- Rejected or declined patients are cleaned up properly instead of piling up as hidden world pawns.
- Hearth & Harbor: one bed button (H&H's) for every bed use, and patients who are up and about may use the spa and other leisure places.
- Hospital Beds Gen 2 beds count as patient and surgery beds by themselves.
- Removed two settings that did nothing (Soft compatibility, Zombie contracts).
- New hand-drawn textures for the landing spot, the tab icon and the Workshop preview.
- German: goodwill is "Ruf" as in the game, the hospital's renown is "Renommee", and the policy pages are "Regeln" everywhere.
- Safe to remove: "Prepare for removal" (Patient intake area, or the Diagnosis page) sends patients home, withdraws offers and hospital letters and removes the landing spots; the save then loads without RimWard with no error from it. RimWard's own state now lives in JB Framework's save data, so a new save names no class of RimWard any more; saves from RimWard 1.x and the 2.0 test builds load with everything intact.
- Built and tested on RimWorld 1.7. The 1.6 build is best effort and untested (the same source compiled against RimWorld 1.6; it was never run in a 1.6 game).
