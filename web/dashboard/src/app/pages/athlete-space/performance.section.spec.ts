import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from '../../api/persona.interceptor';
import { PersonaService } from '../../api/persona.service';
import { PerformanceSection } from './performance.section';

const result = (date: string, value: number) => ({ date, value });
const benchmarks = [
  {
    testType: 'CMJ', unit: 'cm', higherIsBetter: true,
    latest: result('2026-10-01', 33), best: result('2026-09-11', 34),
    firstInPeriod: result('2026-09-11', 34), changeInPeriod: -1, squadAverage: 36.5,
    goal: { testType: 'CMJ', target: 36, targetDate: '2026-11-10', note: null, setBy: 'lena' },
    gapToGoal: 3, status: 'Open',
    history: [result('2026-09-11', 34), result('2026-10-01', 33)],
  },
  {
    testType: 'SPRINT_30M', unit: 's', higherIsBetter: false,
    latest: result('2026-10-06', 4.3), best: result('2026-10-06', 4.3),
    firstInPeriod: result('2026-09-21', 4.4), changeInPeriod: -0.1, squadAverage: 4.2,
    goal: { testType: 'SPRINT_30M', target: 4.35, targetDate: null, note: null, setBy: 'lena' },
    gapToGoal: -0.05, status: 'Reached',
    history: [result('2026-09-21', 4.4), result('2026-10-06', 4.3)],
  },
  {
    testType: 'SJ', unit: 'cm', higherIsBetter: true,
    latest: result('2026-10-01', 30), best: result('2026-10-01', 30),
    firstInPeriod: result('2026-10-01', 30), changeInPeriod: 0, squadAverage: null,
    goal: null, gapToGoal: null, status: null,
    history: [result('2026-10-01', 30)],
  },
];

describe('PerformanceSection', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [PerformanceSection],
      providers: [provideHttpClient(withInterceptors([personaInterceptor])), provideHttpClientTesting()],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  async function render(user: string) {
    TestBed.inject(PersonaService).switchTo(user);
    const fixture = TestBed.createComponent(PerformanceSection);
    fixture.componentRef.setInput('id', '1');
    fixture.detectChanges();
    controller.expectOne((r) => r.url === '/api/athletes/1/benchmarks').flush(benchmarks);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    };
    return { el, settle };
  }

  const card = (el: HTMLElement, test: string) => el.querySelector<HTMLElement>(`.benchmark[data-test="${test}"]`)!;

  it('[AC-71] charts each test with goal and squad average and states the gap in words', async () => {
    const { el } = await render('lena');

    expect(card(el, 'CMJ').querySelector('h3')?.textContent).toContain('Counter-movement jump');
    expect(card(el, 'CMJ').querySelector('.gap')?.textContent).toContain('3 cm to go');
    expect(card(el, 'SPRINT_30M').querySelector('.gap')?.textContent).toContain('Goal reached');
    expect(card(el, 'SJ').querySelector('.gap')?.textContent).toContain('No goal set');
    expect(card(el, 'CMJ').querySelector('svg .goal-line')).toBeTruthy();
    expect(card(el, 'CMJ').querySelector('svg .squad-line')).toBeTruthy();
  });

  it('[AC-71] the coach sets and removes goals', async () => {
    const { el, settle } = await render('lena');

    card(el, 'CMJ').querySelector<HTMLButtonElement>('button.set-goal')!.click();
    await settle();
    const target = card(el, 'CMJ').querySelector<HTMLInputElement>('input[name="target"]')!;
    expect(target.value).toBe('36');
    target.value = '37';
    target.dispatchEvent(new Event('input'));
    card(el, 'CMJ').querySelector<HTMLButtonElement>('button.save-goal')!.click();
    await settle();

    const put = controller.expectOne((r) => r.method === 'PUT' && r.url === '/api/athletes/1/goals/CMJ');
    expect(put.request.body).toMatchObject({ target: 37, targetDate: '2026-11-10' });
    put.flush({ testType: 'CMJ', target: 37, targetDate: '2026-11-10', note: null, setBy: 'lena' });
    await settle();
    controller.expectOne((r) => r.url === '/api/athletes/1/benchmarks').flush(benchmarks);
    await settle();

    card(el, 'SPRINT_30M').querySelector<HTMLButtonElement>('button.remove-goal')!.click();
    await settle();
    expect(controller.expectOne((r) => r.url === '/api/athletes/1/goals/SPRINT_30M').request.method).toBe('DELETE');
  });

  it('[AC-71] other roles see the figures but cannot change goals', async () => {
    const { el } = await render('jonas');

    expect(card(el, 'CMJ').querySelector('.gap')).toBeTruthy();
    expect(el.querySelector('button.set-goal')).toBeNull();
    expect(el.querySelector('button.remove-goal')).toBeNull();
  });
});
