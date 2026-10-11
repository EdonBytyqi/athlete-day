import { Component, effect, inject, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../api/api.service';
import { AuditEntry } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';

@Component({
  selector: 'app-audit-page',
  template: `
    <header class="page-head">
      <div>
        <h1>{{ t('audit.title') }}</h1>
        <p class="muted">{{ t('audit.lead') }}</p>
      </div>
      <button type="button" (click)="load()">{{ t('audit.refresh') }}</button>
    </header>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    } @else {
      <div class="table-wrap">
        <table class="grid-table">
          <thead><tr><th>{{ t('audit.col.when') }}</th><th>{{ t('audit.col.user') }}</th><th>{{ t('audit.col.role') }}</th><th>{{ t('audit.col.action') }}</th><th>{{ t('audit.col.resource') }}</th><th class="num">{{ t('audit.col.athlete') }}</th></tr></thead>
          <tbody>
            @for (a of entries(); track $index) {
              <tr>
                <td class="nowrap">{{ i18n.dateTime(a.at) }}</td>
                <td>{{ a.user }}</td>
                <td>{{ t(roleLabel(a.role)) }}</td>
                <td>{{ t(actionLabel(a.action)) }}</td>
                <td>{{ a.resource }}</td>
                <td class="num">{{ a.athleteId ?? '–' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="muted">{{ t('audit.empty') }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class AuditPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  protected roleLabel(role: string): TranslationKey {
    return `roles.${role}` as TranslationKey;
  }

  protected actionLabel(action: string): TranslationKey {
    return `audit.action.${action}` as TranslationKey;
  }

  protected readonly entries = signal<AuditEntry[]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      this.persona.current();
      this.realtime.version();
      untracked(() => this.load());
    });
  }

  protected load(): void {
    this.api.audit().subscribe({
      next: (e) => {
        this.entries.set(e);
        this.error.set(null);
      },
      error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
    });
  }
}
