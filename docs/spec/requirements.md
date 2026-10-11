# Athlete Day: Requirements Specification

| | |
|---|---|
| Version | 1.1 |
| Status | Current |
| Date | 2026-10-11 |

> **How this version came about.** v1.0 was written *after* the first implementation, to capture what the system does and
> to link every acceptance criterion to a test. From v1.1 on, every change starts here: spec first, then failing tests,
> then code. See [README.md](README.md) for the process.

## 1. Purpose

An interdisciplinary team (athletic coaches, physios, nutrition scientists, kitchen) works on the same athletes.
Athlete Day records one athlete's day across these disciplines and shows each role **only what it needs**.

## 2. Roles

| Role | Goal |
|---|---|
| Coach | Plan training from load and readiness; log sessions; import device data; manage the squad |
| Athlete | Report a morning wellness check quickly |
| Nutrition | Set daily energy and carbohydrate targets |
| Physio | Keep health notes and training restrictions |
| Kitchen | Prepare the right portion per athlete |
| Admin | Check who accessed health-related data |

## 3. Scope

**In scope:** session logging, wellness checks, load monitoring, readiness flags, nutrition targets, kitchen view,
physio notes, device CSV import, audit log, live dashboard updates, squad management (add, edit and archive athletes),
sorting and filtering the squad dashboard.

**Out of scope (v1):** real sign-in (demo personas only), Azure deployment, notifications, athlete-facing dashboard,
medical advice of any kind.

## 4. Domain rules

| Term | Definition |
|---|---|
| Session load | RPE (Borg CR-10, 1–10) × duration in minutes, in arbitrary units (AU) |
| Daily load | Sum of all session loads on one day |
| Acute load | Average daily load over the last 7 days including today; days without training count as 0 |
| Chronic load | Average daily load over the last 28 days including today; days without training count as 0 |
| Ratio | Acute ÷ chronic, rounded to 2 decimals; undefined when chronic load is 0 |
| Today | The current date in Europe/Zurich, regardless of server location |
| Readiness thresholds | Sleep < 6 h · soreness ≥ 4/5 · mood ≤ 2/5 · ratio > 1.3 or < 0.8 · any physio restriction |
| Portion | Energy target < 2200 kcal → S · < 3000 → M · < 3800 → L · otherwise XL |
| Carbohydrates (g) | Target g/kg × body mass, rounded half away from zero |
| Age | Current year (Europe/Zurich) − birth year, i.e. the age reached this year. Only the birth year is stored. |
| Sex | Female, Male or Not stated |
| Athlete code | `ATH-` plus a three-digit number, assigned by the system (highest existing number + 1) and never changed |
| Archived athlete | Hidden from every list and form and accepts no new data; its history is kept and it can be restored |

## 5. Functional requirements

Each acceptance criterion (AC) is verified by at least one automated test tagged with its ID.
`dotnet test --filter Category=Spec` fails if an AC has no test or a test names an unknown AC.

### F1 Access and roles

*As a team, we want each discipline to see only what it needs, so health data stays need-to-know.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-01 | A request without a persona identity is rejected with 401. | Integration |
| AC-02 | A role calling an endpoint outside its need is rejected with 403 (kitchen → dashboard and athlete list; coach and nutrition → physio notes; coach → audit log). | Integration |
| AC-03 | The dashboard shows only the screens the current role may open. | Angular |
| AC-04 | The dashboard sends the persona identity with API calls, and only with API calls. | Angular |
| AC-28 | When the API answers 403, the dashboard explains it as need-to-know access instead of showing a failure. | Angular |

### F2 Session logging (coach)

*As a coach, I want to log a session in seconds, so the load is always up to date.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-05 | A coach can log a session (athlete, date, type, minutes, RPE); the response is 201 and contains load = RPE × minutes. | Unit, Integration |
| AC-06 | Load is only computed for an RPE between 1 and 10. | Unit |
| AC-07 | An invalid request returns 400 with one error entry per invalid field; the same rules run in the browser. | Unit, Integration |
| AC-08 | A session dated more than one day in the future is rejected. | Unit |

### F3 Wellness check (athlete)

*As an athlete, I want to report sleep, soreness and mood, so my coach can adapt today's plan.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-09 | An athlete can submit a check for themselves only; for anyone else the API answers 403. | Integration |
| AC-10 | Sleep < 6 h, soreness ≥ 4 or mood ≤ 2 raises a *check-in* flag with the reason in words. | Unit, Integration |

### F4 Load monitoring

*As a coach, I want to compare this week's load with the last four weeks, so I notice sudden changes.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-11 | Rolling averages count days without training as 0 and ignore days outside the window. | Unit |
| AC-12 | The dashboard report returns today's load, acute and chronic load per athlete using exactly the 7- and 28-day windows. | Unit, Integration |
| AC-13 | The ratio is undefined without chronic load; a ratio above 1.3 or below 0.8 is flagged, the band edges are not. | Unit |
| AC-14 | The trend has one point per day ending today; several sessions on one day are summed. | Unit |
| AC-15 | The dashboard labels the 7/28-day comparison as a monitoring aid, not an injury predictor. | Angular |
| AC-16 | The trend chart scales bars and lines to a readable axis maximum and copes with an empty series. | Angular |

