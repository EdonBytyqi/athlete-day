import { Component, computed, effect, inject, input, linkedSignal, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../api/api.service';
import { AthleteSummary, Versus, VersusMetric } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';
import { addDaysIso, todayIso } from '../shared/dates';

const PERIODS = [7, 28, 90];
const BASE_METRICS = ['totalLoad', 'sessions', 'averageLoad', 'ratio', 'sleep', 'soreness', 'mood', 'checkIns'];

/**
 * Versus mode (AC-96): two athletes side by side over a period. The API ranks each metric (spec section 4); this page
 * only highlights the better value. `?a=` preselects the first athlete, e.g. from the athlete space.
 */
@Component({
  selector: 'app-versus-page',
  template: `
    <header class="page-head">
      <div>
        <h1>{{ t('versus.title') }}</h1>
        <p class="muted">{{ t('versus.lead') }}</p>
      </div>
    </header>

    <section class="panel versus-controls">
      <div class="versus-pickers">
        <label>
          {{ t('versus.athleteA') }}
          <select class="versus-a" (change)="selectedA.set(toId($any($event.target).value))">
            <option value="" [selected]="selectedA() === null">{{ t('space.choose') }}</option>
            @for (group of groups(); track group.sport) {
              <optgroup [label]="group.sport">
                @for (athlete of group.athletes; track athlete.id) {
                  <option [value]="'' + athlete.id" [selected]="athlete.id === selectedA()">{{ athlete.displayName }} ({{ athlete.code }})</option>
                }
              </optgroup>
            }
          </select>
        </label>
        <span class="versus-vs" aria-hidden="true">vs</span>
        <label>
          {{ t('versus.athleteB') }}
          <select class="versus-b" (change)="selectedB.set(toId($any($event.target).value))">
            <option value="" [selected]="selectedB() === null">{{ t('space.choose') }}</option>
            @for (group of groups(); track group.sport) {
              <optgroup [label]="group.sport">
                @for (athlete of group.athletes; track athlete.id) {
                  <option [value]="'' + athlete.id" [selected]="athlete.id === selectedB()">{{ athlete.displayName }} ({{ athlete.code }})</option>
                }
              </optgroup>
            }
          </select>
        </label>
      </div>
      <div class="chips" role="group" [attr.aria-label]="t('versus.period')">
        @for (n of periods; track n) {
          <button type="button" class="chip period" [attr.data-days]="n" [attr.aria-pressed]="days() === n" (click)="days.set(n)">
            {{ t('versus.days', { n }) }}
          </button>
        }
      </div>
    </section>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }

    @if (same()) {
      <p class="notice versus-same">{{ t('versus.same') }}</p>
    } @else if (result(); as r) {
      <section class="panel versus-result">
        <p class="muted">{{ t('versus.range', { from: i18n.date(r.from), to: i18n.date(r.to) }) }}</p>
        <div class="table-scroll">
          <table class="grid-table versus-table">
            <thead>
              <tr>
                <th>{{ t('versus.metric') }}</th>
                <th class="num">{{ r.a.displayName }}</th>
                <th class="num">{{ r.b.displayName }}</th>
              </tr>
            </thead>
            <tbody>
              @for (m of r.metrics; track m.key) {
                <tr class="versus-row" [attr.data-key]="m.key">
                  <th scope="row">
                    {{ label(m) }}
                    @if (m.winner === 'Even') {
                      <span class="tag">{{ t('versus.even') }}</span>
                    }
                  </th>
                  <td class="num value-a" [class.better]="m.winner === 'A'">{{ value(m, m.a) }}</td>
                  <td class="num value-b" [class.better]="m.winner === 'B'">{{ value(m, m.b) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <p class="footnote">{{ t('versus.legend') }}</p>
      </section>
    } @else if (!selectedA() || !selectedB()) {
      <p class="muted panel">{{ t('versus.pick') }}</p>
    }
  `,
})
export class VersusPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  /** From the query string (`/versus?a=2`). */
  readonly a = input<string | undefined>();

  protected readonly periods = PERIODS;
  protected readonly athletes = signal<AthleteSummary[]>([]);
  protected readonly selectedA = linkedSignal(() => this.toId(this.a()));
  protected readonly selectedB = signal<number | null>(null);
  protected readonly days = signal(28);
  protected readonly result = signal<Versus | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly same = computed(() => this.selectedA() !== null && this.selectedA() === this.selectedB());

  protected readonly groups = computed(() => {
    const bySport = new Map<string, AthleteSummary[]>();
    for (const athlete of [...this.athletes()].sort((x, y) => x.displayName.localeCompare(y.displayName))) {
      bySport.set(athlete.sport, [...(bySport.get(athlete.sport) ?? []), athlete]);
    }
    return [...bySport.entries()].sort(([x], [y]) => x.localeCompare(y)).map(([sport, athletes]) => ({ sport, athletes }));
  });

  constructor() {
    this.api.athletes().subscribe({
      next: (athletes) => this.athletes.set(athletes),
      error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
    });

    effect(() => {
      const [a, b, days] = [this.selectedA(), this.selectedB(), this.days()];
      untracked(() => {
        this.result.set(null);
        if (a === null || b === null || a === b) return;
        const to = todayIso();
        this.api.versus(a, b, addDaysIso(to, -(days - 1)), to).subscribe({
          next: (r) => {
            this.result.set(r);
            this.error.set(null);
          },
          error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
        });
      });
    });
  }

  protected toId(value: string | undefined | null): number | null {
    const id = Number(value);
    return value && Number.isInteger(id) && id > 0 ? id : null;
  }

  protected label(m: VersusMetric): string {
    return BASE_METRICS.includes(m.key)
      ? this.t(`versus.metrics.${m.key}` as TranslationKey)
      : this.t('versus.best', { test: this.t(`performance.tests.${m.key}` as TranslationKey) });
  }

  protected value(m: VersusMetric, value: number | null): string {
    if (value === null) return '–';
    switch (m.key) {
      case 'totalLoad':
      case 'averageLoad':
        return `${this.i18n.number(value)}\u00a0AU`;
      case 'sessions':
      case 'checkIns':
        return this.i18n.number(value);
      case 'ratio':
        return this.i18n.number(value, 2, 2);
      case 'sleep':
        return `${this.i18n.number(value, 1, 1)}\u00a0h`;
      case 'soreness':
      case 'mood':
        return `${this.i18n.number(value, 1, 1)}/5`;
      default:
        return m.unit ? `${this.i18n.number(value, 2, 0)}\u00a0${m.unit}` : this.i18n.number(value, 2, 0);
    }
  }
}
