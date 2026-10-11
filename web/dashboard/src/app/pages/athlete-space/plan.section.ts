import { Component, computed, effect, inject, input, model, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../../api/api.service';
import { PlannedSession, PlannedSessionRequest, WeekPlan } from '../../api/models';
import { PersonaService } from '../../api/persona.service';
import { TranslationKey } from '../../i18n/catalogue';
import { I18n } from '../../i18n/i18n.service';
import { addDaysIso } from '../../shared/dates';
import { ToastService } from '../../shared/toast.service';

const SESSION_TYPES = ['Strength', 'Speed', 'Endurance', 'OnIce', 'Technique', 'Recovery'];
const MAX_SESSIONS_PER_DAY = 3;
/** A Monday, only used to name the weekdays of the standard week. */
const SOME_MONDAY = '2026-10-12';

interface DraftSession {
  type: string;
  minutes: number;
  rpe: number;
  note: string;
}

const toDraft = (s: PlannedSession): DraftSession => ({ type: s.type, minutes: s.durationMinutes, rpe: s.rpe, note: s.note ?? '' });
const toRequest = (s: DraftSession): PlannedSessionRequest => ({
  type: s.type,
  durationMinutes: s.minutes,
  rpe: s.rpe,
  note: s.note.trim() || null,
});

/** Editable rows of planned sessions (at most three a day), shared by the day and the standard-week editor. */
@Component({
  selector: 'app-session-rows',
  template: `
    @for (s of sessions(); track $index; let i = $index) {
      <div class="edit-session">
        <label>
          {{ t('plan.type') }}
          <select name="type" (change)="set(i, { type: $any($event.target).value })">
            @for (type of types; track type) {
              <option [value]="type" [selected]="type === s.type">{{ t(typeLabel(type)) }}</option>
            }
          </select>
        </label>
        <label>
          {{ t('plan.minutes') }}
          <input type="number" name="minutes" min="5" max="300" step="5" [value]="s.minutes" (input)="set(i, { minutes: +$any($event.target).value })" />
        </label>
        <label>
          RPE
          <input type="number" name="rpe" min="1" max="10" [value]="s.rpe" (input)="set(i, { rpe: +$any($event.target).value })" />
        </label>
        <button type="button" class="secondary remove-session" [attr.aria-label]="t('plan.removeSession')" (click)="remove(i)">✕</button>
        <label class="session-note-field">
          <span class="visually-hidden">{{ t('plan.note') }}</span>
          <input type="text" name="note" maxlength="200" [placeholder]="t('plan.note')" [value]="s.note" (input)="set(i, { note: $any($event.target).value })" />
        </label>
      </div>
    } @empty {
      <p class="muted">{{ t('plan.restDay') }}</p>
    }
    <button type="button" class="secondary add-session" [disabled]="sessions().length >= max" (click)="add()">{{ t('plan.addSession') }}</button>
  `,
})
export class SessionRows {
  protected readonly t = inject(I18n).t;
  readonly sessions = model.required<DraftSession[]>();
  protected readonly types = SESSION_TYPES;
  protected readonly max = MAX_SESSIONS_PER_DAY;

  protected typeLabel(type: string): TranslationKey {
    return `sessionType.${type}` as TranslationKey;
  }

  protected set(index: number, change: Partial<DraftSession>): void {
    this.sessions.update((list) => list.map((s, i) => (i === index ? { ...s, ...change } : s)));
  }

  protected add(): void {
    this.sessions.update((list) => [...list, { type: 'Strength', minutes: 60, rpe: 5, note: '' }]);
  }

  protected remove(index: number): void {
    this.sessions.update((list) => list.filter((_, i) => i !== index));
  }
}

/**
 * The weekly training plan (AC-83, ADR-14): the standard week with dated changes, planned against logged load.
 * The coach changes single days or the standard week; physio and nutrition read.
 */
@Component({
  selector: 'app-plan-section',
  imports: [SessionRows],
  template: `
    <section class="panel">
      <div class="plan-head">
        <div>
          <h2>{{ t('plan.title') }}</h2>
          <p class="muted">{{ t('plan.lead') }}</p>
        </div>
        @if (isCoach() && !template()) {
          <button type="button" class="secondary edit-template" (click)="openTemplate()">{{ t('plan.editTemplate') }}</button>
        }
      </div>
    </section>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }

    @if (template(); as days) {
      <form class="template-editor panel" (submit)="saveTemplate($event)">
        <h3>{{ t('plan.templateTitle') }}</h3>
        <p class="muted">{{ t('plan.templateLead') }}</p>
        <div class="template-days">
        @for (day of days; track day.dayOfWeek; let i = $index) {
          <fieldset class="template-day" [attr.data-day]="day.dayOfWeek">
            <legend>{{ weekdayOf(day.dayOfWeek) }}</legend>
            <app-session-rows [sessions]="day.sessions" (sessionsChange)="setTemplateDay(i, $event)" />
          </fieldset>
        }
        </div>
        <div class="dialog-actions">
          <button type="button" class="secondary" (click)="template.set(null)">{{ t('common.cancel') }}</button>
          <button type="submit" class="save-template">{{ t('plan.saveTemplate') }}</button>
        </div>
      </form>
    }

    @if (week(); as w) {
      <div class="plan-toolbar">
        <button type="button" class="secondary prev-week" [attr.aria-label]="t('plan.previousWeek')" (click)="move(-7)">‹</button>
        <strong>{{ t('plan.weekOf', { from: i18n.date(w.weekStart), to: i18n.date(lastDay(w)) }) }}</strong>
        <button type="button" class="secondary next-week" [attr.aria-label]="t('plan.nextWeek')" (click)="move(7)">›</button>
        <p class="plan-total">
          {{ t('plan.total', { planned: i18n.number(w.plannedLoad), logged: i18n.number(w.loggedLoad) }) }}
        </p>
      </div>

      <ol class="plan-days">
        @for (day of w.days; track day.date) {
          <li class="plan-day panel" [class.changed]="day.source === 'Changed'" [attr.data-date]="day.date">
            <h3>{{ weekday(day.date) }} <span class="muted">{{ i18n.date(day.date) }}</span></h3>
            @if (day.source === 'Changed') {
              <p class="plan-reason">{{ day.reason ? t('plan.changed', { reason: day.reason }) : t('plan.changedNoReason') }}</p>
            }

            @if (editing() === day.date) {
              <form class="day-editor" (submit)="saveDay($event, day.date)">
                <app-session-rows [(sessions)]="draft" />
                <label>
                  {{ t('plan.reason') }}
                  <input type="text" name="reason" maxlength="200" [value]="reason()" (input)="reason.set($any($event.target).value)" />
                </label>
                <div class="dialog-actions">
                  <button type="button" class="secondary" (click)="editing.set(null)">{{ t('common.cancel') }}</button>
                  <button type="submit" class="save-day">{{ t('plan.saveDay') }}</button>
                </div>
              </form>
            } @else {
              @if (day.sessions.length) {
                <ul class="plan-sessions">
                  @for (s of day.sessions; track $index) {
                    <li>
                      {{ t(typeLabel(s.type)) }} · {{ s.durationMinutes }} min · RPE {{ s.rpe }}
                      @if (s.note) { <span class="muted">· {{ s.note }}</span> }
                    </li>
                  }
                </ul>
              } @else {
                <p class="muted">{{ t('plan.restDay') }}</p>
              }
              <p class="plan-load" [class.over]="day.plannedLoad > 0 && day.loggedLoad > day.plannedLoad * 1.2">
                {{ t('plan.load', { planned: i18n.number(day.plannedLoad), logged: i18n.number(day.loggedLoad) }) }}
              </p>
              @if (isCoach()) {
                <div class="plan-actions">
                  <button type="button" class="secondary change-day" (click)="edit(day.date, day.sessions, day.reason)">{{ t('plan.changeDay') }}</button>
                  @if (day.source === 'Changed') {
                    <button type="button" class="secondary reset-day" (click)="reset(day.date)">{{ t('plan.resetDay') }}</button>
                  }
                </div>
              }
            }
          </li>
        }
      </ol>
    }
  `,
})
export class PlanSection {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly toasts = inject(ToastService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly id = input.required<string>();

  /** The Monday to show; null = this week (the API decides "today", spec section 4). */
  private readonly start = signal<string | null>(null);
  protected readonly week = signal<WeekPlan | null>(null);
  protected readonly editing = signal<string | null>(null);
  protected readonly draft = signal<DraftSession[]>([]);
  protected readonly reason = signal('');
  protected readonly template = signal<{ dayOfWeek: number; sessions: DraftSession[] }[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly isCoach = computed(() => this.persona.current().role === 'Coach');

  constructor() {
    effect(() => {
      const athleteId = Number(this.id());
      untracked(() => {
        this.start.set(null);
        this.editing.set(null);
        this.template.set(null);
        this.load(athleteId);
      });
    });
  }

  protected typeLabel(type: string): TranslationKey {
    return `sessionType.${type}` as TranslationKey;
  }

  protected weekday(date: string): string {
    return this.i18n.dateTime(`${date}T12:00:00Z`, { weekday: 'long', timeZone: 'UTC' });
  }

  protected weekdayOf(dayOfWeek: number): string {
    return this.weekday(addDaysIso(SOME_MONDAY, dayOfWeek - 1));
  }

  protected lastDay(w: WeekPlan): string {
    return addDaysIso(w.weekStart, 6);
  }

  protected move(days: number): void {
    const w = this.week();
    if (!w) return;
    this.start.set(addDaysIso(w.weekStart, days));
    this.editing.set(null);
    this.load(Number(this.id()));
  }

  protected edit(date: string, sessions: PlannedSession[], reason: string | null): void {
    this.draft.set(sessions.map(toDraft));
    this.reason.set(reason ?? '');
    this.editing.set(date);
  }

  protected saveDay(event: Event, date: string): void {
    event.preventDefault();
    const athleteId = Number(this.id());
    this.api
      .changePlanDay(athleteId, date, { reason: this.reason().trim() || null, sessions: this.draft().map(toRequest) })
      .subscribe({
        next: () => {
          this.editing.set(null);
          this.toasts.show(this.t('plan.daySaved'));
          this.load(athleteId);
        },
        error: (e) => this.fail(e),
      });
  }

  protected reset(date: string): void {
    const athleteId = Number(this.id());
    this.api.resetPlanDay(athleteId, date).subscribe({
      next: () => {
        this.toasts.show(this.t('plan.dayReset'));
        this.load(athleteId);
      },
      error: (e) => this.fail(e),
    });
  }

  protected openTemplate(): void {
    this.api.planTemplate(Number(this.id())).subscribe({
      next: (t) => this.template.set(t.days.map((d) => ({ dayOfWeek: d.dayOfWeek, sessions: d.sessions.map(toDraft) }))),
      error: (e) => this.fail(e),
    });
  }

  protected setTemplateDay(index: number, sessions: DraftSession[]): void {
    this.template.update((days) => days && days.map((d, i) => (i === index ? { ...d, sessions } : d)));
  }

  protected saveTemplate(event: Event): void {
    event.preventDefault();
    const days = this.template();
    if (!days) return;
    const athleteId = Number(this.id());
    this.api
      .setPlanTemplate(athleteId, days.map((d) => ({ dayOfWeek: d.dayOfWeek, sessions: d.sessions.map(toRequest) })))
      .subscribe({
        next: () => {
          this.template.set(null);
          this.toasts.show(this.t('plan.templateSaved'));
          this.load(athleteId);
        },
        error: (e) => this.fail(e),
      });
  }

  private load(athleteId: number): void {
    this.api.planWeek(athleteId, this.start()).subscribe({
      next: (w) => {
        this.week.set(w);
        this.error.set(null);
      },
      error: (e) => this.fail(e),
    });
  }

  private fail(e: unknown): void {
    this.error.set(describeError(e, this.persona.current().role, this.t));
  }
}
