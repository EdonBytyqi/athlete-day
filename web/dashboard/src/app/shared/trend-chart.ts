import { Component, computed, inject, input } from '@angular/core';
import { I18n } from '../i18n/i18n.service';
import { LoadTrendPoint } from '../api/models';

export interface Bar {
  x: number;
  y: number;
  width: number;
  height: number;
  date: string;
  load: number;
}

export interface ChartGeometry {
  bars: Bar[];
  acutePath: string;
  chronicPath: string;
  maxY: number;
  ticks: { value: number; y: number }[];
}

export const PADDING = { top: 12, right: 12, bottom: 22, left: 44 };

/** Rounds up to a readable axis maximum (multiples of 250 AU, at least 250). */
export function niceMax(value: number): number {
  return Math.max(250, Math.ceil(value / 250) * 250);
}

/** Pure geometry for the chart, so it can be unit-tested without a browser. */
export function chartGeometry(points: LoadTrendPoint[], width: number, height: number): ChartGeometry {
  const innerW = width - PADDING.left - PADDING.right;
  const innerH = height - PADDING.top - PADDING.bottom;
  const maxY = niceMax(Math.max(0, ...points.flatMap((p) => [p.dailyLoad, p.acute7, p.chronic28])));
  const step = points.length ? innerW / points.length : 0;
  const yFor = (v: number) => PADDING.top + innerH - (v / maxY) * innerH;
  const xCenter = (i: number) => PADDING.left + step * i + step / 2;
  const line = (pick: (p: LoadTrendPoint) => number) =>
    points.map((p, i) => `${i === 0 ? 'M' : 'L'}${round(xCenter(i))},${round(yFor(pick(p)))}`).join(' ');

  return {
    bars: points.map((p, i) => ({
      x: round(PADDING.left + step * i + step * 0.2),
      y: round(yFor(p.dailyLoad)),
      width: round(step * 0.6),
      height: round((p.dailyLoad / maxY) * innerH),
      date: p.date,
      load: p.dailyLoad,
    })),
    acutePath: line((p) => p.acute7),
    chronicPath: line((p) => p.chronic28),
    maxY,
    ticks: [0, 0.5, 1].map((f) => ({ value: maxY * f, y: round(yFor(maxY * f)) })),
  };
}

function round(n: number): number {
  return Math.round(n * 10) / 10;
}

@Component({
  selector: 'app-trend-chart',
  template: `
    <figure class="chart">
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" role="img"
           [attr.aria-label]="t('chart.label', { days: points().length })">
        @for (tick of geometry().ticks; track tick.value) {
          <line class="grid" [attr.x1]="pad.left" [attr.x2]="width - pad.right" [attr.y1]="tick.y" [attr.y2]="tick.y" />
          <text class="axis" [attr.x]="pad.left - 6" [attr.y]="tick.y + 4" text-anchor="end">{{ i18n.number(tick.value) }}</text>
        }
        @for (b of geometry().bars; track b.date) {
          <rect class="bar" [attr.x]="b.x" [attr.y]="b.y" [attr.width]="b.width" [attr.height]="b.height" rx="2">
            <title>{{ t('chart.barTitle', { date: i18n.date(b.date), load: i18n.number(b.load) }) }}</title>
          </rect>
        }
        <path class="line chronic" [attr.d]="geometry().chronicPath" />
        <path class="line acute" [attr.d]="geometry().acutePath" />
      </svg>
      <figcaption class="legend">
        <span><i class="key bar"></i>{{ t('chart.daily') }}</span>
        <span><i class="key acute"></i>{{ t('chart.acute') }}</span>
        <span><i class="key chronic"></i>{{ t('chart.chronic') }}</span>
      </figcaption>
    </figure>
  `,
})
export class TrendChart {
  readonly points = input.required<LoadTrendPoint[]>();
  protected readonly width = 720;
  protected readonly height = 240;
  protected readonly pad = PADDING;
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;
  protected readonly geometry = computed(() => chartGeometry(this.points(), this.width, this.height));
}
