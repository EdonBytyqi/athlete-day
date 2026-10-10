import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService, describeError } from '../api/api.service';
import { DashboardRow } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';
import { ReadinessBadge } from '../shared/readiness-badge';

@Component({
  selector: 'app-dashboard-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DecimalPipe, ReadinessBadge],
  template: `
    <header class="page-head">
      <div>
        <h1>Today's squad</h1>
        <p class="muted">Load, readiness and nutrition per athlete. Updates live when staff log data.</p>
      </div>
      @if (rows(); as list) {
        <div class="stats">
          <div class="stat"><strong>{{ list.length }}</strong><span>athletes</span></div>
          <div class="stat warn"><strong>{{ checkIns() }}</strong><span>to check in</span></div>
          <div class="stat"><strong>{{ missing() }}</strong><span>no morning check</span></div>
        </div>
      }
    </header>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    } @else if (rows(); as list) {
      <div class="table-wrap">
        <table class="grid-table">
          <thead>
            <tr>
              <th>Athlete</th>
              <th class="num">Today</th>
              <th class="num">7-day avg</th>
              <th class="num">28-day avg</th>
              <th class="num">Ratio</th>
              <th>Readiness</th>
              <th>Morning check</th>
              <th>Nutrition today</th>
            </tr>
          </thead>
          <tbody>
            @for (r of list; track r.athleteId) {
              <tr [class.flagged]="r.readiness.status === 'CheckIn'">
                <td>
                  <a [routerLink]="['/athletes', r.athleteId]" class="athlete">
                    <strong>{{ r.displayName }}</strong>
                    <span class="muted">{{ r.code }} · {{ r.sport }}</span>
                  </a>
                  @if (r.restriction !== 'None') {
                    <span class="tag warn">Restriction: {{ r.restriction }}</span>
                  }
                </td>
                <td class="num">{{ r.todayLoad }}</td>
                <td class="num">{{ r.acuteLoad | number: '1.0-0' }}</td>
                <td class="num">{{ r.chronicLoad | number: '1.0-0' }}</td>
                <td class="num">{{ r.acuteChronicRatio === null ? '–' : (r.acuteChronicRatio | number: '1.2-2') }}</td>
                <td><app-readiness-badge [status]="r.readiness.status" [reasons]="r.readiness.reasons" /></td>
                <td>
                  @if (r.wellness; as w) {
                    <span class="muted">Sleep {{ w.sleepHours }} h · Soreness {{ w.soreness }}/5 · Mood {{ w.mood }}/5</span>
                  } @else {
                    <span class="muted">–</span>
                  }
                </td>
                <td>
                  @if (r.nutrition; as n) {
                    {{ n.energyKcal }} kcal · {{ n.carbsG }} g CHO
                  } @else {
                    <span class="muted">not set</span>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <p class="footnote">
        Load = session RPE × minutes (AU). The 7-day vs 28-day comparison is a monitoring aid to start a
        conversation, not an injury predictor; the acute:chronic workload ratio is debated in sports science.
      </p>
    } @else {
      <p class="muted">Loading…</p>
    }
  `,
})
export class DashboardPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);

  protected readonly rows = signal<DashboardRow[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly checkIns = computed(() => this.rows()?.filter((r) => r.readiness.status === 'CheckIn').length ?? 0);
  protected readonly missing = computed(() => this.rows()?.filter((r) => r.wellness === null).length ?? 0);

  constructor() {
    // Reload when the role changes or the API pushes a change.
    effect(() => {
      const role = this.persona.current().role;
      this.realtime.version();
      untracked(() => this.load(role));
    });
  }

  private load(role: string): void {
    this.api.dashboard().subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.error.set(null);
      },
      error: (e) => {
        this.rows.set(null);
        this.error.set(describeError(e, role));
      },
    });
  }
}
