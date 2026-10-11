import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiService, describeError } from '../../api/api.service';
import { SessionNote, TrainingSession } from '../../api/models';
import { PersonaService } from '../../api/persona.service';
import { TranslationKey } from '../../i18n/catalogue';
import { I18n } from '../../i18n/i18n.service';
import { ToastService } from '../../shared/toast.service';

/** Recent sessions with the coach's notes (AC-74). Coach writes, coach and physio read (ADR-12). */
@Component({
  selector: 'app-sessions-section',
  template: `
    <section class="panel">
      <h2>{{ t('sessionsSection.title') }}</h2>
      <p class="muted">{{ t('sessionsSection.lead') }}</p>
      @if (!canReadNotes()) {
        <p class="notice notes-restricted">{{ t('sessionsSection.restricted') }}</p>
      }
    </section>

    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }

    <ol class="session-list">
      @for (s of sessions(); track s.id) {
        <li class="session panel" [attr.data-session]="s.id">
          <p class="session-line">
            <strong>{{ i18n.date(s.date) }}</strong> · {{ t(sessionType(s.type)) }} · {{ s.durationMinutes }} min · RPE {{ s.rpe }} ·
            {{ i18n.number(s.load) }} AU
          </p>
          @for (n of notesFor(s.id); track n.id) {
            <blockquote class="session-note">
              <p>{{ n.text }}</p>
              <footer class="muted">{{ t('sessionsSection.noteMeta', { author: n.author, date: i18n.dateTime(n.createdAt, { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) }) }}</footer>
            </blockquote>
          }
          @if (isCoach()) {
            <form class="note-form" (submit)="addNote($event, s)">
              <label class="visually-hidden" [attr.for]="'note-' + s.id">{{ t('sessionsSection.noteLabel', { date: i18n.date(s.date) }) }}</label>
              <textarea
                [id]="'note-' + s.id"
                rows="2"
                maxlength="1000"
                [placeholder]="t('sessionsSection.notePlaceholder')"
                [value]="drafts()[s.id] ?? ''"
                (input)="setDraft(s.id, $any($event.target).value)"
              ></textarea>
              <button type="submit" class="secondary add-note" [disabled]="!(drafts()[s.id] ?? '').trim()">{{ t('sessionsSection.addNote') }}</button>
            </form>
          }
        </li>
      } @empty {
        <li class="muted panel">{{ t('sessionsSection.empty') }}</li>
      }
    </ol>
  `,
})
export class SessionsSection {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly toasts = inject(ToastService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly id = input.required<string>();

  protected readonly sessions = signal<TrainingSession[]>([]);
  private readonly notes = signal<SessionNote[]>([]);
  protected readonly drafts = signal<Record<number, string>>({});
  protected readonly error = signal<string | null>(null);
  protected readonly isCoach = computed(() => this.persona.current().role === 'Coach');
  protected readonly canReadNotes = computed(() => ['Coach', 'Physio'].includes(this.persona.current().role));

  constructor() {
    effect(() => {
      const athleteId = Number(this.id());
      const canRead = this.canReadNotes();
      untracked(() => {
        this.drafts.set({});
        this.api.sessions(athleteId).subscribe({
          next: (s) => this.sessions.set(s),
          error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
        });
        if (canRead) this.loadNotes(athleteId);
        else this.notes.set([]);
      });
    });
  }

  protected notesFor(sessionId: number): SessionNote[] {
    return this.notes().filter((n) => n.sessionId === sessionId);
  }

  protected sessionType(type: string): TranslationKey {
    return `sessionType.${type}` as TranslationKey;
  }

  protected setDraft(sessionId: number, text: string): void {
    this.drafts.update((d) => ({ ...d, [sessionId]: text }));
  }

  protected addNote(event: Event, session: TrainingSession): void {
    event.preventDefault();
    const text = (this.drafts()[session.id] ?? '').trim();
    if (!text) return;
    this.api.addSessionNote(session.id, text).subscribe({
      next: () => {
        this.drafts.update(({ [session.id]: _, ...rest }) => rest);
        this.toasts.show(this.t('sessionsSection.noteSaved'));
        this.loadNotes(Number(this.id()));
      },
      error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
    });
  }

  private loadNotes(athleteId: number): void {
    this.api.sessionNotes(athleteId).subscribe({ next: (n) => this.notes.set(n), error: () => this.notes.set([]) });
  }
}
