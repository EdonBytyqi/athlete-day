import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService, describeError } from '../api/api.service';
import { AthleteDetails, LoadTrend, Measurement, PhysioNote, Sex, TrainingRestriction, TrainingSession, WellnessDay } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';
import { logSessionUrl } from '../shared/staff-url';
import { TrendChart } from '../shared/trend-chart';

@Component({
  selector: 'app-athlete-page',
  imports: [RouterLink, TrendChart],
  template: `
    <a routerLink="/dashboard" class="back">{{ t('athlete.back') }}</a>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    } @else if (trend(); as load) {
      <header class="page-head">
        <div>
          @if (details(); as d) {
            <h1>{{ d.displayName }}</h1>
            <p class="muted">
              {{ d.code }} · {{ d.sport }} · {{ age(d) }} · {{ t(sexText(d.sex)) }} ·
              {{ t('athlete.kg', { value: i18n.number(d.bodyMassKg, 1, 0) }) }}
            </p>
          } @else {
            <h1>{{ load.code }}</h1>
          }
          <p class="muted">{{ t('athlete.trendLead') }}</p>
        </div>
        @if (isCoach()) {
          <a class="button" [href]="logSessionUrl(load.athleteId)" target="_blank" rel="noopener">{{ t('athlete.logSession') }}</a>
        }
      </header>

      <section class="panel">
        <app-trend-chart [points]="load.points" />
        <p class="footnote">{{ t('athlete.monitoringAid') }}</p>
      </section>

      <div class="two-col">
        <section class="panel">
          <h2>{{ t('athlete.recentSessions') }}</h2>
          <div class="table-scroll">
            <table class="grid-table compact">
              <thead>
                <tr>
                  <th>{{ t('athlete.col.date') }}</th><th>{{ t('athlete.col.type') }}</th>
                  <th class="num">{{ t('athlete.col.minutes') }}</th><th class="num">{{ t('athlete.col.rpe') }}</th>
                  <th class="num">{{ t('athlete.col.load') }}</th>
                </tr>
              </thead>
              <tbody>
                @for (s of sessions(); track s.id) {
                  <tr>
                    <td class="nowrap">{{ i18n.date(s.date) }}</td><td>{{ t(sessionType(s.type)) }}</td>
                    <td class="num">{{ s.durationMinutes }}</td><td class="num">{{ s.rpe }}</td>
                    <td class="num">{{ i18n.number(s.load) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="panel">
          <h2>{{ t('athlete.measurements') }}</h2>
          @if (measurements().length) {
            <div class="table-scroll">
              <table class="grid-table compact">
                <thead>
                  <tr><th>{{ t('athlete.col.date') }}</th><th>{{ t('athlete.col.test') }}</th><th class="num">{{ t('athlete.col.value') }}</th></tr>
                </thead>
                <tbody>
                  @for (m of measurements(); track $index) {
                    <tr>
                      <td class="nowrap">{{ i18n.date(m.date) }}</td><td>{{ m.testType }}</td>
                      <td class="num">{{ i18n.number(m.value, 2, 0) }} {{ m.unit }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="muted">{{ t('athlete.noMeasurements') }}</p>
          }
        </section>
      </div>

      @if (canSeeWellness()) {
        <section class="panel">
          <h2>{{ t('athlete.wellnessTitle') }}</h2>
          @if (wellness().length) {
            <div class="table-scroll">
              <table class="grid-table compact wellness-history">
                <thead>
                  <tr>
                    <th>{{ t('athlete.col.date') }}</th><th class="num">{{ t('athlete.col.sleep') }}</th>
                    <th class="num">{{ t('athlete.col.soreness') }}</th><th class="num">{{ t('athlete.col.mood') }}</th>
                    <th>{{ t('athlete.col.flags') }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (d of wellness(); track d.date) {
                    <tr [class.flagged]="d.flags.length > 0">
                      <td class="nowrap">{{ i18n.date(d.date) }}</td>
                      <td class="num">{{ i18n.number(d.sleepHours, 1, 0) }} h</td>
                      <td class="num">{{ d.soreness }}/5</td>
                      <td class="num">{{ d.mood }}/5</td>
                      <td>{{ d.flags.join(' · ') || '–' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="muted">{{ t('athlete.noWellness') }}</p>
          }
        </section>
      }

      <section class="panel">
        <h2>{{ t('athlete.physioNotes') }}</h2>
        @if (isPhysio()) {
          @for (n of notes(); track n.id) {
            <article class="note">
              <div class="muted">
                {{ t('athlete.noteMeta', { date: i18n.date(n.date), author: n.author, restriction: t(restrictionLabel(n.restriction)) }) }}
              </div>
              <p>{{ n.note }}</p>
            </article>
          } @empty {
            <p class="muted">{{ t('athlete.noNotes') }}</p>
          }
          <p class="footnote">{{ t('athlete.notesAudited') }}</p>
        } @else {
          <p class="muted">{{ t('athlete.notesPhysioOnly') }}</p>
        }
      </section>
    } @else {
      <p class="muted">{{ t('common.loading') }}</p>
    }
  `,
})
export class AthletePage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly id = input.required<string>();

  protected readonly details = signal<AthleteDetails | null>(null);
  protected readonly trend = signal<LoadTrend | null>(null);
  protected readonly sessions = signal<TrainingSession[]>([]);
  protected readonly measurements = signal<Measurement[]>([]);
  protected readonly notes = signal<PhysioNote[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly wellness = signal<WellnessDay[]>([]);
  protected readonly isPhysio = computed(() => this.persona.current().role === 'Physio');
  protected readonly isCoach = computed(() => this.persona.current().role === 'Coach');
  private readonly thisYear = new Date().getFullYear();
  // Spec F13 AC-46/AC-49: wellness history is for coach and physio only.
  protected readonly canSeeWellness = computed(() => ['Coach', 'Physio'].includes(this.persona.current().role));

  constructor() {
    // Reload on a language switch too: wellness flags are server texts in the request's language.
    effect(() => {
      const athleteId = Number(this.id());
      const role = this.persona.current().role;
      this.i18n.lang();
      this.realtime.version();
      untracked(() => this.load(athleteId, role));
    });
  }

  protected logSessionUrl(athleteId: number): string {
    return logSessionUrl(athleteId, this.i18n.lang());
  }

  protected age(d: AthleteDetails): string {
    return d.birthYear === null ? this.t('athlete.ageUnknown') : this.t('athlete.years', { n: this.thisYear - d.birthYear });
  }

  protected sexText(sex: Sex): TranslationKey {
    return `athlete.sexText.${sex}`;
  }

  protected sessionType(type: string): TranslationKey {
    return `sessionType.${type}` as TranslationKey;
  }

  protected restrictionLabel(restriction: TrainingRestriction): TranslationKey {
    return `restriction.${restriction}`;
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
        this.error.set(describeError(e, role, this.t));
      },
    });

    if (role === 'Coach' || role === 'Physio') {
      this.api.wellness(athleteId).subscribe({ next: (w) => this.wellness.set(w), error: () => this.wellness.set([]) });
    } else {
      this.wellness.set([]);
    }

    // Only the physio asks for notes, so nobody else even triggers an audit entry.
    if (role === 'Physio') {
      this.api.physioNotes(athleteId).subscribe({ next: (n) => this.notes.set(n), error: () => this.notes.set([]) });
    } else {
      this.notes.set([]);
    }
  }
}
