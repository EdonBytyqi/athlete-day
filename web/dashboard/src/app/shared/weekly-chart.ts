import { Component, computed, inject, input } from '@angular/core';
import { WeeklyProgress } from '../api/models';
import { I18n } from '../i18n/i18n.service';
import { niceMax } from './trend-chart';

const W = 720;
const H = 220;
const PAD = { top: 12, right: 40, bottom: 26, left: 48 };
const SLEEP_MAX = 12; // hours, right axis

/** Weekly load as bars and average sleep as a line on its own axis (AC-68). */
@Component({
  selector: 'app-weekly-chart',
  template: `
    <figure class="chart weekly-chart">
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" role="img" [attr.aria-label]="t('overview.weeklyChart', { n: weeks().length })">
        @for (tick of geometry().ticks; track tick.value) {
          <line class="grid" [attr.x1]="pad.left" [attr.x2]="width - pad.right" [attr.y1]="tick.y" [attr.y2]="tick.y" />
          <text class="axis" [attr.x]="pad.left - 6" [attr.y]="tick.y + 4" text-anchor="end">{{ i18n.number(tick.value) }}</text>
        }
        @for (b of geometry().bars; track b.label) {
          <rect class="bar" [attr.x]="b.x" [attr.y]="b.y" [attr.width]="b.width" [attr.height]="b.height" rx="3">
            <title>{{ b.label }}: {{ i18n.number(b.load) }} AU</title>
          </rect>
          <text class="axis" [attr.x]="b.x + b.width / 2" [attr.y]="height - 8" text-anchor="middle">{{ b.short }}</text>
        }
        <path class="line sleep" [attr.d]="geometry().sleepPath" />
        <text class="axis" [attr.x]="width - pad.right + 6" [attr.y]="pad.top + 8">{{ sleepMax }} h</text>
      </svg>
      <figcaption class="legend">
        <span><i class="key bar"></i>{{ t('overview.weekLoad') }}</span>
        <span><i class="key acute"></i>{{ t('overview.weekSleep') }}</span>
      </figcaption>
    </figure>
  `,
})
export class WeeklyChart {
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly weeks = input.required<WeeklyProgress[]>();

  protected readonly width = W;
  protected readonly height = H;
  protected readonly pad = PAD;
  protected readonly sleepMax = SLEEP_MAX;

  protected readonly geometry = computed(() => {
    const weeks = this.weeks();
    const innerW = W - PAD.left - PAD.right;
    const innerH = H - PAD.top - PAD.bottom;
    const maxY = niceMax(Math.max(0, ...weeks.map((w) => w.totalLoad)));
    const step = weeks.length ? innerW / weeks.length : 0;
    const y = (v: number, max: number) => PAD.top + innerH - (v / max) * innerH;
    const center = (i: number) => PAD.left + step * i + step / 2;
    const sleepPoints = weeks
      .map((w, i) => (w.averageSleep === null ? null : `${center(i).toFixed(1)},${y(w.averageSleep, SLEEP_MAX).toFixed(1)}`))
      .filter((p): p is string => p !== null);

    return {
      bars: weeks.map((w, i) => ({
        x: PAD.left + step * i + step * 0.2,
        y: y(w.totalLoad, maxY),
        width: step * 0.6,
        height: (w.totalLoad / maxY) * innerH,
        load: w.totalLoad,
        label: this.i18n.date(w.weekStart),
        short: w.weekStart.slice(8, 10) + '.' + w.weekStart.slice(5, 7) + '.',
      })),
      sleepPath: sleepPoints.length ? 'M' + sleepPoints.join(' L') : '',
      ticks: [0, 0.5, 1].map((f) => ({ value: maxY * f, y: y(maxY * f, maxY) })),
    };
  });
}
