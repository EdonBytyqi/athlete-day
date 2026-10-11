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
    const type = async (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      await fixture.whenStable();
    };
    return { fixture, el, click, type };
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
    const { el, click, type, fixture } = await render();

    await click(el.querySelector('button.add-athlete'));
    const dialog = el.querySelector('dialog.athlete-form')!;
    expect(dialog.hasAttribute('open')).toBe(true);
    expect(dialog.querySelector('h2')?.textContent).toContain('Add athlete');

    await type('dialog.athlete-form input[name="displayName"]', 'Ada');
    await type('dialog.athlete-form input[name="sport"]', 'Rowing');
    await type('dialog.athlete-form input[name="birthYear"]', '2001');
    await type('dialog.athlete-form input[name="bodyMassKg"]', '68.5');
    await click(dialog.querySelector('button[type="submit"]'));

    const create = controller.expectOne('/api/athletes');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toMatchObject({ displayName: 'Ada', sport: 'Rowing', birthYear: 2001, bodyMassKg: 68.5 });
    create.flush({ id: 9, code: 'ATH-009', displayName: 'Ada', sport: 'Rowing', birthYear: 2001, sex: 'NotStated', bodyMassKg: 68.5, archivedAt: null }, { status: 201, statusText: 'Created' });
    await fixture.whenStable();

    expect(TestBed.inject(ToastService).current()?.message).toContain('Ada added as ATH-009');
  });

  it('[AC-42] log session opens the staff form with the athlete selected', async () => {
    const { el } = await render();
    const elias = [...el.querySelectorAll('tbody tr')].find((tr) => tr.textContent?.includes('Elias'))!;

    expect(elias.querySelector('a.log-session')?.getAttribute('href')).toMatch(/\/sessions\/new\?athlete=2$/);
  });
});
