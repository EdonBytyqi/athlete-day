import { Component, effect, inject, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../api/api.service';
import { ImportJob } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';

@Component({
  selector: 'app-imports-page',
  template: `
    <header class="page-head">
      <div>
        <h1>{{ t('imports.title') }}</h1>
        <p class="muted">{{ t('imports.lead') }}</p>
      </div>
    </header>

    <section class="panel upload">
      <input #picker type="file" accept=".csv" [attr.aria-label]="t('imports.fileLabel')" (change)="selected.set(picker.files?.item(0) ?? null)" />
      <button type="button" [disabled]="!selected() || uploading()" (click)="upload(picker)">
        {{ uploading() ? t('imports.uploading') : t('imports.upload') }}
      </button>
      <span class="muted">{{ t('imports.columns') }}</span>
    </section>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }

    <div class="two-col wide-right">
      <section class="panel">
        <h2>{{ t('imports.recent') }}</h2>
        <ul class="job-list">
          @for (j of jobs(); track j.id) {
            <li>
              <button type="button" class="job" [class.active]="report()?.id === j.id" (click)="open(j.id)">
                <span class="status" [class]="'status ' + j.status.toLowerCase()">{{ t(statusLabel(j.status)) }}</span>
                <strong>{{ j.fileName }}</strong>
                <span class="muted">{{ t('imports.jobLine', { imported: j.importedRows, rejected: j.rejectedRows, time: i18n.dateTime(j.uploadedAt, { hour: '2-digit', minute: '2-digit' }) }) }}</span>
              </button>
            </li>
          } @empty {
            <li class="muted">{{ t('imports.none') }}</li>
          }
        </ul>
      </section>

      <section class="panel">
        <h2>{{ t('imports.report') }}</h2>
        @if (report(); as r) {
          <p>{{ t('imports.summary', { file: r.fileName, user: r.uploadedBy, total: r.totalRows, imported: r.importedRows, rejected: r.rejectedRows }) }}</p>
          @if (r.failureReason) {
            <p class="banner error">{{ r.failureReason }}</p>
          }
          @if (r.errors.length) {
            <table class="grid-table compact">
              <thead><tr><th class="num">{{ t('imports.col.row') }}</th><th>{{ t('imports.col.column') }}</th><th>{{ t('imports.col.reason') }}</th><th>{{ t('imports.col.line') }}</th></tr></thead>
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
            <p class="muted">{{ t('imports.allPassed') }}</p>
          } @else {
            <p class="muted">{{ t('imports.waiting') }}</p>
          }
        } @else {
          <p class="muted">{{ t('imports.selectOne') }}</p>
        }
      </section>
    </div>
  `,
})
export class ImportsPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  protected statusLabel(status: ImportJob['status']): TranslationKey {
    return `imports.status.${status}`;
  }

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
        this.error.set(describeError(e, this.persona.current().role, this.t));
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
        this.error.set(describeError(e, this.persona.current().role, this.t));
      },
    });
  }
}
