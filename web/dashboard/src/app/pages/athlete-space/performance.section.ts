import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../../api/api.service';
import { Benchmark } from '../../api/models';
import { PersonaService } from '../../api/persona.service';
import { TranslationKey } from '../../i18n/catalogue';
import { I18n } from '../../i18n/i18n.service';
import { BenchmarkChart } from '../../shared/benchmark-chart';
import { ToastService } from '../../shared/toast.service';

interface GoalDraft {
  testType: string;
  target: string;
  targetDate: string;
  note: string;
}

/** Device tests against goals, own best and the squad (AC-71). The coach sets and removes goals here. */
@Component({
  selector: 'app-performance-section',
  imports: [BenchmarkChart],
  template: `
    <section class="panel">
      <h2>{{ t('performance.title') }}</h2>
      <p class="muted">{{ t('performance.lead') }}</p>
    </section>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }

    <div class="benchmarks">
      @for (b of benchmarks(); track b.testType) {
        <article class="benchmark panel" [attr.data-test]="b.testType" [class]="'benchmark panel status-' + (b.status ?? 'none').toLowerCase()">
          <header class="benchmark-head">
            <h3>{{ t(testLabel(b.testType)) }}</h3>
            @if (!b.higherIsBetter) {
              <span class="tag">{{ t('performance.lowerIsBetter') }}</span>
            }
          </header>

          <p class="gap">{{ gapText(b) }}</p>

          <app-benchmark-chart [benchmark]="b" [testName]="t(testLabel(b.testType))" />
          <p class="legend">
            <span><i class="key acute"></i>{{ t('performance.results') }}</span>
            @if (b.goal) { <span><i class="key goal"></i>{{ t('performance.goal') }}</span> }
            @if (b.squadAverage !== null) { <span><i class="key chronic"></i>{{ t('performance.squad') }}</span> }
          </p>

          <dl class="figures">
            <div><dt>{{ t('performance.latest') }}</dt><dd>{{ value(b, b.latest.value) }}</dd></div>
            <div><dt>{{ t('performance.best') }}</dt><dd>{{ value(b, b.best.value) }}</dd></div>
            <div><dt>{{ t('performance.change') }}</dt><dd>{{ b.changeInPeriod === null ? '–' : signed(b, b.changeInPeriod) }}</dd></div>
            <div><dt>{{ t('performance.squad') }}</dt><dd>{{ b.squadAverage === null ? '–' : value(b, b.squadAverage) }}</dd></div>
            @if (b.goal; as goal) {
              <div>
                <dt>{{ t('performance.goal') }}</dt>
                <dd>
                  {{ value(b, goal.target) }}
                  @if (goal.targetDate) { · {{ t('performance.by', { date: i18n.date(goal.targetDate) }) }} }
                  @if (goal.note) { <span class="muted goal-note">{{ goal.note }}</span> }
                </dd>
              </div>
            }
          </dl>

          @if (isCoach()) {
            @if (draft()?.testType === b.testType) {
              <form class="goal-form" (submit)="saveGoal($event, b)">
                <label>
                  {{ t('performance.target', { unit: b.unit }) }}
                  <input name="target" type="number" step="0.01" required [value]="draft()!.target" (input)="edit('target', $any($event.target).value)" />
                </label>
                <label>
                  {{ t('performance.targetDate') }}
                  <input name="targetDate" type="date" [value]="draft()!.targetDate" (input)="edit('targetDate', $any($event.target).value)" />
                </label>
                <label>
                  {{ t('performance.note') }}
                  <input name="note" maxlength="200" [value]="draft()!.note" (input)="edit('note', $any($event.target).value)" />
                </label>
                <div class="dialog-actions">
                  <button type="button" class="secondary" (click)="draft.set(null)">{{ t('common.cancel') }}</button>
                  <button type="submit" class="save-goal">{{ t('performance.saveGoal') }}</button>
                </div>
              </form>
            } @else {
              <div class="goal-actions">
                <button type="button" class="secondary set-goal" (click)="openGoal(b)">
                  {{ b.goal ? t('performance.editGoal') : t('performance.setGoal') }}
                </button>
                @if (b.goal) {
                  <button type="button" class="secondary remove-goal" (click)="removeGoal(b)">{{ t('performance.removeGoal') }}</button>
                }
              </div>
            }
          }
        </article>
      } @empty {
        <p class="muted panel">{{ t('performance.empty') }}</p>
      }
    </div>
  `,
})
export class PerformanceSection {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly toasts = inject(ToastService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly id = input.required<string>();

  protected readonly benchmarks = signal<Benchmark[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly draft = signal<GoalDraft | null>(null);
  protected readonly isCoach = computed(() => this.persona.current().role === 'Coach');

  constructor() {
    effect(() => {
      const athleteId = Number(this.id());
      this.persona.current();
      untracked(() => {
        this.draft.set(null);
        this.load(athleteId);
      });
    });
  }

  protected testLabel(testType: string): TranslationKey {
    return `performance.tests.${testType}` as TranslationKey;
  }

  /** The gap in words, e.g. "3 cm to go" or "Goal reached" (AC-71). */
  protected gapText(b: Benchmark): string {
    if (!b.goal || b.gapToGoal === null) return this.t('performance.noGoal');
    if (b.status === 'Reached') return this.t('performance.reached');
    const toGo = this.t('performance.toGo', { value: this.i18n.number(b.gapToGoal, 2, 0), unit: b.unit });
    return b.status === 'Behind' ? `${toGo} · ${this.t('performance.datePassed')}` : toGo;
  }

  protected value(b: Benchmark, v: number): string {
    return `${this.i18n.number(v, 2, 0)} ${b.unit}`;
  }

  protected signed(b: Benchmark, v: number): string {
    return `${v > 0 ? '+' : ''}${this.value(b, v)}`;
  }

  protected openGoal(b: Benchmark): void {
    this.draft.set({
      testType: b.testType,
      target: b.goal ? String(b.goal.target) : '',
      targetDate: b.goal?.targetDate ?? '',
      note: b.goal?.note ?? '',
    });
  }

  protected edit(field: 'target' | 'targetDate' | 'note', value: string): void {
    this.draft.update((d) => (d ? { ...d, [field]: value } : d));
  }

  protected saveGoal(event: Event, b: Benchmark): void {
    event.preventDefault();
    const d = this.draft();
    if (!d || d.target === '') return;
    const athleteId = Number(this.id());
    this.api
      .setGoal(athleteId, b.testType, { target: Number(d.target), targetDate: d.targetDate || null, note: d.note.trim() || null })
      .subscribe({
        next: () => {
          this.draft.set(null);
          this.toasts.show(this.t('performance.goalSaved'));
          this.load(athleteId);
        },
        error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
      });
  }

  protected removeGoal(b: Benchmark): void {
    const athleteId = Number(this.id());
    this.api.removeGoal(athleteId, b.testType).subscribe({
      next: () => {
        this.toasts.show(this.t('performance.goalRemoved'));
        this.load(athleteId);
      },
      error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
    });
  }

  private load(athleteId: number): void {
    this.api.benchmarks(athleteId).subscribe({
      next: (b) => {
        this.benchmarks.set(b);
        this.error.set(null);
      },
      error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
    });
  }
}
