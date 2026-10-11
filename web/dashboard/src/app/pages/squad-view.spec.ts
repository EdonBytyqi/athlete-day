import { DashboardRow } from '../api/models';
import { sportsOf, viewSquad } from './squad-view';

const row = (athleteId: number, overrides: Partial<DashboardRow>): DashboardRow => ({
  athleteId,
  code: `ATH-00${athleteId}`,
  displayName: 'Nora',
  sport: 'Alpine skiing',
  age: 24,
  sex: 'Female',
  todayLoad: 0,
  acuteLoad: 0,
  chronicLoad: 0,
  acuteChronicRatio: null,
  wellness: null,
  readiness: { status: 'Ok', reasons: [] },
  restriction: 'None',
  nutrition: null,
  ...overrides,
});

const squad = [
  row(1, { displayName: 'Nora', sport: 'Alpine skiing', age: 24, sex: 'Female', todayLoad: 300 }),
  row(2, { displayName: 'elias', sport: 'Ice hockey', age: 19, sex: 'Male', todayLoad: 900, readiness: { status: 'CheckIn', reasons: ['x'] } }),
  row(3, { displayName: 'Lea', sport: 'Sprint', age: null, sex: 'NotStated', todayLoad: 0, readiness: { status: 'Missing', reasons: [] } }),
  row(4, { displayName: 'Jan', sport: 'Ice hockey', age: 31, sex: 'Male', todayLoad: 450 }),
];
const all = { query: '', sport: null };
const names = (rows: DashboardRow[]) => rows.map((r) => r.displayName);

describe('viewSquad', () => {
  it('[AC-38] sorts by name, sport, age, sex, load and readiness in both directions', () => {
    expect(names(viewSquad(squad, all, { key: 'name', direction: 'asc' }))).toEqual(['elias', 'Jan', 'Lea', 'Nora']);
    expect(names(viewSquad(squad, all, { key: 'name', direction: 'desc' }))).toEqual(['Nora', 'Lea', 'Jan', 'elias']);
    expect(names(viewSquad(squad, all, { key: 'sport', direction: 'asc' }))).toEqual(['Nora', 'elias', 'Jan', 'Lea']);
    // Unknown age stays last in either direction.
    expect(names(viewSquad(squad, all, { key: 'age', direction: 'asc' }))).toEqual(['elias', 'Nora', 'Jan', 'Lea']);
    expect(names(viewSquad(squad, all, { key: 'age', direction: 'desc' }))).toEqual(['Jan', 'Nora', 'elias', 'Lea']);
    expect(names(viewSquad(squad, all, { key: 'sex', direction: 'asc' }))).toEqual(['Nora', 'elias', 'Jan', 'Lea']);
    expect(names(viewSquad(squad, all, { key: 'todayLoad', direction: 'desc' }))).toEqual(['elias', 'Jan', 'Nora', 'Lea']);
    // Readiness ascending puts the most urgent first: check in, then no check-in, then ready.
    expect(names(viewSquad(squad, all, { key: 'readiness', direction: 'asc' }))).toEqual(['elias', 'Lea', 'Jan', 'Nora']);
  });

  it('[AC-39] filters by name or code and by sport', () => {
    const byName = { key: 'name', direction: 'asc' } as const;

    expect(names(viewSquad(squad, { query: 'NO', sport: null }, byName))).toEqual(['Nora']);
    expect(names(viewSquad(squad, { query: 'ath-004', sport: null }, byName))).toEqual(['Jan']);
    expect(names(viewSquad(squad, { query: '', sport: 'Ice hockey' }, byName))).toEqual(['elias', 'Jan']);
    expect(names(viewSquad(squad, { query: 'jan', sport: 'Sprint' }, byName))).toEqual([]);
    expect(sportsOf(squad)).toEqual(['Alpine skiing', 'Ice hockey', 'Sprint']);
  });
});
