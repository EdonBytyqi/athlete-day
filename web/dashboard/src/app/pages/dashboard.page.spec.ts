import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { DashboardPage } from './dashboard.page';
import { DashboardRow } from '../api/models';
import { personaInterceptor } from '../api/persona.interceptor';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';
import { ToastService } from '../shared/toast.service';
import { I18n } from '../i18n/i18n.service';
import { addDaysIso, todayIso } from '../shared/dates';

const sportsList = [
  { id: 4, name: 'Rowing', retired: false },
  { id: 2, name: 'Ice hockey', retired: false },
  { id: 9, name: 'Fencing', retired: true },
];

const row = (overrides: Partial<DashboardRow>): DashboardRow => ({
  athleteId: 1,
  code: 'ATH-001',
  displayName: 'Nora',
  sport: 'Alpine skiing',
  age: 24,
  sex: 'Female',
  todayLoad: 420,
  acuteLoad: 380,
  chronicLoad: 400,
  acuteChronicRatio: 0.95,
  wellness: { date: '2026-10-10', sleepHours: 8, soreness: 2, mood: 4 },
  readiness: { status: 'Ok', reasons: [] },
  restriction: 'None',
  nutrition: { energyKcal: 3200, carbsG: 396 },
  range: { from: '2026-10-11', to: '2026-10-11', days: 1, totalLoad: 0, averageDailyLoad: 0, sessions: 0, wellnessDays: 0, averageSleep: null, averageSoreness: null, averageMood: null },
  ...overrides,
});

const squad = [
  row({}),
  row({ athleteId: 2, code: 'ATH-002', displayName: 'Elias', sport: 'Ice hockey', age: 19, sex: 'Male' }),
  row({ athleteId: 3, code: 'ATH-003', displayName: 'Lea', sport: 'Sprint', age: 31, sex: 'Female' }),
];

