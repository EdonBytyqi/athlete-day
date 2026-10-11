import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiService, describeError } from '../../api/api.service';
import { AthleteDetails, AthleteSummary, Sex } from '../../api/models';
import { PersonaService } from '../../api/persona.service';
import { TranslationKey } from '../../i18n/catalogue';
import { I18n } from '../../i18n/i18n.service';
import { logSessionUrl } from '../../shared/staff-url';

const SECTIONS = ['plan', 'performance', 'sessions', 'reviews', 'documents'] as const;

/**
 * One place per athlete (AC-66): header, a searchable selector grouped by sport, and the sections as child routes.
 * Switching athletes keeps the open section, so a coach can go through the squad section by section.
 */
@Component({
  selector: 'app-athlete-space',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <a routerLink="/dashboard" class="back">{{ t('athlete.back') }}</a>

    <header class="page-head space-head">
      <div>
        @if (details(); as d) {
          <h1>{{ d.displayName }}</h1>
          <p class="muted">
            {{ d.code }} · {{ d.sport }} · {{ age(d) }} · {{ t(sexText(d.sex)) }} ·
            {{ t('athlete.kg', { value: i18n.number(d.bodyMassKg, 1, 0) }) }}
          </p>
        } @else if (error(); as message) {
          <p class="banner error" role="alert">{{ message }}</p>
        } @else {
          <h1>{{ t('common.loading') }}</h1>
        }
      </div>
      <div class="space-tools">
        <div class="athlete-picker">
          <label>
            <span class="visually-hidden">{{ t('space.searchLabel') }}</span>
            <input
              type="search"
              class="athlete-search"
              [placeholder]="t('space.searchPlaceholder')"
              [value]="query()"
              (input)="query.set($any($event.target).value)"
            />
          </label>
          <label>
            <span class="visually-hidden">{{ t('space.selectLabel') }}</span>
            <select class="athlete-select" (change)="switchTo($any($event.target).value)">
              @if (!currentListed()) {
                <option value="" selected>{{ t('space.choose') }}</option>
              }
              @for (group of groups(); track group.sport) {
                <optgroup [label]="group.sport">
                  @for (a of group.athletes; track a.id) {
                    <option [value]="'' + a.id" [selected]="'' + a.id === id()">{{ a.displayName }} ({{ a.code }})</option>
                  }
                </optgroup>
              }
            </select>
          </label>
        </div>
        <a class="button secondary compare" routerLink="/versus" [queryParams]="{ a: id() }">{{ t('versus.compare') }}</a>
        @if (isCoach()) {
          <a class="button" [href]="logSessionUrl()" target="_blank" rel="noopener">{{ t('athlete.logSession') }}</a>
        }
      </div>
    </header>

    <nav class="sections tabs-inline" [attr.aria-label]="t('space.sectionsLabel')">
      <a [routerLink]="['/athletes', id()]" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">{{ t('space.sections.overview') }}</a>
      <a [routerLink]="['/athletes', id(), 'plan']" routerLinkActive="active">{{ t('space.sections.plan') }}</a>
      <a [routerLink]="['/athletes', id(), 'performance']" routerLinkActive="active">{{ t('space.sections.performance') }}</a>
      <a [routerLink]="['/athletes', id(), 'sessions']" routerLinkActive="active">{{ t('space.sections.sessions') }}</a>
      <a [routerLink]="['/athletes', id(), 'reviews']" routerLinkActive="active">{{ t('space.sections.reviews') }}</a>
      <a [routerLink]="['/athletes', id(), 'documents']" routerLinkActive="active">{{ t('space.sections.documents') }}</a>
    </nav>

    <router-outlet />
  `,
})
export class AthleteSpacePage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly id = input.required<string>();

  protected readonly details = signal<AthleteDetails | null>(null);
  protected readonly error = signal<string | null>(null);
  private readonly squad = signal<AthleteSummary[]>([]);
  protected readonly query = signal('');
  protected readonly isCoach = computed(() => this.persona.current().role === 'Coach');
  private readonly thisYear = new Date().getFullYear();

  /** The squad grouped by sport, both sorted by name, narrowed by the search. */
  protected readonly groups = computed(() => {
    const q = this.query().trim().toLowerCase();
    const matches = this.squad()
      .filter((a) => !q || a.displayName.toLowerCase().includes(q) || a.code.toLowerCase().includes(q))
      .sort((a, b) => a.displayName.localeCompare(b.displayName));
    const sports = [...new Set(matches.map((a) => a.sport))].sort((a, b) => a.localeCompare(b));
    return sports.map((sport) => ({ sport, athletes: matches.filter((a) => a.sport === sport) }));
  });

  /** When the search hides the current athlete, a placeholder keeps "picking the only match" a real change. */
  protected readonly currentListed = computed(() => this.groups().some((g) => g.athletes.some((a) => '' + a.id === this.id())));

  constructor() {
    this.api.athletes().subscribe({ next: (squad) => this.squad.set(squad), error: () => this.squad.set([]) });

    effect(() => {
      const athleteId = Number(this.id());
      untracked(() => {
        this.details.set(null);
        this.api.athlete(athleteId).subscribe({
          next: (d) => {
            this.details.set(d);
            this.error.set(null);
          },
          error: (e) => this.error.set(describeError(e, this.persona.current().role, this.t)),
        });
      });
    });
  }

  protected switchTo(value: string): void {
    if (!value) return;
    // Keep the open section: /athletes/1/performance → /athletes/2/performance.
    const section = SECTIONS.find((s) => this.router.url.split('?')[0].endsWith('/' + s));
    this.query.set('');
    this.router.navigate(section ? ['/athletes', value, section] : ['/athletes', value]);
  }

  protected logSessionUrl(): string {
    return logSessionUrl(Number(this.id()), this.i18n.lang());
  }

  protected age(d: AthleteDetails): string {
    return d.birthYear === null ? this.t('athlete.ageUnknown') : this.t('athlete.years', { n: this.thisYear - d.birthYear });
  }

  protected sexText(sex: Sex): TranslationKey {
    return `athlete.sexText.${sex}`;
  }
}
