import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../../api/api.service';
import { LoadTrend, PhysioNote, Progress, Trend, TrendItem, TrendMetric, TrainingRestriction, WellnessDay } from '../../api/models';
import { PersonaService } from '../../api/persona.service';
import { RealtimeService } from '../../api/realtime.service';
import { TranslationKey } from '../../i18n/catalogue';
import { I18n } from '../../i18n/i18n.service';
import { TrendChart } from '../../shared/trend-chart';
import { WeeklyChart } from '../../shared/weekly-chart';

type Meaning = 'good' | 'attention' | 'change' | 'stable';

/** What a change means (spec section 4): up is good for sleep and mood, bad for soreness, neutral for load. */
const DIRECTION: Record<TrendMetric, 'higherBetter' | 'lowerBetter' | 'neutral'> = {
  load: 'neutral',
  sessions: 'neutral',
  sleep: 'higherBetter',
  soreness: 'lowerBetter',
  mood: 'higherBetter',
};

export function meaningOf(metric: TrendMetric, trend: Trend): Meaning {
  if (trend === 'Stable') return 'stable';
  const direction = DIRECTION[metric];
  if (direction === 'neutral') return 'change';
  return (trend === 'Up') === (direction === 'higherBetter') ? 'good' : 'attention';
}

const ARROWS: Record<Trend, string> = { Up: '↑', Down: '↓', Stable: '→' };

/** Overview of the athlete space (AC-68, AC-49): key figures, weekly chart, daily trend, wellness and physio notes. */
@Component({
  selector: 'app-overview-section',
  imports: [TrendChart, WeeklyChart],
  template: `
    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }

    <section class="panel">
      <div class="section-head">
        <div>
          <h2>{{ t('overview.figures') }}</h2>
          <p class="muted">{{ t('overview.compared') }}</p>
        </div>
        <div class="weeks chips" role="group" [attr.aria-label]="t('space.periodLabel')">
          @for (n of weekOptions; track n) {
            <button type="button" class="chip" [attr.aria-pressed]="weeks() === n" (click)="weeks.set(n)">{{ t('space.weeks', { n }) }}</button>
          }
        </div>
      </div>

      @if (progress(); as p) {
        <div class="kpis">
          @for (k of kpis(); track k.metric) {
            <div class="kpi" [attr.data-metric]="k.metric" [class]="'kpi meaning-' + k.meaning">
              <span class="kpi-label">{{ t(k.label) }}</span>
              <span class="kpi-value">
                {{ k.value }}
                <span class="trend" [class]="'trend trend-' + k.trend.toLowerCase()" [attr.aria-label]="t(trendLabel(k.trend))">{{ k.arrow }}</span>
              </span>
              <span class="kpi-note">{{ t(meaningLabel(k.meaning)) }}</span>
              <span class="kpi-before muted">{{ t('overview.before', { value: k.before }) }}</span>
            </div>
          }
        </div>

        <h3 class="sub">{{ t('overview.weekly') }}</h3>
        <app-weekly-chart [weeks]="p.weeks" />
      } @else {
        <p class="muted">{{ t('common.loading') }}</p>
      }
    </section>

    @if (trend(); as load) {
      <section class="panel">
        <h2>{{ t('overview.trend28') }}</h2>
        <app-trend-chart [points]="load.points" />
        <p class="footnote">{{ t('athlete.monitoringAid') }}</p>
      </section>
    }

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
  `,
})
export class OverviewSection {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly id = input.required<string>();

  protected readonly weekOptions = [4, 12, 26];
  protected readonly weeks = signal(12);
  protected readonly progress = signal<Progress | null>(null);
  protected readonly trend = signal<LoadTrend | null>(null);
  protected readonly wellness = signal<WellnessDay[]>([]);
  protected readonly notes = signal<PhysioNote[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly isPhysio = computed(() => this.persona.current().role === 'Physio');
  // Spec F13 AC-46/AC-49: wellness history is for coach and physio only.
  protected readonly canSeeWellness = computed(() => ['Coach', 'Physio'].includes(this.persona.current().role));

  protected readonly kpis = computed(() =>
    (this.progress()?.trends ?? []).map((item) => {
      const meaning = meaningOf(item.metric, item.trend);
      return {
        metric: item.metric,
        label: `overview.kpi.${item.metric}` as TranslationKey,
        value: this.format(item, item.recent),
        before: this.format(item, item.previous),
        trend: item.trend,
        arrow: ARROWS[item.trend],
        meaning,
      };
    }),
  );

  constructor() {
    // Progress follows the chosen period; everything reloads on another athlete, role, language or a pushed change.
    effect(() => {
      const athleteId = Number(this.id());
      const weeks = this.weeks();
      this.persona.current();
      this.i18n.lang();
      this.realtime.version();
      untracked(() => this.loadProgress(athleteId, weeks));
    });
    effect(() => {
      const athleteId = Number(this.id());
      const role = this.persona.current().role;
      this.i18n.lang();
      this.realtime.version();
      untracked(() => this.loadRest(athleteId, role));
    });
  }

  protected trendLabel(trend: Trend): TranslationKey {
    return `overview.trend.${trend}`;
  }

  protected meaningLabel(meaning: Meaning): TranslationKey {
    return `overview.meaning.${meaning}`;
  }

  protected restrictionLabel(restriction: TrainingRestriction): TranslationKey {
    return `restriction.${restriction}`;
  }

  private format(item: TrendItem, value: number | null): string {
    if (value === null) return '–';
    if (item.metric === 'load') return `${this.i18n.number(value)} AU`;
    if (item.metric === 'sessions') return this.i18n.number(value);
    if (item.metric === 'sleep') return `${this.i18n.number(value, 1, 1)} h`;
    return `${this.i18n.number(value, 1, 1)}/5`;
  }

  private loadProgress(athleteId: number, weeks: number): void {
    this.api.progress(athleteId, weeks).subscribe({
      next: (p) => {
        this.progress.set(p);
        this.error.set(null);
      },
      error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
    });
  }

  private loadRest(athleteId: number, role: string): void {
    this.api.loadTrend(athleteId).subscribe({ next: (t) => this.trend.set(t), error: () => this.trend.set(null) });

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
