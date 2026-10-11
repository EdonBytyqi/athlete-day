import { Component, computed, inject, input, signal } from '@angular/core';
import { DashboardRow } from '../api/models';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';

type Metric = 'acuteLoad' | 'chronicLoad' | 'ratio';

const LABELS: Record<Metric, TranslationKey> = {
  acuteLoad: 'squad.col.weekAvg',
  chronicLoad: 'squad.col.monthAvg',
  ratio: 'squad.col.weekVsMonth',
};

/** The usual range of the week-vs-month ratio (spec section 4); outside it is worth a conversation. */
const BAND = { low: 0.8, high: 1.3 };

interface Bar {
  id: number;
  name: string;
  value: number | null;
  width: number;
  label: string;
  outside: boolean;
}

/**
 * The squad as bars on one load metric at a time, highest first (AC-62).
 * For week vs month, the usual range is shaded and bars outside it are highlighted.
 */
@Component({
  selector: 'app-load-compare',
  template: `
    <section class="compare panel" aria-labelledby="compare-title">
      <div class="compare-head">
        <h2 id="compare-title">{{ t('compare.title') }}</h2>
        <div class="compare-metrics chips" role="group" [attr.aria-label]="t('compare.metric')">
          @for (m of metrics; track m) {
            <button type="button" class="chip" [attr.aria-pressed]="metric() === m" (click)="metric.set(m)">{{ t(labels[m]) }}</button>
          }
        </div>
      </div>
      <ol class="bars">
        @for (b of bars(); track b.id) {
          <li class="bar-row" [class.outside]="b.outside">
            <span class="bar-name">{{ b.name }}</span>
            <span class="bar-track" aria-hidden="true">
              @if (metric() === 'ratio') {
                <span class="band" [style.left.%]="band().left" [style.width.%]="band().width"></span>
              }
              <span class="bar-fill" [style.width.%]="b.width"></span>
            </span>
            <span class="bar-value">{{ b.label }}</span>
          </li>
        }
      </ol>
      @if (metric() === 'ratio') {
        <p class="footnote">{{ t('compare.bandNote') }}</p>
      }
    </section>
  `,
})
export class LoadCompare {
  private readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly rows = input.required<DashboardRow[]>();

  protected readonly metrics: Metric[] = ['acuteLoad', 'chronicLoad', 'ratio'];
  protected readonly labels = LABELS;
  protected readonly metric = signal<Metric>('acuteLoad');

  /** Axis maximum: the highest value, and for the ratio at least 2 so the band keeps its place. */
  private readonly scale = computed(() => {
    const values = this.rows().map((r) => this.valueOf(r) ?? 0);
    const max = Math.max(0, ...values);
    return this.metric() === 'ratio' ? Math.max(2, max) : max || 1;
  });

  protected readonly band = computed(() => ({
    left: (BAND.low / this.scale()) * 100,
    width: ((BAND.high - BAND.low) / this.scale()) * 100,
  }));

  protected readonly bars = computed<Bar[]>(() => {
    const isRatio = this.metric() === 'ratio';
    return this.rows()
      .map((r) => {
        const value = this.valueOf(r);
        return {
          id: r.athleteId,
          name: r.displayName,
          value,
          width: value === null ? 0 : (value / this.scale()) * 100,
          label: value === null ? '–' : this.i18n.number(value, isRatio ? 2 : 0),
          outside: isRatio && value !== null && (value < BAND.low || value > BAND.high),
        };
      })
      .sort((a, b) => (b.value ?? -1) - (a.value ?? -1) || a.name.localeCompare(b.name));
  });

  private valueOf(r: DashboardRow): number | null {
    switch (this.metric()) {
      case 'acuteLoad':
        return r.acuteLoad;
      case 'chronicLoad':
        return r.chronicLoad;
      case 'ratio':
        return r.acuteChronicRatio;
    }
  }
}
