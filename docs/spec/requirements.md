# Athlete Day: Requirements Specification

| | |
|---|---|
| Version | 1.9 |
| Status | Current |
| Date | 2026-10-12 |

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
| Admin | Check who accessed health-related data; keep the athletes' identity documents |

## 3. Scope

**In scope:** session logging, wellness checks, load monitoring, readiness flags, nutrition targets, kitchen view,
physio notes, device CSV import, audit log, live dashboard updates, squad management (add, edit and archive athletes),
sorting and filtering the squad dashboard, athlete documents, English and German, a light layout that works on phones, tablets and
desktops.

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
| Language | English (default) or German. Formats: English as en-GB (11 Oct 2026, 1,234.5), German as de-CH (11.10.2026, 1’234.5; the grouping mark is ' or ’ depending on the browser's locale data). |
| Date range | From and to, both included, as dates in Europe/Zurich. To is today or earlier; at most 366 days. Average daily load over a range = total load ÷ days in the range (rest days count as 0). |
| Sport | One entry of the managed sports list (name, active or retired). Athletes refer to it; its name is shown wherever the athlete is. |
| Week | Monday to Sunday, in Europe/Zurich. |
| Trend (last 4 weeks vs the 4 before) | Load and sessions: up or down when they differ by more than 10 %, otherwise stable. Sleep, soreness and mood: up or down when the averages differ by more than 0.3, otherwise stable. Up is good for sleep and mood, bad for soreness; for load and sessions it is neither, only a change to talk about. |
| Device test direction | Higher is better for CMJ, SJ, GPS_DISTANCE and GPS_HSR; lower is better for SPRINT_30M. |
| Goal status | *Reached* when the latest result meets the target; *behind* when the goal date has passed without that; otherwise *open*. |
| Plan day | The standard week's sessions for that weekday, unless the coach changed the date: then exactly the changed sessions (none = cancelled day). Planned load = planned RPE × planned minutes. |
| Document type | *Medical* (reports, scans): physio only. *Government ID* (passport, ID card): admin only. *Certificate* (licence, anti-doping course) and *General*: coach and physio. A role sees, uploads, downloads and deletes exactly the types it may see. |
| Document file | PDF, JPEG or PNG, recognised by its first bytes (not by the name or the declared type), 1 byte to 10 MB. Stored in Blob Storage under a random name; the original file name is kept only as metadata. |

## 5. Functional requirements

Each acceptance criterion (AC) is verified by at least one automated test tagged with its ID. Levels: *Unit*, *Integration*
(real API and SQL Server), *Angular* (dashboard component tests) and *Staff* (Blazor component tests with bUnit).
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
| AC-32 | A coach can add an athlete with name, sport, birth year, sex and body mass; the response is 201 and the system assigns the next free code (section 4). Athletes added at the same time get different codes. | Integration |
| AC-33 | Name and sport are required (2–60 characters), the birth year must give an age between 10 and 70, body mass is 30–200 kg; an invalid request returns 400 with one error entry per invalid field. | Unit, Integration |
| AC-34 | A coach can edit an athlete's name, sport, birth year, sex and body mass; the code does not change. An unknown or archived athlete returns 404. | Integration |
| AC-35 | Deleting an athlete archives it: it disappears from the dashboard, the athlete list and the kitchen view, and new data for it is rejected; its history is kept, and a coach can list archived athletes and restore them. | Integration |
| AC-36 | Only the coach can add, edit, archive or restore athletes; every other role gets 403. Each of these changes is written to the audit log with the athlete's id, including the creation of a new athlete, and pushes a change signal. | Integration |
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

### F13 Wellness history

*As a coach or physio, I want to see an athlete's recent morning checks, so I notice a trend before it becomes a problem.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-43 | `GET /api/athletes/{athleteId}/wellness?days=n` returns the athlete's checks from the last n days including today (default 14), newest first, each with date, sleep, soreness, mood and flags. | Integration |
| AC-44 | A day's flags use the readiness thresholds of section 4 and the same wording as readiness reasons; a day within all thresholds has no flags. | Unit |
| AC-45 | `days` outside 1–28 is rejected with 400 and an error for `days`. | Integration |
| AC-46 | Only coach and physio may read the history; other roles get 403. | Integration |
| AC-47 | Every successful read is written to the audit log as resource `WellnessHistory` with the athlete. | Integration |
| AC-48 | An unknown athlete returns 404. | Integration |
| AC-49 | The athlete page shows the history as a table with flagged days highlighted, for coach and physio; other roles do not request it. | Angular |

