import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService, describeError } from '../api/api.service';
import { AthleteDetails, DashboardRow, Sex, TrainingRestriction } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';
import { InfoTip } from '../shared/info-tip';
import { ModalDialog } from '../shared/modal-dialog';
import { ReadinessBadge } from '../shared/readiness-badge';
import { logSessionUrl } from '../shared/staff-url';
import { ToastService } from '../shared/toast.service';
import { AthleteForm } from './athlete-form';
import { SortKey, SquadSort, sportsOf, viewSquad } from './squad-view';

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, ReadinessBadge, InfoTip, ModalDialog, AthleteForm],
  templateUrl: './dashboard.page.html',
})
export class DashboardPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);
  private readonly toasts = inject(ToastService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  protected readonly rows = signal<DashboardRow[] | null>(null);
  protected readonly error = signal<string | null>(null);

  // ---- finding athletes ----
  protected readonly query = signal('');
  protected readonly sport = signal<string | null>(null);
  protected readonly sort = signal<SquadSort>({ key: 'name', direction: 'asc' });
  protected readonly sortOptions: SortKey[] = ['name', 'sport', 'age', 'sex', 'todayLoad', 'readiness'];
  protected readonly sports = computed(() => sportsOf(this.rows() ?? []));
  protected readonly visible = computed(() =>
    viewSquad(this.rows() ?? [], { query: this.query(), sport: this.sport() }, this.sort()),
  );
  protected readonly filtered = computed(() => this.query().trim() !== '' || this.sport() !== null);

  protected readonly checkIns = computed(() => this.rows()?.filter((r) => r.readiness.status === 'CheckIn').length ?? 0);
  protected readonly missing = computed(() => this.rows()?.filter((r) => r.wellness === null).length ?? 0);

  // ---- squad management (coach) ----
  protected readonly isCoach = computed(() => this.persona.current().role === 'Coach');
  /** One sentence per role: what this page is for, in plain words. */
  protected readonly hint = computed(() => {
    const role = this.persona.current().role;
    return ['Coach', 'Physio', 'Nutrition'].includes(role) ? this.t(`squad.hint.${role}` as TranslationKey) : null;
  });
  /** 'new' opens an empty form; an athlete opens it for editing. */
  protected readonly editing = signal<AthleteDetails | 'new' | null>(null);
  protected readonly editingAthlete = computed(() => {
    const e = this.editing();
    return e === 'new' ? null : e;
  });
  protected readonly archiving = signal<DashboardRow | null>(null);
  protected readonly archived = signal<AthleteDetails[] | null>(null);

  constructor() {
    // Reload when the role or language changes (server texts such as readiness reasons are translated
    // by the API), or when the API pushes a change.
    effect(() => {
      const role = this.persona.current().role;
      this.i18n.lang();
      this.realtime.version();
      untracked(() => this.load(role));
    });
  }

  protected logSessionUrl(athleteId: number): string {
    return logSessionUrl(athleteId, this.i18n.lang());
  }

  protected sortKeyLabel(key: SortKey): TranslationKey {
    return `squad.sortKey.${key}`;
  }

  protected sexLabel(sex: Sex): TranslationKey {
    return `sex.${sex}`;
  }

  protected restrictionLabel(restriction: TrainingRestriction): TranslationKey {
    return `restriction.${restriction}`;
  }

  protected sortBy(key: SortKey): void {
    this.sort.update((s) =>
      s.key === key ? { key, direction: s.direction === 'asc' ? 'desc' : 'asc' } : { key, direction: 'asc' },
    );
  }

  protected sortFromSelect(value: string): void {
    const [key, direction] = value.split(':') as [SortKey, 'asc' | 'desc'];
    this.sort.set({ key, direction });
  }

  protected ariaSort(key: SortKey): 'ascending' | 'descending' | null {
    const s = this.sort();
    return s.key === key ? (s.direction === 'asc' ? 'ascending' : 'descending') : null;
  }

  protected toggleSport(sport: string): void {
    this.sport.update((current) => (current === sport ? null : sport));
  }

  protected clearFilters(): void {
    this.query.set('');
    this.sport.set(null);
  }

  protected edit(row: DashboardRow): void {
    this.api.athlete(row.athleteId).subscribe({
      next: (athlete) => this.editing.set(athlete),
      error: (e) => this.toasts.show(this.describe(e)),
    });
  }

  protected saved({ athlete, created }: { athlete: AthleteDetails; created: boolean }): void {
    this.editing.set(null);
    this.reload();
    this.toasts.show(
      created
        ? this.t('squad.addedToast', { name: athlete.displayName, code: athlete.code })
        : this.t('squad.savedToast', { name: athlete.displayName }),
    );
  }

  protected confirmArchive(): void {
    const row = this.archiving();
    if (!row) return;
    this.api.archiveAthlete(row.athleteId).subscribe({
      next: () => {
        this.archiving.set(null);
        this.reload();
        this.toasts.show(this.t('squad.archivedToast', { name: row.displayName }), {
          label: this.t('squad.undo'),
          run: () => this.restore(row.athleteId, row.displayName),
        });
      },
      error: (e) => {
        this.archiving.set(null);
        this.toasts.show(this.describe(e));
      },
    });
  }

  protected restore(athleteId: number, name: string): void {
    this.api.restoreAthlete(athleteId).subscribe({
      next: () => {
        this.reload();
        this.toasts.show(this.t('squad.restoredToast', { name }));
      },
      error: (e) => this.toasts.show(this.describe(e)),
    });
  }

  protected closeMenu(menu: HTMLElement): void {
    // jsdom (unit tests) has no Popover API.
    if (typeof menu.hidePopover === 'function' && menu.matches(':popover-open')) menu.hidePopover();
  }

  protected onArchiveToggle(event: Event): void {
    if ((event.target as HTMLDetailsElement).open) this.loadArchived();
  }

  private describe(error: unknown): string {
    return describeError(error, this.persona.current().role, this.t);
  }

  private reload(): void {
    this.load(this.persona.current().role);
    if (this.archived() !== null) this.loadArchived();
  }

  private loadArchived(): void {
    this.api.archivedAthletes().subscribe({ next: (a) => this.archived.set(a), error: () => this.archived.set([]) });
  }

  private load(role: string): void {
    this.api.dashboard().subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.error.set(null);
      },
      error: (e) => {
        this.rows.set(null);
        this.error.set(describeError(e, role, this.t));
      },
    });
  }
}
