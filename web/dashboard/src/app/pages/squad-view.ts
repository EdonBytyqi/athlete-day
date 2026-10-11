import { DashboardRow, ReadinessStatus, Sex } from '../api/models';

export type SortKey = 'name' | 'sport' | 'age' | 'sex' | 'todayLoad' | 'readiness';
export type SortDirection = 'asc' | 'desc';

export interface SquadSort {
  key: SortKey;
  direction: SortDirection;
}

export interface SquadFilter {
  /** Matches name or code, case-insensitive. */
  query: string;
  /** null = all sports. */
  sport: string | null;
}

// Ascending readiness lists the most urgent athletes first.
const READINESS_ORDER: Record<ReadinessStatus, number> = { CheckIn: 0, Missing: 1, Ok: 2 };
const SEX_ORDER: Record<Sex, number> = { Female: 0, Male: 1, NotStated: 2 };

const byName = (a: DashboardRow, b: DashboardRow) =>
  a.displayName.localeCompare(b.displayName, undefined, { sensitivity: 'base' });

const compare: Record<SortKey, (a: DashboardRow, b: DashboardRow) => number> = {
  name: byName,
  sport: (a, b) => a.sport.localeCompare(b.sport, undefined, { sensitivity: 'base' }),
  age: (a, b) => (a.age ?? 0) - (b.age ?? 0),
  sex: (a, b) => SEX_ORDER[a.sex] - SEX_ORDER[b.sex],
  todayLoad: (a, b) => a.todayLoad - b.todayLoad,
  readiness: (a, b) => READINESS_ORDER[a.readiness.status] - READINESS_ORDER[b.readiness.status],
};

/** Filters and sorts the squad. Unknown ages stay last in both directions; ties fall back to the name. */
export function viewSquad(rows: readonly DashboardRow[], filter: SquadFilter, sort: SquadSort): DashboardRow[] {
  const query = filter.query.trim().toLowerCase();
  const sign = sort.direction === 'asc' ? 1 : -1;

  return rows
    .filter((r) => filter.sport === null || r.sport === filter.sport)
    .filter((r) => !query || r.displayName.toLowerCase().includes(query) || r.code.toLowerCase().includes(query))
    .sort((a, b) => {
      if (sort.key === 'age' && (a.age === null) !== (b.age === null)) return a.age === null ? 1 : -1;
      return sign * compare[sort.key](a, b) || byName(a, b);
    });
}

/** The sports in the squad, alphabetically, for the filter chips. */
export function sportsOf(rows: readonly DashboardRow[]): string[] {
  return [...new Set(rows.map((r) => r.sport))].sort((a, b) => a.localeCompare(b));
}
