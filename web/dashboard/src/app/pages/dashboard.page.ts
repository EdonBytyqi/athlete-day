import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService, describeError } from '../api/api.service';
import { AthleteDetails, DashboardRow, Role, Sex } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';
import { InfoTip } from '../shared/info-tip';
import { ModalDialog } from '../shared/modal-dialog';
import { ReadinessBadge } from '../shared/readiness-badge';
import { logSessionUrl } from '../shared/staff-url';
import { ToastService } from '../shared/toast.service';
import { AthleteForm } from './athlete-form';
import { SortKey, SquadSort, sportsOf, viewSquad } from './squad-view';

/** One sentence per role: what this page is for, in plain words. */
const ROLE_HINTS: Partial<Record<Role, string>> = {
  Coach:
    'Your squad today. Orange rows need a short conversation before training; click a name for the 28-day trend. Use ⋯ to log a session, edit or archive.',
  Physio: 'The squad today. Restrictions you set show as tags; click a name for the trend and your notes.',
  Nutrition: "The squad today. The last column shows today's energy and carbohydrate targets you set for the kitchen.",
};

const SEX_LABELS: Record<Sex, string> = { Female: 'Female', Male: 'Male', NotStated: '–' };

const SORT_LABELS: Record<SortKey, string> = {
  name: 'name',
  sport: 'sport',
  age: 'age',
  sex: 'sex',
  todayLoad: 'load today',
  readiness: 'readiness',
};

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, DecimalPipe, ReadinessBadge, InfoTip, ModalDialog, AthleteForm],
  templateUrl: './dashboard.page.html',
})
export class DashboardPage {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly realtime = inject(RealtimeService);
  private readonly toasts = inject(ToastService);

  protected readonly rows = signal<DashboardRow[] | null>(null);
  protected readonly error = signal<string | null>(null);

  // ---- finding athletes ----
  protected readonly query = signal('');
  protected readonly sport = signal<string | null>(null);
  protected readonly sort = signal<SquadSort>({ key: 'name', direction: 'asc' });
  protected readonly sports = computed(() => sportsOf(this.rows() ?? []));
  protected readonly visible = computed(() =>
    viewSquad(this.rows() ?? [], { query: this.query(), sport: this.sport() }, this.sort()),
  );
  protected readonly filtered = computed(() => this.query().trim() !== '' || this.sport() !== null);

  protected readonly checkIns = computed(() => this.rows()?.filter((r) => r.readiness.status === 'CheckIn').length ?? 0);
  protected readonly missing = computed(() => this.rows()?.filter((r) => r.wellness === null).length ?? 0);

  // ---- squad management (coach) ----
  protected readonly isCoach = computed(() => this.persona.current().role === 'Coach');
  protected readonly hint = computed(() => ROLE_HINTS[this.persona.current().role] ?? null);
  /** 'new' opens an empty form; an athlete opens it for editing. */
  protected readonly editing = signal<AthleteDetails | 'new' | null>(null);
  protected readonly editingAthlete = computed(() => {
    const e = this.editing();
    return e === 'new' ? null : e;
  });
  protected readonly archiving = signal<DashboardRow | null>(null);
  protected readonly archived = signal<AthleteDetails[] | null>(null);

  protected readonly sexLabels = SEX_LABELS;
  protected readonly sortLabels = SORT_LABELS;
  protected readonly logSessionUrl = logSessionUrl;

  constructor() {
    // Reload when the role changes or the API pushes a change.
    effect(() => {
      const role = this.persona.current().role;
      this.realtime.version();
      untracked(() => this.load(role));
    });
  }

  protected sortBy(key: SortKey): void {
    this.sort.update((s) =>
      s.key === key ? { key, direction: s.direction === 'asc' ? 'desc' : 'asc' } : { key, direction: 'asc' },
    );
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
      error: (e) => this.toasts.show(describeError(e, this.persona.current().role)),
    });
  }

  protected saved({ athlete, created }: { athlete: AthleteDetails; created: boolean }): void {
    this.editing.set(null);
    this.reload();
    this.toasts.show(created ? `${athlete.displayName} added as ${athlete.code}.` : `${athlete.displayName} saved.`);
  }

  protected confirmArchive(): void {
    const row = this.archiving();
    if (!row) return;
    this.api.archiveAthlete(row.athleteId).subscribe({
      next: () => {
        this.archiving.set(null);
        this.reload();
        this.toasts.show(`${row.displayName} archived.`, {
          label: 'Undo',
          run: () => this.restore(row.athleteId, row.displayName),
        });
      },
      error: (e) => {
        this.archiving.set(null);
        this.toasts.show(describeError(e, this.persona.current().role));
      },
    });
  }

  protected restore(athleteId: number, name: string): void {
    this.api.restoreAthlete(athleteId).subscribe({
      next: () => {
        this.reload();
        this.toasts.show(`${name} is back in the squad.`);
      },
      error: (e) => this.toasts.show(describeError(e, this.persona.current().role)),
    });
  }

  protected closeMenu(menu: HTMLElement): void {
    // jsdom (unit tests) has no Popover API.
    if (typeof menu.hidePopover === 'function' && menu.matches(':popover-open')) menu.hidePopover();
  }

  protected onArchiveToggle(event: Event): void {
    if ((event.target as HTMLDetailsElement).open) this.loadArchived();
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
        this.error.set(describeError(e, role));
      },
    });
  }
}
