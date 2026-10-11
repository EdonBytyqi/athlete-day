import { Component, effect, inject, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService, describeError } from '../api/api.service';
import { ImportJob } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';

@Component({
  selector: 'app-imports-page',
  imports: [DatePipe],
  template: `
    <header class="page-head">
      <div>
        <h1>Device import</h1>
        <p class="muted">
          Upload a CSV export (jump tests, GPS). The file goes to Blob Storage, an event is queued and a background
          worker validates it. Good rows are imported; bad rows come back with the reason.
        </p>
      </div>
    </header>

    <section class="panel upload">
      <input #picker type="file" accept=".csv" (change)="selected.set(picker.files?.item(0) ?? null)" />
      <button type="button" [disabled]="!selected() || uploading()" (click)="upload(picker)">
        {{ uploading() ? 'Uploading…' : 'Upload' }}
      </button>
      <span class="muted">Columns: athlete_code, date, test_type, value, unit. Samples are in the repo's samples/ folder.</span>
    </section>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }

    <div class="two-col wide-right">
      <section class="panel">
        <h2>Recent imports</h2>
        <ul class="job-list">
          @for (j of jobs(); track j.id) {
            <li>
              <button type="button" class="job" [class.active]="report()?.id === j.id" (click)="open(j.id)">
                <span class="status" [class]="'status ' + j.status.toLowerCase()">{{ j.status }}</span>
                <strong>{{ j.fileName }}</strong>
                <span class="muted">{{ j.importedRows }} imported · {{ j.rejectedRows }} rejected · {{ j.uploadedAt | date: 'HH:mm' }}</span>
              </button>
            </li>
          } @empty {
            <li class="muted">No imports yet.</li>
          }
        </ul>
      </section>

      <section class="panel">
        <h2>Validation report</h2>
        @if (report(); as r) {
          <p>
            <strong>{{ r.fileName }}</strong> by {{ r.uploadedBy }}: {{ r.totalRows }} rows,
            {{ r.importedRows }} imported, {{ r.rejectedRows }} rejected.
          </p>
          @if (r.failureReason) {
            <p class="banner error">{{ r.failureReason }}</p>
          }
          @if (r.errors.length) {
            <table class="grid-table compact">
              <thead><tr><th class="num">Row</th><th>Column</th><th>Reason</th><th>Line</th></tr></thead>
              <tbody>
                @for (e of r.errors; track e.rowNumber) {
                  <tr>
                    <td class="num">{{ e.rowNumber }}</td>
                    <td><code>{{ e.column }}</code></td>
                    <td>{{ e.reason }}</td>
                    <td><code class="raw">{{ e.rawLine }}</code></td>
                  </tr>
                }
              </tbody>
            </table>
          } @else if (r.status === 'Completed') {
            <p class="muted">Every row passed validation.</p>
          } @else {
            <p class="muted">Waiting for the worker…</p>
          }
        } @else {
          <p class="muted">Select an import to see its report.</p>
        }
      </section>
    </div>
  `,
})
export class ImportsPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);

  protected readonly jobs = signal<ImportJob[]>([]);
  protected readonly report = signal<ImportJob | null>(null);
  protected readonly selected = signal<File | null>(null);
  protected readonly uploading = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // The worker pushes "import" when it finishes, which refreshes the list and the open report.
    effect(() => {
      this.persona.current();
      this.realtime.version();
      untracked(() => {
        this.refresh();
        const open = this.report();
        if (open) this.open(open.id);
      });
    });
  }

  protected upload(picker: HTMLInputElement): void {
    const file = this.selected();
    if (!file) return;
    this.uploading.set(true);
    this.api.upload(file).subscribe({
      next: (job) => {
        this.uploading.set(false);
        this.selected.set(null);
        picker.value = '';
        this.error.set(null);
        this.report.set(job);
        this.refresh();
      },
      error: (e) => {
        this.uploading.set(false);
        this.error.set(describeError(e, this.persona.current().role));
      },
    });
  }

  protected open(id: string): void {
    this.api.importJob(id).subscribe({ next: (job) => this.report.set(job) });
  }

  private refresh(): void {
    this.api.imports().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.error.set(null);
        if (!this.report() && jobs.length) this.open(jobs[0].id);
      },
      error: (e) => {
        this.jobs.set([]);
        this.report.set(null);
        this.error.set(describeError(e, this.persona.current().role));
      },
    });
  }
}
