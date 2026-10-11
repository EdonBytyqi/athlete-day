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
