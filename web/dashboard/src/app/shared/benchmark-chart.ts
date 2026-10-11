import { Component, computed, inject, input } from '@angular/core';
import { Benchmark } from '../api/models';
import { I18n } from '../i18n/i18n.service';

const W = 360;
const H = 140;
const PAD = { top: 10, right: 10, bottom: 18, left: 44 };

/** One device test over the period, with the goal line and the squad average (AC-71). */
@Component({
  selector: 'app-benchmark-chart',
  template: `
    <svg class="benchmark-chart" [attr.viewBox]="'0 0 ' + width + ' ' + height" role="img" [attr.aria-label]="label()">
      @for (tick of geometry().ticks; track tick.value) {
        <line class="grid" [attr.x1]="pad.left" [attr.x2]="width - pad.right" [attr.y1]="tick.y" [attr.y2]="tick.y" />
        <text class="axis" [attr.x]="pad.left - 6" [attr.y]="tick.y + 4" text-anchor="end">{{ i18n.number(tick.value, geometry().decimals, 0) }}</text>
      }
      @if (geometry().goalY !== null) {
        <line class="goal-line" [attr.x1]="pad.left" [attr.x2]="width - pad.right" [attr.y1]="geometry().goalY" [attr.y2]="geometry().goalY" />
      }
      @if (geometry().squadY !== null) {
        <line class="squad-line" [attr.x1]="pad.left" [attr.x2]="width - pad.right" [attr.y1]="geometry().squadY" [attr.y2]="geometry().squadY" />
      }
      <path class="line results" [attr.d]="geometry().path" />
      @for (p of geometry().points; track p.date) {
        <circle class="result" [attr.cx]="p.x" [attr.cy]="p.y" r="3">
          <title>{{ i18n.date(p.date) }}: {{ i18n.number(p.value, 2, 0) }} {{ benchmark().unit }}</title>
        </circle>
      }
    </svg>
  `,
})
export class BenchmarkChart {
  protected readonly i18n = inject(I18n);
  readonly benchmark = input.required<Benchmark>();
  readonly testName = input.required<string>();

  protected readonly width = W;
  protected readonly height = H;
  protected readonly pad = PAD;
  protected readonly label = computed(() => this.i18n.t('performance.chart', { test: this.testName() }));

  protected readonly geometry = computed(() => {
    const b = this.benchmark();
    const history = b.history.length ? b.history : [b.latest];
    const values = [...history.map((h) => h.value), b.goal?.target, b.squadAverage].filter((v): v is number => v != null);
    const spread = Math.max(...values) - Math.min(...values) || Math.abs(values[0]) * 0.1 || 1;
    const min = Math.min(...values) - spread * 0.15;
    const max = Math.max(...values) + spread * 0.15;
    const innerW = W - PAD.left - PAD.right;
    const innerH = H - PAD.top - PAD.bottom;
    const y = (v: number) => PAD.top + innerH - ((v - min) / (max - min)) * innerH;
    const x = (i: number) => PAD.left + (history.length > 1 ? (innerW * i) / (history.length - 1) : innerW / 2);
    const points = history.map((h, i) => ({ date: h.date, value: h.value, x: x(i), y: y(h.value) }));

    return {
      points,
      path: points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' '),
      goalY: b.goal ? y(b.goal.target) : null,
      squadY: b.squadAverage != null ? y(b.squadAverage) : null,
      // Enough decimals that the three ticks differ: sprint times need 2, jumps 1, distances none.
      decimals: max - min < 1 ? 2 : max - min < 50 ? 1 : 0,
      ticks: [min + (max - min) * 0.1, (min + max) / 2, max - (max - min) * 0.1].map((v) => ({ value: v, y: y(v) })),
    };
  });
}
