# Architecture decisions

Short records of the decisions that shape the code. Format: context → decision → consequence.

## ADR-01 EF Core for writes, Dapper for the dashboard report

* **Context:** writes are simple entity changes; the dashboard needs window sums, today's wellness, nutrition and the
  latest restriction for every athlete (NFR-03).
* **Decision:** EF Core 10 for writes and migrations; one hand-written T-SQL query via Dapper for the dashboard.
* **Consequence:** clear SQL with one round trip; the query is covered by an integration test against a real SQL Server (AC-12).

## ADR-02 Shared contracts project for validation

* **Context:** the Blazor form and the API must reject the same input (AC-07).
* **Decision:** request types with DataAnnotations live in `AthleteDay.Contracts`, used by both; the API uses .NET 10's
  built-in minimal API validation.
* **Consequence:** one place to change a rule; server-only checks (unknown athlete) come back as ValidationProblem.

## ADR-03 Demo personas instead of real sign-in

* **Context:** the demo must switch roles in one click; real identity is out of scope for v1.
* **Decision:** a custom authentication handler reads `X-Demo-User` / `X-Demo-Role`. Authorization uses normal policies.
* **Consequence:** replacing the handler with Entra ID (JWT bearer) changes no endpoint or policy.

## ADR-04 Queue-based import instead of synchronous processing

* **Context:** device files can be large and partly invalid; uploads should return immediately (AC-23).
* **Decision:** store the file in Blob Storage, publish `ImportFileUploaded` to a Storage Queue, process in a background
  worker. Locally Azurite; in Azure, Event Grid can feed the same queue.
* **Consequence:** at-least-once delivery, so the consumer must be idempotent (AC-30).

## ADR-05 Live updates carry no data

* **Context:** pushing dashboard rows over SignalR would bypass per-role authorization.
* **Decision:** the push says only "something changed" (AC-27); clients reload through the API with their own role.
* **Consequence:** one extra request per change, and no way for the push channel to leak health data.

## ADR-06 Load ratio as a monitoring aid

* **Context:** the acute:chronic workload ratio is debated in sports science.
* **Decision:** show acute and chronic load and a ratio band as reasons for a conversation, never as an injury risk score (AC-15).
* **Consequence:** wording in UI and docs avoids prediction claims.

## ADR-07 Archive athletes instead of deleting them

* **Context:** a coach must be able to remove an athlete from the squad (AC-35), but sessions, wellness checks, physio
  notes and measurements belong to the athlete's history, and a slip of the mouse during a session must be recoverable.
* **Decision:** `DELETE /api/athletes/{id}` sets `ArchivedAt`. An EF Core global query filter hides archived athletes from
  every query, so forms, imports and lookups treat them as unknown; the Dapper report filters them in its SQL. Restore
  and the archive list opt out of the filter explicitly.
* **Consequence:** no data is lost by accident and the UI can offer *Undo*. A real erasure request (right to be
  forgotten) would be a separate, audited admin action that deletes the history too; it is out of scope for v1.1.

## ADR-08 Birth year instead of birth date

* **Context:** the dashboard sorts by age (AC-38), and age matters for training and nutrition, but an exact birth date
  is more personal data than the team needs.
* **Decision:** store only the birth year and show the age reached this year. Sex is stored as Female, Male or Not
  stated. Both appear on the dashboard (coach, physio, nutrition) and never in the kitchen view.
* **Consequence:** data minimisation under the Swiss FADP / DSG; the age can be off by one before the birthday, which
  is precise enough for squad planning.

## ADR-09 Runtime translation catalogues, server messages by Accept-Language

* **Context:** both apps and the API must work in English and German (AC-53 – AC-57). The dashboard should switch
  language without a reload, and server texts (validation, readiness reasons, import errors) are shown in the UI.
* **Decision:**
  * **Angular:** a small signal-based translation service with one catalogue per language as TypeScript objects. The
    German catalogue is typed against the English one, so a missing key fails the build. Dates and numbers are
    formatted with `Intl` for the current locale (en-GB, de-CH). Angular's built-in `@angular/localize` was not used:
    it compiles one build per language, so switching means a reload and a second deployment.
  * **.NET:** `.resx` resources (English neutral, `.de` for German) in the project that owns the text: Contracts for
    validation messages (shared by API and Blazor), Core for readiness reasons and import errors, the API for its own
    errors, and the staff app for its UI. The API picks the culture from `Accept-Language` (request localization);
    both apps send their language.
  * **Blazor WebAssembly** loads culture data at start-up, so its language switch stores the choice and reloads the
    page (the documented .NET pattern).
* **Consequence:** one place per app to translate; a test compares the language catalogues (AC-54). Import row errors
  are stored as text in the language of the upload; a report viewed later in the other language stays as it was.

## ADR-10 Sports as a reference table

* **Context:** sports were free text on each athlete, so "Ice hockey", "Icehockey" and "ice hockey" could coexist and
  split the filters (AC-63 – AC-65).