describe('DashboardPage', () => {
  let controller: HttpTestingController;
  const version = signal(0);

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([personaInterceptor])),
        provideHttpClientTesting(),
        { provide: RealtimeService, useValue: { version } },
      ],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  async function render(rows: DashboardRow[] = squad) {
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    controller.expectOne('/api/dashboard').flush(rows);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const click = async (target: Element | null | undefined) => {
      (target as HTMLElement).click();
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const choose = async (selector: string, value: string) => {
      const select = el.querySelector<HTMLSelectElement>(selector)!;
      select.value = value;
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const type = async (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      await fixture.whenStable();
    };
    return { fixture, el, click, type, choose };
  }

  const names = (el: HTMLElement) =>
    [...el.querySelectorAll('tbody tr .athlete strong')].map((n) => n.textContent?.trim());
  const header = (el: HTMLElement, label: string) =>
    [...el.querySelectorAll('th')].find((th) => th.querySelector('button.sort')?.textContent?.trim() === label);

  it('[AC-15] lists athletes and counts check-ins', async () => {
    const { el } = await render([
      row({}),
      row({
        athleteId: 2,
        code: 'ATH-002',
        displayName: 'Elias',
        acuteChronicRatio: 1.72,
        readiness: { status: 'CheckIn', reasons: ['7-day load well above 28-day average (ratio 1.72)'] },
      }),
    ]);

    expect(el.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(el.querySelector('.stat.warn strong')?.textContent).toBe('1');
    expect(el.querySelector('tr.flagged')?.textContent).toContain('ratio 1.72');
    expect(el.textContent).toContain('not an injury predictor');
  });

  it('[AC-28] explains a 403 as need-to-know access instead of failing', async () => {
    TestBed.inject(PersonaService).switchTo('luca');
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();

    controller.expectOne('/api/dashboard').flush(null, { status: 403, statusText: 'Forbidden' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('.banner.error')?.textContent).toContain(
      'Not available for the Kitchen role',
    );
  });

  it('[AC-31] reloads when the API pushes a change', async () => {
    const { fixture } = await render([row({})]);

    version.update((v) => v + 1);
    fixture.detectChanges();
    await fixture.whenStable();

    controller.expectOne('/api/dashboard').flush([row({ todayLoad: 900 })]);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('900');
  });

  it('[AC-38] sorts by a clicked column header and reverses on a second click', async () => {
    const { el, click } = await render();

    expect(names(el)).toEqual(['Elias', 'Lea', 'Nora']);
    expect(header(el, 'Athlete')?.getAttribute('aria-sort')).toBe('ascending');

    await click(header(el, 'Age')?.querySelector('button.sort'));
    expect(names(el)).toEqual(['Elias', 'Nora', 'Lea']);
    expect(header(el, 'Age')?.getAttribute('aria-sort')).toBe('ascending');
    expect(header(el, 'Athlete')?.hasAttribute('aria-sort')).toBe(false);

    await click(header(el, 'Age')?.querySelector('button.sort'));
    expect(names(el)).toEqual(['Lea', 'Nora', 'Elias']);
    expect(header(el, 'Age')?.getAttribute('aria-sort')).toBe('descending');

    for (const label of ['Sport', 'Sex', 'Load today', 'Readiness']) {
      expect(header(el, label), label).toBeTruthy();
    }
  });

  it('[AC-39] filters by search and sport, and offers to clear when nothing matches', async () => {
    const { el, click, type } = await render();

    await type('input[type="search"]', 'ath-002');
    expect(names(el)).toEqual(['Elias']);

    await type('input[type="search"]', '');
    const chip = [...el.querySelectorAll('.chips button')].find((b) => b.textContent?.trim() === 'Sprint');
    await click(chip);
    expect(chip?.getAttribute('aria-pressed')).toBe('true');
    expect(names(el)).toEqual(['Lea']);

    await type('input[type="search"]', 'nora');
    expect(names(el)).toEqual([]);
    expect(el.querySelector('.empty')?.textContent).toContain('No athletes match');

    await click(el.querySelector('.empty button'));
    expect(names(el)).toEqual(['Elias', 'Lea', 'Nora']);
    expect(el.querySelector<HTMLInputElement>('input[type="search"]')?.value).toBe('');
  });

  it('[AC-40] uses plain-language headers with keyboard-reachable explanations', async () => {
    const { el } = await render();

    for (const label of ['Load today', 'Week average', 'Month average', 'Week vs month']) {
      const th = [...el.querySelectorAll('th')].find((h) => h.textContent?.includes(label));
      expect(th, label).toBeTruthy();
      const tip = th!.querySelector<HTMLButtonElement>('button.info-tip');
      expect(tip, label).toBeTruthy();
      const text = el.querySelector(`#${tip!.getAttribute('aria-describedby')}`);
      expect(text?.getAttribute('role')).toBe('tooltip');
      expect(text?.textContent?.length).toBeGreaterThan(20);
    }
    expect(el.textContent).toContain('RPE × minutes');
  });

  it('[AC-41] shows squad management to the coach only', async () => {
    const coach = await render();
    expect(coach.el.querySelector('button.add-athlete')).toBeTruthy();
    expect(coach.el.querySelectorAll('tbody .row-actions')).toHaveLength(3);
    for (const action of ['a.log-session', 'a.details', 'button.edit', 'button.archive']) {
      expect(coach.el.querySelector(`tbody tr ${action}`), action).toBeTruthy();
    }

    TestBed.inject(PersonaService).switchTo('jonas');
    coach.fixture.detectChanges();
    await coach.fixture.whenStable();
    controller.expectOne('/api/dashboard').flush(squad);
    await coach.fixture.whenStable();

    expect(coach.el.querySelector('button.add-athlete')).toBeNull();
    expect(coach.el.querySelector('.row-actions')).toBeNull();
  });

  it('[AC-41] asks before archiving and offers undo afterwards', async () => {
    const { el, click, fixture } = await render();
    const toasts = TestBed.inject(ToastService);
    const nora = [...el.querySelectorAll('tbody tr')].find((tr) => tr.textContent?.includes('Nora'))!;

    await click(nora.querySelector('button.archive'));
    const dialog = el.querySelector('dialog.confirm');
    expect(dialog?.hasAttribute('open')).toBe(true);
    expect(dialog?.textContent).toContain('Archive Nora?');

    await click(dialog!.querySelector('button.confirm-archive'));
    const archive = controller.expectOne('/api/athletes/1');
    expect(archive.request.method).toBe('DELETE');
    archive.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    controller.expectOne('/api/dashboard').flush(squad.slice(1));
    await fixture.whenStable();

    expect(toasts.current()?.message).toContain('Nora archived');
    expect(toasts.current()?.actionLabel).toBe('Undo');

    toasts.act();
    const restore = controller.expectOne('/api/athletes/1/restore');
    expect(restore.request.method).toBe('POST');
  });

  it('[AC-41] adds an athlete through the form', async () => {
    const { el, click, type, choose, fixture } = await render();

    await click(el.querySelector('button.add-athlete'));
    const dialog = el.querySelector('dialog.athlete-form')!;
    expect(dialog.hasAttribute('open')).toBe(true);
    expect(dialog.querySelector('h2')?.textContent).toContain('Add athlete');

    await type('dialog.athlete-form input[name="athlete.displayName"]', 'Ada');
    controller.expectOne('/api/sports').flush(sportsList);
    await fixture.whenStable();
    const sportOptions = [...dialog.querySelectorAll('select[name="athlete.sportId"] option')].map((o) => o.textContent?.trim());
    expect(sportOptions).toEqual(['Choose a sport…', 'Ice hockey', 'Rowing']);
    await choose('dialog.athlete-form select[name="athlete.sportId"]', '4');
    await type('dialog.athlete-form input[name="athlete.birthYear"]', '2001');
    await type('dialog.athlete-form input[name="athlete.bodyMassKg"]', '68.5');
    await click(dialog.querySelector('button[type="submit"]'));

    const create = controller.expectOne('/api/athletes');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toMatchObject({ displayName: 'Ada', sportId: 4, birthYear: 2001, bodyMassKg: 68.5 });
    create.flush({ id: 9, code: 'ATH-009', displayName: 'Ada', sportId: 4, sport: 'Rowing', birthYear: 2001, sex: 'NotStated', bodyMassKg: 68.5, archivedAt: null }, { status: 201, statusText: 'Created' });
    await fixture.whenStable();

    expect(TestBed.inject(ToastService).current()?.message).toContain('Ada added as ATH-009');
  });

  it('[AC-60] loads the chosen period and says which period the figures refer to', async () => {
    const { el, click, fixture } = await render();
    const today = todayIso();
    expect(el.querySelector('.period')?.textContent).toContain(TestBed.inject(I18n).date(today));

    const week = [...el.querySelectorAll('.range-presets button')].find((b) => b.textContent?.trim() === 'Last 7 days');
    await click(week);
    const request = controller.expectOne((r) => r.url === '/api/dashboard');
    expect([request.request.params.get('from'), request.request.params.get('to')]).toEqual([addDaysIso(today, -6), today]);
    request.flush(squad.map((r) => ({ ...r, range: { ...r.range, from: addDaysIso(today, -6), to: today, days: 7, totalLoad: 2100, averageDailyLoad: 300, sessions: 5 } })));
    await fixture.whenStable();

    expect(week?.getAttribute('aria-pressed')).toBe('true');
    expect(el.querySelector('.period')?.textContent).toContain('7');
    expect([...el.querySelectorAll('th')].some((th) => th.textContent?.includes('Load in period'))).toBe(true);
    expect(el.textContent).toContain('2,100');

    const from = el.querySelector<HTMLInputElement>('input[type="date"][name="from"]')!;
    from.value = addDaysIso(today, -2);
    from.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
    const custom = controller.expectOne((r) => r.url === '/api/dashboard');
    expect(custom.request.params.get('from')).toBe(addDaysIso(today, -2));
    custom.flush(squad);
  });

  it('[AC-62] compares the squad on one load metric at a time', async () => {
    const { el, click } = await render([
      row({ athleteId: 1, displayName: 'Nora', acuteLoad: 300, chronicLoad: 420, acuteChronicRatio: 0.71 }),
      row({ athleteId: 2, displayName: 'Elias', acuteLoad: 650, chronicLoad: 380, acuteChronicRatio: 1.71 }),
      row({ athleteId: 3, displayName: 'Lea', acuteLoad: 360, chronicLoad: 330, acuteChronicRatio: 1.09 }),
    ]);
    const bars = () => [...el.querySelectorAll('.compare .bar-row .bar-name')].map((n) => n.textContent?.trim());
    const metric = (label: string) => [...el.querySelectorAll('.compare-metrics button')].find((b) => b.textContent?.trim() === label);

    expect(metric('Week average')?.getAttribute('aria-pressed')).toBe('true');
    expect(bars()).toEqual(['Elias', 'Lea', 'Nora']);

    await click(metric('Month average'));
    expect(bars()).toEqual(['Nora', 'Elias', 'Lea']);

    await click(metric('Week vs month'));
    expect(bars()).toEqual(['Elias', 'Lea', 'Nora']);
    expect(el.querySelector('.compare .band')).toBeTruthy();
    const outside = [...el.querySelectorAll('.compare .bar-row.outside .bar-name')].map((n) => n.textContent?.trim());
    expect(outside).toEqual(['Elias', 'Nora']);
  });

  it('[AC-65] the athlete form offers active sports plus the current retired one', async () => {
    const { el, click, fixture } = await render();
    const nora = [...el.querySelectorAll('tbody tr')].find((tr) => tr.textContent?.includes('Nora'))!;

    await click(nora.querySelector('button.edit'));
    controller.expectOne('/api/athletes/1').flush({ id: 1, code: 'ATH-001', displayName: 'Nora', sportId: 9, sport: 'Fencing', birthYear: 2002, sex: 'Female', bodyMassKg: 66, archivedAt: null });
    await fixture.whenStable();
    controller.expectOne('/api/sports').flush(sportsList);
    await fixture.whenStable();

    const options = [...el.querySelectorAll('dialog.athlete-form select[name="athlete.sportId"] option')].map((o) => o.textContent?.trim());
    expect(options).toEqual(['Choose a sport…', 'Fencing (retired)', 'Ice hockey', 'Rowing']);
  });

  it('[AC-57] reloads in the new language so server texts are translated too', async () => {
    const { el, fixture } = await render();

    TestBed.inject(I18n).setLanguage('de');
    fixture.detectChanges();
    await fixture.whenStable();

    const reload = controller.expectOne('/api/dashboard');
    expect(reload.request.headers.get('Accept-Language')).toBe('de');
    reload.flush(squad);
    await fixture.whenStable();
    expect(el.querySelector('h1')?.textContent).toContain('Kader heute');
    expect(header(el, 'Belastung heute')).toBeTruthy();
  });

  it('[AC-42] log session opens the staff form with the athlete selected', async () => {
    const { el } = await render();
    const elias = [...el.querySelectorAll('tbody tr')].find((tr) => tr.textContent?.includes('Elias'))!;

    expect(elias.querySelector('a.log-session')?.getAttribute('href')).toMatch(/\/sessions\/new\?athlete=2&lang=en$/);
  });
});