### F5 Readiness

*As a coach, I want flags with reasons instead of a single score, so I know what to ask the athlete.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-17 | No reasons → *Ready*; only a missing check → *No check-in*; a missing check plus another reason → *Check in*. | Unit |
| AC-18 | An active physio restriction is always listed as a reason. | Unit |

### F6 Nutrition and kitchen

*As the kitchen, I want each athlete's portion and carbohydrates, without seeing anything about their health.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-19 | Carbohydrates are shown in whole grams; the portion size follows the energy target (section 4). | Unit |
| AC-20 | The kitchen view contains exactly: athlete code, name, energy, carbohydrates, portion, kitchen note, and no health data. | Integration |

### F7 Physio notes and audit

*As a physio, I want private notes; as data protection, I want to know who looked at health data.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-21 | A physio note requires text. | Unit |
| AC-22 | Every successful view of physio notes is written to the audit log with user, role, action, resource and athlete. | Integration |

### F8 Device import

*As a coach, I want to upload a device export and see exactly which rows were rejected and why.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-23 | An upload is accepted with 202, processed asynchronously via storage and a queue; valid rows are imported and invalid rows reported with row number, column and reason. | Unit, Integration |
| AC-24 | Row rules: known athlete code, date `yyyy-MM-dd` not in the future, supported test type, number with `.` decimal separator within the plausible range, matching unit, no duplicates; `;` and `,` delimiters and quoted cells are accepted. | Unit |
| AC-25 | A wrong header or an empty file fails the whole file; blank lines and Windows line endings are ignored. | Unit |
| AC-26 | Files that are not `.csv` are rejected with 400. | Integration |
| AC-30 | An import event delivered twice is processed once (idempotent consumer). | Integration |

### F9 Live updates

*As a coach, I want the dashboard to update when staff enter data, without reloading.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-27 | After a write, the API pushes a change signal to connected clients that contains only a reason and a timestamp, no athlete data. | Integration |
| AC-31 | The dashboard reloads its data through the API when a change signal arrives. | Angular |

### F10 API contract

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-29 | The API's published OpenAPI document matches [openapi.json](openapi.json). An API change starts by changing that file. | Integration |

### F11 Squad management (coach)

*As a coach, I want to add, edit and archive athletes myself, so the squad on screen matches the squad in the hall.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-32 | A coach can add an athlete with name, sport, birth year, sex and body mass; the response is 201 and the system assigns the next free code (section 4). | Integration |
| AC-33 | Name and sport are required (2–60 characters), the birth year must give an age between 10 and 70, body mass is 30–200 kg; an invalid request returns 400 with one error entry per invalid field. | Unit, Integration |
| AC-34 | A coach can edit an athlete's name, sport, birth year, sex and body mass; the code does not change. An unknown or archived athlete returns 404. | Integration |
| AC-35 | Deleting an athlete archives it: it disappears from the dashboard, the athlete list and the kitchen view, and new data for it is rejected; its history is kept, and a coach can list archived athletes and restore them. | Integration |
| AC-36 | Only the coach can add, edit, archive or restore athletes; every other role gets 403. Each of these changes is written to the audit log and pushes a change signal. | Integration |
| AC-37 | Each dashboard row contains the athlete's age and sex; the kitchen view does not change (AC-20). | Integration |

### F12 Squad dashboard usability

*As anyone on the staff, I want to find an athlete and understand the numbers without asking someone.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-38 | The dashboard sorts by name, sport, age, sex, today's load or readiness when a column header is clicked; a second click reverses the order, and the sorted column is marked with `aria-sort`. | Angular |
| AC-39 | A search box filters by name or code and sport chips filter by sport; when nothing matches, the page says so and offers to clear the filters. | Angular |
| AC-40 | Column headers use plain language, and every load column explains its meaning in a tooltip that also opens with the keyboard. | Angular |
| AC-41 | The coach sees *Add athlete* and actions per row (log session, details, edit, archive); other roles see none of them. Archiving asks for confirmation and ends with a message that offers *Undo*. | Angular |
| AC-42 | *Log session* opens the staff input form with the athlete already selected. | Angular |

## 6. Non-functional requirements

| ID | Requirement |
|---|---|
| NFR-01 | Synthetic data only: invented names, pseudonymous codes, no real persons or organisations. |
| NFR-02 | Privacy by design: response shapes for a role contain no fields for data that role must not see (Swiss FADP / DSG). |
| NFR-03 | The dashboard needs one database round trip for all athletes. |
| NFR-04 | The system runs with one command (`docker compose up`) and without Docker on Windows (LocalDB + Azurite). |
| NFR-05 | Every push runs build, all tests, the traceability check and the Docker build in CI. |

## 7. Change log

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-10-10 | Baseline written after the first implementation; all ACs linked to tests. |
| 1.1 | 2026-10-11 | F11 squad management (AC-32–AC-37) and F12 dashboard usability (AC-38–AC-42); age, sex, code and archive rules in section 4. |