### F14 Athlete sign-in in the staff app (demo)

*As an athlete, I want to pick my own name in the staff app, so I can send my morning check, whoever I am in the squad.*

Demo only: in production the athlete signs in with Entra ID and the identity already says who they are (ADR-03).

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-50 | In the staff app, choosing the athlete role shows a second list with the current squad (archived athletes excluded, athletes added later included); picking an athlete signs in as that athlete, so their code is the identity sent to the API. | Staff |
| AC-51 | The morning check greets the signed-in athlete by name and submits for that athlete; picking another athlete switches the form to them. Without a chosen athlete, the form asks to choose one and cannot be sent. | Staff |
| AC-52 | Every athlete in the squad, including one added after start-up, can submit their own morning check, and it appears on the dashboard. | Integration |

### F15 English and German

*As a member of a Swiss team, I want to use the app in English or German, so everyone works in the language they read
best.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-53 | The dashboard has a language switch (English, German). Switching changes all visible text at once, without reloading; the choice is remembered, and the link to the staff app opens it in the same language. | Angular |
| AC-54 | All interface text comes from one translation catalogue per app (and one per .NET project for server messages). A test fails if a text exists in one language but not in the other. | Angular, Unit |
| AC-55 | Dates and numbers follow the language (section 4): English as 11 Oct 2026 and 1,234.5, German as 11.10.2026 and 1’234.5. | Angular |
| AC-56 | The staff app has the same language switch; its labels, buttons, choices and validation messages appear in the chosen language. | Staff |
| AC-57 | The API returns validation and error messages, readiness reasons and import row errors in the language of the request's `Accept-Language` header (German for `de`, `de-CH` …; English otherwise). Both apps send their current language. | Integration, Angular |

### F16 Date range on the dashboard

*As a coach, I want to look at a specific day or period, so I can review last week or prepare a conversation about it.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-58 | The dashboard takes an optional date range (`from`, `to`; both default to today). `to` may not be in the future, `from` may not be after `to`, and a range spans at most 366 days; otherwise the API answers 400 with an error for the field. | Integration |
| AC-59 | For the range, each row adds the total load, the average daily load (rest days count as 0), the number of sessions, and the average sleep, soreness and mood over the days with a morning check. Load of the day, week and month averages, ratio, readiness, morning check, nutrition and restriction are those of the end date. | Unit, Integration |
| AC-60 | The dashboard has a date range control with presets (today, last 7 days, last 28 days) and custom start and end dates. Changing it reloads the data for that range, and the page says which day or period the figures refer to. | Angular |

### F17 Comparing week and month load

*As a coach, I want to rank the squad by week average, month average or the week-vs-month ratio, so outliers stand out.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-61 | The squad table also sorts by week average, month average and week vs month; athletes without a ratio come last in both directions. | Angular |
| AC-62 | A comparison panel shows every athlete as a bar for one metric at a time (week average, month average, week vs month), highest first. Switching the metric is one click. For week vs month, the usual range 0.8–1.3 is marked and values outside it are highlighted. | Angular |

### F18 Managed list of sports

