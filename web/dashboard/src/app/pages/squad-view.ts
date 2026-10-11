import { DashboardRow, ReadinessStatus, Sex } from '../api/models';

export type SortKey =
  | 'name'
  | 'sport'
  | 'age'
  | 'sex'
  | 'todayLoad'
  | 'acuteLoad'
  | 'chronicLoad'
  | 'ratio'
  | 'readiness';
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
  acuteLoad: (a, b) => a.acuteLoad - b.acuteLoad,
  chronicLoad: (a, b) => a.chronicLoad - b.chronicLoad,
  ratio: (a, b) => (a.acuteChronicRatio ?? 0) - (b.acuteChronicRatio ?? 0),
  readiness: (a, b) => READINESS_ORDER[a.readiness.status] - READINESS_ORDER[b.readiness.status],
};

/** Values that may be unknown; unknown ones stay last in both directions. */
const OPTIONAL: Partial<Record<SortKey, (r: DashboardRow) => unknown>> = {
  age: (r) => r.age,
  ratio: (r) => r.acuteChronicRatio,
};

/** Filters and sorts the squad. Unknown ages and ratios stay last in both directions; ties fall back to the name. */
export function viewSquad(rows: readonly DashboardRow[], filter: SquadFilter, sort: SquadSort): DashboardRow[] {
  const query = filter.query.trim().toLowerCase();
  const sign = sort.direction === 'asc' ? 1 : -1;

  return rows
    .filter((r) => filter.sport === null || r.sport === filter.sport)
    .filter((r) => !query || r.displayName.toLowerCase().includes(query) || r.code.toLowerCase().includes(query))
    .sort((a, b) => {
      const optional = OPTIONAL[sort.key];
      if (optional && (optional(a) === null) !== (optional(b) === null)) return optional(a) === null ? 1 : -1;
      return sign * compare[sort.key](a, b) || byName(a, b);
    });
}

/** The sports in the squad, alphabetically, for the filter chips. */
export function sportsOf(rows: readonly DashboardRow[]): string[] {
  return [...new Set(rows.map((r) => r.sport))].sort((a, b) => a.localeCompare(b));
}
