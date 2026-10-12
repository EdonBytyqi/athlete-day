import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from '../api/persona.interceptor';
import { PersonaService } from '../api/persona.service';
import { addDaysIso, todayIso } from '../shared/dates';
import { VersusPage } from './versus.page';

const squad = [
  { id: 1, code: 'ATH-001', displayName: 'Nora', sport: 'Alpine skiing' },
  { id: 2, code: 'ATH-002', displayName: 'Elias', sport: 'Ice hockey' },
  { id: 3, code: 'ATH-003', displayName: 'Lea', sport: 'Sprint' },
];
const metric = (key: string, a: number | null, b: number | null, winner: string, unit: string | null = null) => ({ key, unit, a, b, winner });
const versus = {
  from: '2026-09-14',
  to: '2026-10-11',
  a: { id: 2, code: 'ATH-002', displayName: 'Elias', sport: 'Ice hockey' },
  b: { id: 3, code: 'ATH-003', displayName: 'Lea', sport: 'Sprint' },
  metrics: [
    metric('totalLoad', 14200, 9800, 'NotRanked', 'AU'),
    metric('sleep', 6.8, 7.6, 'B', 'h'),
    metric('soreness', 2.1, 2.1, 'Even', '/5'),
    metric('CMJ', 41.5, null, 'NotRanked', 'cm'),
    metric('SPRINT_30M', 4.21, 4.02, 'B', 's'),
  ],
};

describe('VersusPage', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [VersusPage],
      providers: [provideRouter([]), provideHttpClient(withInterceptors([personaInterceptor])), provideHttpClientTesting()],
    });
    controller = TestBed.inject(HttpTestingController);
    TestBed.inject(PersonaService).switchTo('lena');
  });

  async function render(a?: string) {
    const fixture = TestBed.createComponent(VersusPage);
    if (a) fixture.componentRef.setInput('a', a);
    fixture.detectChanges();
    controller.expectOne('/api/athletes').flush(squad);
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const choose = async (side: 'a' | 'b', id: string) => {
      const select = el.querySelector<HTMLSelectElement>(`select.versus-${side}`)!;
      select.value = id;
      select.dispatchEvent(new Event('change'));
      await settle();
    };
    return { el, settle, choose };
  }

  const row = (el: HTMLElement, key: string) => el.querySelector<HTMLElement>(`.versus-row[data-key="${key}"]`)!;

  it('[AC-96] compares two athletes chosen by sport over 28 days and highlights the better value', async () => {
    const { el, settle, choose } = await render('2');

    expect([...el.querySelectorAll('select.versus-b optgroup')].map((g) => g.getAttribute('label'))).toEqual([
      'Alpine skiing',
      'Ice hockey',
      'Sprint',
    ]);
    expect(el.querySelector<HTMLSelectElement>('select.versus-a')!.value).toBe('2');
    await choose('b', '3');

    const request = controller.expectOne((r) => r.url === '/api/versus');
    expect(request.request.params.get('a')).toBe('2');
    expect(request.request.params.get('b')).toBe('3');
    expect(request.request.params.get('to')).toBe(todayIso());
    expect(request.request.params.get('from')).toBe(addDaysIso(todayIso(), -27));
    request.flush(versus);
    await settle();

    expect(row(el, 'sleep').querySelector('.value-b')?.classList.contains('better')).toBe(true);
    expect(row(el, 'sleep').querySelector('.value-a')?.classList.contains('better')).toBe(false);
    expect(row(el, 'soreness').querySelector('.better')).toBeNull();
    expect(row(el, 'totalLoad').querySelector('.better')).toBeNull();
    expect(row(el, 'CMJ').textContent).toContain('Counter-movement jump');
    expect(row(el, 'CMJ').querySelector('.value-b')?.textContent).toContain('–');
    expect(row(el, 'SPRINT_30M').querySelector('.value-b')?.classList.contains('better')).toBe(true);
  });

  it('[AC-96] changes the period', async () => {
    const { el, settle, choose } = await render('2');
    await choose('b', '3');
    controller.expectOne((r) => r.url === '/api/versus').flush(versus);
    await settle();

    el.querySelector<HTMLButtonElement>('button.period[data-days="7"]')!.click();
    await settle();

    const request = controller.expectOne((r) => r.url === '/api/versus');
    expect(request.request.params.get('from')).toBe(addDaysIso(todayIso(), -6));
    expect(el.querySelector('button.period[data-days="7"]')?.getAttribute('aria-pressed')).toBe('true');
  });

  it('[AC-96] does not compare an athlete with themselves', async () => {
    const { el, choose } = await render('2');
    await choose('b', '2');

    controller.expectNone((r) => r.url === '/api/versus');
    expect(el.querySelector('.versus-same')).not.toBeNull();
  });
});
