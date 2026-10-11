import { Component, inject, signal } from '@angular/core';
import { ApiService, describeError } from '../api/api.service';
import { AthleteSummary, Sport } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { I18n } from '../i18n/i18n.service';
import { DocumentsPanel } from '../shared/documents-panel';
import { ToastService } from '../shared/toast.service';

/** Admin settings: the managed sports list (AC-63 – AC-65, ADR-10) and identity documents (AC-92, ADR-15). */
@Component({
  selector: 'app-settings-page',
  imports: [DocumentsPanel],
  template: `
    <header class="page-head">
      <div>
        <h1>{{ t('settings.title') }}</h1>
        <p class="muted">{{ t('settings.lead') }}</p>
      </div>
    </header>

    <section class="panel sports-settings">
      <h2>{{ t('settings.sports') }}</h2>

      <form class="add-sport-form" (submit)="add($event)">
        <label>
          <span class="visually-hidden">{{ t('settings.newSport') }}</span>
          <input name="newSport" autocomplete="off" [placeholder]="t('settings.newSport')" [value]="newName()" (input)="newName.set($any($event.target).value)" />
        </label>
        <button type="submit" class="add-sport" [disabled]="newName().trim().length < 2">{{ t('settings.add') }}</button>
      </form>

      @if (error(); as message) {
        <p class="banner error" role="alert">{{ message }}</p>
      }

      <ul class="sport-list">
        @for (s of sports(); track s.id) {
          <li class="sport-row" [class.retired]="s.retired">
            <input
              class="sport-name"
              [attr.aria-label]="t('settings.name', { name: s.name })"
              [value]="draft(s)"
              (input)="setDraft(s.id, $any($event.target).value)"
            />
            @if (s.retired) {
              <span class="tag">{{ t('settings.retiredTag') }}</span>
            }
            <span class="sport-actions">
              <button type="button" class="save secondary" [disabled]="draft(s).trim() === s.name" (click)="save(s, draft(s), s.retired)">
                {{ t('settings.save') }}
              </button>
              @if (s.retired) {
                <button type="button" class="reactivate secondary" (click)="save(s, s.name, false)">{{ t('settings.reactivate') }}</button>
              } @else {
                <button type="button" class="retire secondary" (click)="save(s, s.name, true)">{{ t('settings.retire') }}</button>
              }
            </span>
          </li>
        } @empty {
          <li class="muted">{{ t('settings.empty') }}</li>
        }
      </ul>
    </section>

    <section class="panel identity-documents">
      <h2>{{ t('settings.identityDocuments') }}</h2>
      <p class="muted">{{ t('settings.identityLead') }}</p>
      <label class="document-athlete-label">
        {{ t('space.selectLabel') }}
        <select class="document-athlete" (change)="documentAthlete.set(+$any($event.target).value || null)">
          <option value="" [selected]="documentAthlete() === null">{{ t('space.choose') }}</option>
          @for (a of athletes(); track a.id) {
            <option [value]="'' + a.id" [selected]="a.id === documentAthlete()">{{ a.displayName }} ({{ a.code }})</option>
          }
        </select>
      </label>
      @if (documentAthlete(); as athleteId) {
        <app-documents-panel [athleteId]="athleteId" />
      }
    </section>
  `,
})
export class SettingsPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly toasts = inject(ToastService);
  protected readonly t = inject(I18n).t;

  protected readonly sports = signal<Sport[]>([]);
  protected readonly athletes = signal<AthleteSummary[]>([]);
  protected readonly documentAthlete = signal<number | null>(null);
  protected readonly newName = signal('');
  protected readonly error = signal<string | null>(null);
  /** Unsaved renames, by sport id. */
  private readonly drafts = signal<Record<number, string>>({});

  constructor() {
    this.load();
    this.api.athletes().subscribe({
      next: (athletes) => this.athletes.set(athletes),
      error: (e) => this.error.set(this.describe(e)),
    });
  }

  protected draft(sport: Sport): string {
    return this.drafts()[sport.id] ?? sport.name;
  }

  protected setDraft(id: number, name: string): void {
    this.drafts.update((d) => ({ ...d, [id]: name }));
  }

  protected add(event: Event): void {
    event.preventDefault();
    const name = this.newName().trim();
    this.api.addSport(name).subscribe({
      next: (sport) => {
        this.newName.set('');
        this.toasts.show(this.t('settings.added', { name: sport.name }));
        this.load();
      },
      error: (e) => this.error.set(this.describe(e)),
    });
  }

  protected save(sport: Sport, name: string, retired: boolean): void {
    this.api.updateSport(sport.id, { name: name.trim(), retired }).subscribe({
      next: (saved) => {
        this.drafts.update(({ [sport.id]: _, ...rest }) => rest);
        const key = retired === sport.retired ? 'settings.saved' : retired ? 'settings.retiredToast' : 'settings.reactivated';
        this.toasts.show(this.t(key, { name: saved.name }));
        this.load();
      },
      error: (e) => this.error.set(this.describe(e)),
    });
  }

  private load(): void {
    this.api.sports().subscribe({
      next: (sports) => {
        this.sports.set(sports);
        this.error.set(null);
      },
      error: (e) => this.error.set(this.describe(e)),
    });
  }

  private describe(error: unknown): string {
    return describeError(error, this.persona.current().role, this.t);
  }
}
