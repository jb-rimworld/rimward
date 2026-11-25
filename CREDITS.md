# Credits

**RimWard – Hospital for Profit (Anesthesia Not Included)** is a GPL-3.0 fork. It stands on the shoulders of:

- **Adamas** — the original *Hospital* mod (`Adamas.Hospital`, GPL-3.0). RimWard reuses and adapts its
  patient lord/duty/bed/exit AI and the proven medical-tourism loop. Huge thanks.
- **assssssqwww** — *Hospital Fix And Tweak* (`assssssqwww.HospitalTweak`, GPL). Its fixes (surgery billing
  guards, mass-casualty handling, Combat Extended self-tend suppression, disease/severity balancing,
  dismissal logic) are folded into RimWard natively.

Ideas taken over in RimWard 2.0 (re-implemented in our own code, no code copied):

- From **Adamas**' later *Hospital* updates (2.7 to 2.9.x): spawn-safe generation of surgery cases, billing every medical
  operation instead of three worker classes, cleaning vanished patients out of the save (the "null key" fix), sizing mass casualty
  events on free beds, Combat Extended self-tend handling, recruited-patient cleanup.
- From **mgrTDziedzic**'s *Hospital (Fork)*: the deafness cure belongs on the ear, arrested patients leave the patient list without
  losing their prisoner status, the "GR" defName filter that blocked unrelated recipes, beds named "...Pod..." no longer treated as
  med pods, feeding billed only when the meal was actually fed.
- From **assssssqwww**'s *Hospital Fix And Tweak*: the surgery fee formula (work, required skill, risk) and "excise carcinoma pays".

RimWard keeps the parts that worked and rebuilds the rest (fair death penalties, surgery auto-retry,
the unified outcome score, triage, the consolidated tab, renown, soft compat, and more).

Soft integrations (no dependency; each one does nothing when the mod is absent): Combat Extended (CE Team; patients do not
self-tend), Hearth & Harbor and Hospital Beds Gen 2 (both by the same author).

Built with Harmony (pardeike) and JB Framework (settings window, compatibility page, test harness; same author).

## License and source
GPL-3.0 (see `LICENSE`). As a derivative of GPL-3.0 work, RimWard is and remains GPL-3.0.

- **Source:** the Workshop download contains the compiled mod only. The complete corresponding source of every release (C# code,
  build files, tests, art tools) is public at https://github.com/jb-rimworld/rimward, tagged with the version (for example
  `v2.0.0`); the repository is pushed before each Workshop upload.
- **Modified files:** every C# file carries an SPDX licence line and says whether it contains code from Hospital / Hospital Fix And
  Tweak (modified by juliusberger00 in 2026) or was written for RimWard 2.0. RimWard 1.x (2026) and 2.0 (24 November 2026) are the
  modified versions; `CHANGELOG.md` lists what changed.