* **Decision:** a `Sports` table (name, retired) managed by the admin; athletes hold a foreign key. DTOs keep the sport
  *name* for reading, so dashboards and the staff app stay simple; writes take the sport id. Retiring instead of
  deleting keeps existing athletes valid.
* **Consequence:** one rename changes every view at once. The migration turns the existing free-text values into list
  entries, so no athlete loses its sport.

## ADR-11 Dashboard range figures in the same SQL round trip

* **Context:** the date range (AC-58 – AC-59) adds totals and averages for a period, while week and month averages stay
  anchored on the end date.
* **Decision:** the Dapper report takes `@From` and `@To`; `@To` replaces "today" in the existing windows, and two more
  CTEs aggregate sessions and wellness checks over the range. Still one query (NFR-03).
* **Consequence:** looking at a past day shows the squad as it was then, with the data recorded so far; nothing is
  recalculated in the browser.

## ADR-12 Session notes in their own table, readable by coach and physio only

* **Context:** a session had one free-text `Notes` field, set when it was logged. Coaches want to add notes later,
  several per session, and know who wrote what (AC-72 – AC-74). Notes about training context are not health data, but
  they are not for the kitchen, nutrition or athletes either.
* **Decision:** a `SessionNotes` table (session, athlete, text, author, time). The existing `Notes` values move into it
  in the migration, and a note entered while logging becomes a `SessionNote` too. Reading needs a new `CoachingNotes`
  policy (coach and physio) and is audited; the sessions list itself never contains note text.
* **Consequence:** one place for notes, with history; the response shape of the sessions list stays free of note text
  for roles that may not read it (NFR-02).

## ADR-13 Progress reviews keep a snapshot of their figures

* **Context:** a review documents development over a period (AC-75). If its figures were recalculated on every read,
  a late import or a corrected session would silently change what the coach wrote about.
* **Decision:** when a review is saved, the API computes the period's figures (weekly load, sessions, wellness averages,
  tests against goals) with the same code as the progress and benchmark endpoints and stores them as JSON with the
  review. Reviews cannot be edited afterwards; a new review is written instead.
* **Consequence:** reviews read like signed documents and stay consistent with their text. The JSON is versioned by
  the DTO shape, so old snapshots must stay readable when the DTO grows (new fields optional).

## ADR-14 Training plans: a standard week plus whole-day changes

* **Context:** plans are weekly, but single days change (a match moved, a sore athlete, a camp). The coach must see at
  a glance which sessions come from the standard week and which were changed for that date (AC-78 – AC-80).
* **Decision:** a standard week per athlete (sessions per weekday) and, separately, *day changes*: for one date, the
  complete list of sessions that replaces the standard day (an empty list cancels it), with a reason. The week view
  merges both in `AthleteDay.Core` and marks each day as standard or changed. Undoing a change deletes it.
* **Consequence:** simple to explain and to show; a change never edits the standard week, and the standard week can be
  rewritten without losing the dated changes. A change replaces the whole day rather than single sessions, which keeps
  "what applies on this date" unambiguous.

## ADR-15 Documents in Blob Storage, metadata in SQL, served only by the API

* **Context:** athlete documents range from a training certificate to a medical report or a passport scan. They are
  files, not rows, and who may see them depends on the type (AC-85 – AC-90).
* **Decision:** the file goes to a private Blob container (`athlete-documents`) under a random name, behind an
  `IDocumentStore` interface (Azurite locally, in memory in tests, like the device import). SQL holds the metadata and
  the access rule works on the type: `DocumentAccess` in `AthleteDay.Core` decides which roles see which types. Every
  download goes through the API, which checks the type, writes the audit entry and streams the file; no SAS links are
  handed out. A document the role may not see answers 404, not 403, so its existence does not leak. File types are
  recognised by their first bytes and served as attachments with `nosniff`.
* **Consequence:** one place for the rule and one audit trail. Large downloads cost API bandwidth, which is fine at
  squad scale. Blob names carry no personal data. In production the container would add malware scanning (e.g.
  Defender for Storage) and soft delete; both are configuration, not code.

## ADR-16 Insights are rules with their figures, not generated text

* **Context:** coaches want hints ("sleep is dropping", "new personal best") without reading every chart. A language
  model could write them, but it would send health data to an outside service and could state things the data does
  not support (AC-97 – AC-99).
* **Decision:** a fixed set of rules in `AthleteDay.Core` (`InsightRules`), each with a documented threshold (spec
  section 4). An insight carries its kind, a tone and the figures that triggered it; the client turns that into a
  sentence in English or German. Versus mode follows the same idea: the ranking rules are data (direction per metric),
  not judgement.
* **Consequence:** every insight is explainable and unit-tested, no data leaves the system, and both languages say the
  same thing. New insights need code and a spec change; the rules are deliberately conservative to avoid alarm fatigue.
  A generated summary could be added later on top of these figures (see SPEC-DRIVEN-DEVELOPMENT.md), not instead of
  them.
