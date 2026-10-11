import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../../api/api.service';
import { ProgressReview } from '../../api/models';
import { PersonaService } from '../../api/persona.service';
import { TranslationKey } from '../../i18n/catalogue';
import { I18n } from '../../i18n/i18n.service';
import { ToastService } from '../../shared/toast.service';

type Part = 'observations' | 'achievements' | 'challenges' | 'recommendations';
const PARTS: Part[] = ['observations', 'achievements', 'challenges', 'recommendations'];

/** Progress reviews (AC-77): past reviews with their stored figures; the coach writes new ones (ADR-13). */
@Component({
  selector: 'app-reviews-section',
  template: `
    <section class="panel">
      <h2>{{ t('reviews.title') }}</h2>
      <p class="muted">{{ t('reviews.lead') }}</p>
      @if (!canRead()) {
        <p class="notice reviews-restricted">{{ t('reviews.restricted') }}</p>
      }
    </section>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }

    @if (isCoach()) {
      <form class="review-form panel" (submit)="save($event)">
        <h3>{{ t('reviews.write') }}</h3>
        <div class="review-weeks chips" role="group" [attr.aria-label]="t('reviews.forPeriod')">
          <span class="muted">{{ t('reviews.forPeriod') }}</span>
          @for (n of weekOptions; track n) {
            <button type="button" class="chip" [attr.aria-pressed]="weeks() === n" (click)="weeks.set(n)">{{ t('space.weeks', { n }) }}</button>
          }
        </div>
        @for (part of parts; track part) {
          <label>
            {{ t(partLabel(part)) }}
            <textarea [name]="part" rows="3" maxlength="2000" [value]="draft()[part]" (input)="edit(part, $any($event.target).value)"></textarea>
          </label>
        }
        <div class="dialog-actions">
          <button type="submit" class="save-review" [disabled]="!hasText()">{{ t('reviews.save') }}</button>
        </div>
      </form>
    }

    @if (canRead()) {
      @for (r of reviews(); track r.id) {
        <article class="review panel">
          <header>
            <h3>{{ t('reviews.period', { from: i18n.date(r.from), to: i18n.date(r.to) }) }}</h3>
            <p class="muted">{{ t('reviews.meta', { author: r.author, date: i18n.date(r.createdAt) }) }}</p>
          </header>
          @for (part of parts; track part) {
            @if (r[part]; as text) {
              <h4>{{ t(partLabel(part)) }}</h4>
              <p>{{ text }}</p>
            }
          }
          <div class="snapshot">
            <h4>{{ t('reviews.snapshot') }}</h4>
            <dl class="figures">
              <div><dt>{{ t('reviews.totalLoad') }}</dt><dd>{{ i18n.number(r.snapshot.totalLoad) }} AU</dd></div>
              <div><dt>{{ t('reviews.sessions') }}</dt><dd>{{ r.snapshot.sessions }}</dd></div>
              <div><dt>{{ t('reviews.sleep') }}</dt><dd>{{ r.snapshot.averageSleep === null ? '–' : i18n.number(r.snapshot.averageSleep, 1, 1) + ' h' }}</dd></div>
              <div><dt>{{ t('reviews.soreness') }}</dt><dd>{{ r.snapshot.averageSoreness === null ? '–' : i18n.number(r.snapshot.averageSoreness, 1, 1) + '/5' }}</dd></div>
              <div><dt>{{ t('reviews.mood') }}</dt><dd>{{ r.snapshot.averageMood === null ? '–' : i18n.number(r.snapshot.averageMood, 1, 1) + '/5' }}</dd></div>
              @for (b of r.snapshot.benchmarks; track b.testType) {
                <div>
                  <dt>{{ t(testLabel(b.testType)) }}</dt>
                  <dd>
                    {{ i18n.number(b.latest.value, 2, 0) }} {{ b.unit }}
                    @if (b.status === 'Reached') { · {{ t('performance.reached') }} }
                    @else if (b.goal && b.gapToGoal !== null) { · {{ t('performance.toGo', { value: i18n.number(b.gapToGoal, 2, 0), unit: b.unit }) }} }
                  </dd>
                </div>
              }
            </dl>
          </div>
        </article>
      } @empty {
        <p class="muted panel">{{ t('reviews.empty') }}</p>
      }
    }
  `,
})
export class ReviewsSection {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly toasts = inject(ToastService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly id = input.required<string>();

  protected readonly parts = PARTS;
  protected readonly weekOptions = [4, 12, 26];
  protected readonly weeks = signal(12);
  protected readonly reviews = signal<ProgressReview[]>([]);
  protected readonly draft = signal<Record<Part, string>>(emptyDraft());
  protected readonly error = signal<string | null>(null);
  protected readonly isCoach = computed(() => this.persona.current().role === 'Coach');
  protected readonly canRead = computed(() => ['Coach', 'Physio'].includes(this.persona.current().role));
  protected readonly hasText = computed(() => PARTS.some((p) => this.draft()[p].trim()));

  constructor() {
    effect(() => {
      const athleteId = Number(this.id());
      const canRead = this.canRead();
      untracked(() => {
        this.draft.set(emptyDraft());
        if (canRead) this.load(athleteId);
        else this.reviews.set([]);
      });
    });
  }

  protected partLabel(part: Part): TranslationKey {
    return `reviews.parts.${part}`;
  }

  protected testLabel(testType: string): TranslationKey {
    return `performance.tests.${testType}` as TranslationKey;
  }

  protected edit(part: Part, value: string): void {
    this.draft.update((d) => ({ ...d, [part]: value }));
  }

  protected save(event: Event): void {
    event.preventDefault();
    if (!this.hasText()) return;
    const d = this.draft();
    const athleteId = Number(this.id());
    this.api
      .addReview(athleteId, {
        weeks: this.weeks(),
        observations: d.observations.trim() || null,
        achievements: d.achievements.trim() || null,
        challenges: d.challenges.trim() || null,
        recommendations: d.recommendations.trim() || null,
      })
      .subscribe({
        next: () => {
          this.draft.set(emptyDraft());
          this.toasts.show(this.t('reviews.saved'));
          this.load(athleteId);
        },
        error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
      });
  }

  private load(athleteId: number): void {
    this.api.reviews(athleteId).subscribe({
      next: (r) => {
        this.reviews.set(r);
        this.error.set(null);
      },
      error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
    });
  }
}

function emptyDraft(): Record<Part, string> {
  return { observations: '', achievements: '', challenges: '', recommendations: '' };
}
