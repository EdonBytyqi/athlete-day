import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService, describeError } from '../api/api.service';
import { AuditEntry } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';

@Component({
  selector: 'app-audit-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  template: `
    <header class="page-head">
      <div>
        <h1>Audit log</h1>
        <p class="muted">Who viewed or changed health-related data, and when.</p>
      </div>
      <button type="button" (click)="load()">Refresh</button>
    </header>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    } @else {
      <div class="table-wrap">
        <table class="grid-table">
          <thead><tr><th>When</th><th>User</th><th>Role</th><th>Action</th><th>Resource</th><th class="num">Athlete</th></tr></thead>
          <tbody>
            @for (a of entries(); track $index) {
              <tr>
                <td>{{ a.at | date: 'dd.MM. HH:mm:ss' }}</td>
                <td>{{ a.user }}</td>
                <td>{{ a.role }}</td>
                <td>{{ a.action }}</td>
                <td>{{ a.resource }}</td>
                <td class="num">{{ a.athleteId ?? '–' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="muted">Nothing logged yet.</td></tr>
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
      error: (e) => this.error.set(describeError(e, this.persona.current().role)),
    });
  }
}
