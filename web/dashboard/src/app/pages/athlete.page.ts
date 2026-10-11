import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService, describeError } from '../api/api.service';
import { AthleteDetails, LoadTrend, Measurement, PhysioNote, Sex, TrainingSession } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';
import { logSessionUrl } from '../shared/staff-url';
import { TrendChart } from '../shared/trend-chart';

const SEX_LABELS: Record<Sex, string> = { Female: 'female', Male: 'male', NotStated: 'sex not stated' };

@Component({
  selector: 'app-athlete-page',
  imports: [RouterLink, TrendChart],
  template: `
    <a routerLink="/dashboard" class="back">← Squad</a>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    } @else if (trend(); as t) {
      <header class="page-head">
        <div>
          @if (details(); as d) {
            <h1>{{ d.displayName }}</h1>
            <p class="muted">{{ d.code }} · {{ d.sport }} · {{ age(d) }} · {{ sexLabels[d.sex] }} · {{ d.bodyMassKg }} kg</p>
          } @else {
            <h1>{{ t.code }}</h1>
          }
          <p class="muted">Training load over the last 28 days</p>
        </div>
        @if (isCoach()) {
          <a class="button" [href]="logSessionUrl(t.athleteId)" target="_blank" rel="noopener">Log session ↗</a>
        }
      </header>

      <section class="panel">
        <app-trend-chart [points]="t.points" />
        <p class="footnote">A monitoring aid for planning, not an injury predictor.</p>
      </section>

      <div class="two-col">
        <section class="panel">
          <h2>Recent sessions</h2>
          <table class="grid-table compact">
            <thead><tr><th>Date</th><th>Type</th><th class="num">Min</th><th class="num">RPE</th><th class="num">Load</th></tr></thead>
            <tbody>
              @for (s of sessions(); track s.id) {
                <tr><td>{{ s.date }}</td><td>{{ s.type }}</td><td class="num">{{ s.durationMinutes }}</td><td class="num">{{ s.rpe }}</td><td class="num">{{ s.load }}</td></tr>
              }
            </tbody>
          </table>
        </section>

        <section class="panel">
          <h2>Device measurements</h2>
          @if (measurements().length) {
            <table class="grid-table compact">
              <thead><tr><th>Date</th><th>Test</th><th class="num">Value</th></tr></thead>
              <tbody>
                @for (m of measurements(); track $index) {
                  <tr><td>{{ m.date }}</td><td>{{ m.testType }}</td><td class="num">{{ m.value }} {{ m.unit }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="muted">No imported measurements yet.</p>
          }
        </section>
      </div>

      <section class="panel">
        <h2>Physio notes</h2>
        @if (isPhysio()) {
          @for (n of notes(); track n.id) {
            <article class="note">
              <div class="muted">{{ n.date }} · {{ n.author }} · restriction: {{ n.restriction }}</div>
              <p>{{ n.note }}</p>
            </article>
          } @empty {
            <p class="muted">No notes.</p>
          }
          <p class="footnote">Every time you open these notes, it is written to the audit log.</p>
        } @else {
          <p class="muted">Health notes are visible to physios only. You see the restriction level on the squad page.</p>
        }
      </section>
    } @else {
      <p class="muted">Loading…</p>
    }
  `,
})
export class AthletePage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);

  readonly id = input.required<string>();

  protected readonly details = signal<AthleteDetails | null>(null);
  protected readonly trend = signal<LoadTrend | null>(null);
  protected readonly sessions = signal<TrainingSession[]>([]);
  protected readonly measurements = signal<Measurement[]>([]);
  protected readonly notes = signal<PhysioNote[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly isPhysio = computed(() => this.persona.current().role === 'Physio');
  protected readonly isCoach = computed(() => this.persona.current().role === 'Coach');
  protected readonly sexLabels = SEX_LABELS;
  protected readonly logSessionUrl = logSessionUrl;
  private readonly thisYear = new Date().getFullYear();

  protected age(d: AthleteDetails): string {
    return d.birthYear === null ? 'age unknown' : `${this.thisYear - d.birthYear} years`;
  }

  constructor() {
    effect(() => {
      const athleteId = Number(this.id());
      const role = this.persona.current().role;
      this.realtime.version();
      untracked(() => this.load(athleteId, role));
    });
  }

  private load(athleteId: number, role: string): void {
    forkJoin({
      details: this.api.athlete(athleteId),
      trend: this.api.loadTrend(athleteId),
      sessions: this.api.sessions(athleteId),
      measurements: this.api.measurements(athleteId),
    }).subscribe({
      next: (r) => {
        this.details.set(r.details);
        this.trend.set(r.trend);
        this.sessions.set(r.sessions);
        this.measurements.set(r.measurements);
        this.error.set(null);
      },
      error: (e) => {
        this.trend.set(null);
        this.error.set(describeError(e, role));
      },
    });

    // Only the physio asks for notes, so nobody else even triggers an audit entry.
    if (role === 'Physio') {
      this.api.physioNotes(athleteId).subscribe({ next: (n) => this.notes.set(n), error: () => this.notes.set([]) });
    } else {
      this.notes.set([]);
    }
  }
}
