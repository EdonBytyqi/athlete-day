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
import { addDaysIso, daysInRange, todayIso } from '../shared/dates';
import { logSessionUrl } from '../shared/staff-url';
import { ToastService } from '../shared/toast.service';
import { AthleteForm } from './athlete-form';
import { LoadCompare } from './load-compare';
import { SortKey, SquadSort, sportsOf, viewSquad } from './squad-view';

type Preset = 'today' | 'week' | 'month';

const PRESETS: { key: Preset; days: number; label: TranslationKey }[] = [
  { key: 'today', days: 1, label: 'range.today' },
  { key: 'week', days: 7, label: 'range.week' },
  { key: 'month', days: 28, label: 'range.month' },
];

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, ReadinessBadge, InfoTip, ModalDialog, AthleteForm, LoadCompare],
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
  protected readonly sortOptions: SortKey[] = ['name', 'sport', 'age', 'sex', 'todayLoad', 'acuteLoad', 'chronicLoad', 'ratio', 'readiness'];
  protected readonly sports = computed(() => sportsOf(this.rows() ?? []));
  protected readonly visible = computed(() =>
    viewSquad(this.rows() ?? [], { query: this.query(), sport: this.sport() }, this.sort()),
  );
  protected readonly filtered = computed(() => this.query().trim() !== '' || this.sport() !== null);

  // ---- period (AC-58 – AC-60): figures are as of the end date, totals cover the whole period ----
  protected readonly today = todayIso();
  protected readonly period = signal({ from: this.today, to: this.today });
  protected readonly isRange = computed(() => this.period().from !== this.period().to);
  protected readonly preset = computed<Preset | null>(() => {
    const { from, to } = this.period();
    if (to !== this.today) return null;
    return PRESETS.find((p) => p.days === daysInRange(from, to))?.key ?? null;
  });
  protected readonly presets = PRESETS;
  protected readonly periodLabel = computed(() => {
    const { from, to } = this.period();
    const date = (iso: string) => this.i18n.date(iso);
    if (from === to) return to === this.today ? this.t('range.todayLabel', { date: date(to) }) : this.t('range.dayLabel', { date: date(to) });
    return this.t('range.periodLabel', { from: date(from), to: date(to), days: daysInRange(from, to) });
  });
  protected readonly title = computed(() =>
    this.period().to === this.today ? this.t('squad.title') : this.t('squad.titleOn', { date: this.i18n.date(this.period().to) }),
  );
  protected readonly loadColumn = computed(() =>
    this.period().to === this.today ? this.t('squad.col.loadToday') : this.t('squad.col.loadOn', { date: this.i18n.date(this.period().to) }),
  );

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
    // Reload when the role, the period or the language changes (server texts such as readiness reasons are
    // translated by the API), or when the API pushes a change.
    effect(() => {
      const role = this.persona.current().role;
      this.period();
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

  protected setPreset(days: number): void {
    this.period.set({ from: addDaysIso(this.today, -(days - 1)), to: this.today });
  }

  protected setFrom(value: string): void {
    if (!value) return;
    const to = this.period().to;
    this.period.set({ from: value, to: value > to ? value : to });
  }

  protected setTo(value: string): void {
    if (!value) return;
    const to = value > this.today ? this.today : value;
    const from = this.period().from;
    this.period.set({ from: from > to ? to : from, to });
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
    // Today only: no parameters, so the request stays the plain default the API documents.
    const period = this.period();
    const today = period.from === this.today && period.to === this.today;
    this.api.dashboard(today ? undefined : period).subscribe({
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