*As an administrator, I want to control the list of sports, so athlete profiles, filters and reports use the same names.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-63 | The admin can add, rename, retire and reactivate sports. Names are 2–60 characters and unique (ignoring case). Every other role gets 403 for these changes; every role that sees athletes can read the list. | Integration |
| AC-64 | An athlete's sport is chosen from the list: adding or editing takes a sport id, and an unknown or retired sport is rejected (an athlete may keep a sport that was retired later). Renaming a sport renames it everywhere athletes are shown. | Integration |
| AC-65 | The athlete form offers the active sports in a dropdown (plus the athlete's current sport if it was retired). The admin has a Settings screen with the list. | Angular |

### F19 Athlete space

*As a coach, I want one place per athlete with everything about their development, and to switch athletes without losing my place.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-66 | The athlete page is a space with the sections Overview, Performance, Sessions and notes, and Reviews. A searchable athlete selector, grouped by sport, switches the whole space to another athlete and keeps the open section. | Angular |
| AC-67 | `GET /api/athletes/{id}/progress?weeks=n` (4–52, default 12) returns one entry per week (Monday to Sunday) ending with the current week, each with total load, sessions and average sleep, soreness and mood. It also compares the last 4 weeks with the 4 before for load, sessions, sleep, soreness and mood as up, down or stable (section 4). Coach, physio and nutrition only; reads are audited. | Unit, Integration |
| AC-68 | The Overview shows key figures with an arrow for up, down or stable against the 4 weeks before, says in words whether a change is good or needs attention, and charts the weekly load with average sleep. The period can be 4, 12 or 26 weeks. | Angular |

### F20 Benchmarks and goals

*As a coach, I want to see each athlete's test results against their goals, their own history and the squad, so I know where they stand.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-69 | `GET /api/athletes/{id}/benchmarks?weeks=n` returns, per device test the athlete has done: the latest result, the personal best, the first result in the period and the change since, the squad average for the same sport (latest result per athlete), the goal and the gap to it, and a status: *reached*, *behind* (goal date passed) or *open*. Better means higher for jumps and distances and lower for sprint times. | Unit, Integration |
| AC-70 | The coach sets one goal per test and athlete (target value within the test's plausible range, optional date and note) and can remove it. Other roles get 403. | Integration |
| AC-71 | The Performance section charts each test's history with the goal line and the squad average, and states the gap in words (e.g. "4.2 cm to go"). The coach can set or remove goals there. | Angular |

### F21 Session notes

*As a coach, I want to add notes to sessions and read them later, so I remember the context behind a change.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-72 | The coach adds notes to a session (1–1000 characters); each note keeps its author, time, session date and athlete. A note entered while logging a session is stored the same way. Other roles get 403 for adding. | Integration |
| AC-73 | Coach and physio read an athlete's session notes, newest first, with the session's date, type and load. Other roles get 403, and every read is audited. | Integration |
| AC-74 | The Sessions and notes section lists recent sessions with their notes, and the coach can add a note to any of them. | Angular |

### F22 Progress reviews

*As a coach, I want to record structured reviews of an athlete's development and keep them, so long-term progress is documented.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-75 | The coach records a review for a period with observations, achievements, challenges and recommendations (at least one filled, each up to 2000 characters). The API stores a snapshot of the period's figures with it: load per week, sessions, wellness averages and test results against goals. The snapshot does not change when later data arrives. | Integration |
| AC-76 | Coach and physio read an athlete's reviews, newest first. Other roles get 403, and reads are audited. | Integration |
| AC-77 | The Reviews section lists past reviews with their snapshot figures, and the coach can write a new one for the last 4, 12 or 26 weeks. | Angular |

### F23 Weekly training plans

*As a coach, I want a standard week per athlete that I can change for single days, so the plan stays clear and still
fits what happens.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-78 | Each athlete has a standard week: per weekday up to 3 planned sessions (type, 5–300 minutes, RPE 1–10, optional note up to 200 characters). The coach replaces it as a whole; other roles get 403; invalid sessions return 400. Changing one athlete's plan leaves every other athlete's plan unchanged. | Unit, Integration |
| AC-79 | The week plan (Monday to Sunday, default the current week) shows for each day either the standard week's sessions or, if the coach changed that date, the changed sessions. Each day says whether it is *standard* or *changed* (with the reason), and every session shows its planned load (RPE × minutes). | Unit, Integration |
| AC-80 | The coach changes a single date: other sessions, an added session, or none at all (a cancelled day, with a reason). The change can be undone, which brings back the standard week for that date. The standard week itself is not touched. | Integration |
| AC-81 | The week plan compares planned and logged load per day and for the whole week. | Unit, Integration |
| AC-82 | Coach, physio and nutrition read every athlete's plan; an athlete reads only their own; the kitchen gets 403. | Integration |
| AC-83 | The athlete space has a Plan section: a week view with previous and next week, changed days clearly marked, planned against logged load. The coach edits the standard week and changes or resets single days there. | Angular |
| AC-84 | In the staff app, a signed-in athlete sees their own week plan, read-only, with changed days marked. | Staff |

### F24 Athlete documents

*As a member of the staff, I want to keep an athlete's documents in one safe place, so the right people find them and
nobody else sees them.*

| ID | Acceptance criterion | Verified by |
|---|---|---|
| AC-85 | Coach, physio and admin upload a document for an athlete: a file, a type and a title (1–100 characters). The file goes to Blob Storage under a random name; the database keeps the metadata (type, title, original file name, content type, size, SHA-256, who uploaded it and when). The answer is 201 with the metadata. | Integration |
| AC-86 | Need-to-know per document type (section 4): uploading a type the role may not see returns 403 and stores nothing. Nutrition, kitchen and athletes get 403 on every document endpoint. | Unit, Integration |
| AC-87 | An athlete's document list contains only the types the role may see, newest first. Downloading or deleting a document of another type returns 404, so its existence is not revealed. | Integration |
| AC-88 | An empty file, a file over 10 MB, a file that is not PDF, JPEG or PNG by its first bytes (whatever its name or declared type), a missing or too long title, or an unknown type returns 400 and stores nothing. | Unit, Integration |
| AC-89 | A download returns exactly the stored bytes, with the stored content type, as an attachment under the original file name, and with `X-Content-Type-Options: nosniff`. Files are only ever served through the API, never by a direct storage link. | Integration |
| AC-90 | Deleting a document removes its metadata and its file. Listing, downloading, uploading and deleting are each written to the audit log with the athlete's id. | Integration |
| AC-91 | The athlete space has a Documents section for coach and physio: the list (type, title, file name, size, date, uploader), an upload form that offers only the types the role may upload, download, and delete after a confirmation. Other roles see a short notice instead. | Angular |
| AC-92 | The admin keeps identity documents on the Settings page: choose an athlete, then list, upload, download and delete their government IDs. For this the admin may read the athlete list (names and codes only). | Angular, Integration |

## 6. Non-functional requirements

| ID | Requirement |
|---|---|
| NFR-01 | Synthetic data only: invented names, pseudonymous codes, no real persons or organisations. |
| NFR-02 | Privacy by design: response shapes for a role contain no fields for data that role must not see (Swiss FADP / DSG). |
| NFR-03 | The dashboard needs one database round trip for all athletes. |
| NFR-04 | The system runs with one command (`docker compose up`) and without Docker on Windows (LocalDB + Azurite). |
| NFR-05 | Every push runs build, all tests, the traceability check and the Docker build in CI. |
| NFR-06 | Responsive: usable from 360 px wide to desktop. No horizontal page scrolling; navigation fits or folds into a menu; wide tables scroll inside their card or become cards; touch targets are at least 44 × 44 px. Checked with screenshots at 375, 768 and 1440 px for every UI change. |
| NFR-07 | One light design for both apps: white background, light-blue accent, shared design tokens. Text and controls meet WCAG AA contrast (4.5:1 for text). No dark mode. |

## 7. Change log

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-10-10 | Baseline written after the first implementation; all ACs linked to tests. |
| 1.1 | 2026-10-11 | F11 squad management (AC-32–AC-37) and F12 dashboard usability (AC-38–AC-42); age, sex, code and archive rules in section 4. |
| 1.2 | 2026-10-11 | AC-32: concurrent adds get different codes. AC-36: the audit entry for a new athlete carries its id. Both found in review. |
| 1.3 | 2026-10-11 | F13 wellness history (AC-43–AC-49); contract: `GET /api/athletes/{athleteId}/wellness`, schema `WellnessDayDto`. Written 2026-10-10 as v1.1 on a parallel branch and renumbered when rebased onto v1.2. |
| 1.4 | 2026-10-11 | F14 athlete sign-in in the staff app (AC-50–AC-52). Adds the verification level *Staff* (bUnit component tests). |
| 1.5 | 2026-10-11 | F15 English and German (AC-53–AC-57); NFR-06 responsive layout and NFR-07 light design; language rule in section 4. Phase 1 of the "enhance the hub" brief. |
| 1.6 | 2026-10-11 | F16 date range (AC-58–AC-60), F17 week and month comparison (AC-61–AC-62), F18 managed sports list (AC-63–AC-65); date range and sport rules in section 4. Phase 2. |
| 1.7 | 2026-10-11 | F19 athlete space (AC-66–AC-68), F20 benchmarks and goals (AC-69–AC-71), F21 session notes (AC-72–AC-74), F22 progress reviews (AC-75–AC-77); week, trend, test direction and goal status rules in section 4. Phase 3. |
| 1.8 | 2026-10-11 | F23 weekly training plans (AC-78–AC-84); plan day rule in section 4. Phase 4. |
| 1.9 | 2026-10-12 | F24 athlete documents (AC-85–AC-92); document type and file rules in section 4; admin keeps identity documents. Phase 5. |
