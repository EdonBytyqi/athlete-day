import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from '../../api/persona.interceptor';
import { PersonaService } from '../../api/persona.service';
import { PlanSection } from './plan.section';

const s = (type: string, minutes: number, rpe: number, note: string | null = null) => ({
  type, durationMinutes: minutes, rpe, note, plannedLoad: minutes * rpe,
});
const day = (date: string, dayOfWeek: number, source: 'Standard' | 'Changed', sessions: ReturnType<typeof s>[], reason: string | null = null, logged = 0) => ({
  date, dayOfWeek, source, reason, sessions,
  plannedLoad: sessions.reduce((sum, x) => sum + x.plannedLoad, 0), loggedLoad: logged, loggedSessions: logged ? 1 : 0,
});
const week = {
  athleteId: 1,
  weekStart: '2026-10-12',
  days: [
    day('2026-10-12', 1, 'Standard', [s('Strength', 60, 6)], null, 300),
    day('2026-10-13', 2, 'Standard', []),
    day('2026-10-14', 3, 'Changed', [s('Technique', 40, 4)], 'Match moved'),
    day('2026-10-15', 4, 'Standard', []),
    day('2026-10-16', 5, 'Changed', [], 'Travel'),
    day('2026-10-17', 6, 'Standard', []),
    day('2026-10-18', 7, 'Standard', []),
  ],
  plannedLoad: 520,
  loggedLoad: 300,
};
const template = {
  athleteId: 1,
  days: [1, 2, 3, 4, 5, 6, 7].map((d) => ({ dayOfWeek: d, sessions: d === 1 ? [s('Strength', 60, 6)] : [] })),
};

describe('PlanSection', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [PlanSection],
      providers: [provideHttpClient(withInterceptors([personaInterceptor])), provideHttpClientTesting()],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  async function render(user: string) {
    TestBed.inject(PersonaService).switchTo(user);
    const fixture = TestBed.createComponent(PlanSection);
    fixture.componentRef.setInput('id', '1');
    fixture.detectChanges();
    controller.expectOne((r) => r.url === '/api/athletes/1/plan/week').flush(week);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const type = async (input: HTMLInputElement, value: string) => {
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await settle();
    };
    return { el, settle, type };
  }

  const dayCard = (el: HTMLElement, date: string) => el.querySelector<HTMLElement>(`.plan-day[data-date="${date}"]`)!;

  it('[AC-83] shows the week with changed days marked and planned against logged load', async () => {
    const { el } = await render('jonas');

    expect(el.querySelectorAll('.plan-day')).toHaveLength(7);
    expect(el.querySelectorAll('.plan-day.changed')).toHaveLength(2);
    expect(dayCard(el, '2026-10-14').querySelector('.plan-reason')?.textContent).toContain('Match moved');
    expect(dayCard(el, '2026-10-12').querySelector('.plan-load')?.textContent).toContain('360');
    expect(dayCard(el, '2026-10-12').querySelector('.plan-load')?.textContent).toContain('300');
    expect(el.querySelector('.plan-total')?.textContent).toContain('520');
    expect(el.querySelector('button.change-day')).toBeNull(); // the physio reads only
    expect(el.querySelector('button.edit-template')).toBeNull();
  });

  it('[AC-83] moves to the next week', async () => {
    const { el, settle } = await render('lena');

    el.querySelector<HTMLButtonElement>('button.next-week')!.click();
    await settle();

    expect(controller.expectOne((r) => r.url === '/api/athletes/1/plan/week').request.params.get('start')).toBe('2026-10-19');
  });

  it('[AC-83] the coach changes a single day and resets another', async () => {
    const { el, settle, type } = await render('lena');

    dayCard(el, '2026-10-12').querySelector<HTMLButtonElement>('button.change-day')!.click();
    await settle();
    const editor = dayCard(el, '2026-10-12').querySelector<HTMLElement>('.day-editor')!;
    expect(editor.querySelector<HTMLInputElement>('input[name="minutes"]')?.value).toBe('60');
    await type(editor.querySelector<HTMLInputElement>('input[name="reason"]')!, 'Lighter after the match');
    editor.querySelector<HTMLButtonElement>('button.add-session')!.click();
    await settle();
    editor.querySelector<HTMLButtonElement>('button.save-day')!.click();
    await settle();

    const put = controller.expectOne((r) => r.method === 'PUT' && r.url === '/api/athletes/1/plan/days/2026-10-12');
    expect(put.request.body.reason).toBe('Lighter after the match');
    expect(put.request.body.sessions).toHaveLength(2);
    expect(put.request.body.sessions[0]).toMatchObject({ type: 'Strength', durationMinutes: 60, rpe: 6 });
    put.flush(null);
    await settle();
    controller.expectOne((r) => r.url === '/api/athletes/1/plan/week').flush(week);
    await settle();

    dayCard(el, '2026-10-14').querySelector<HTMLButtonElement>('button.reset-day')!.click();
    await settle();
    expect(controller.expectOne((r) => r.url === '/api/athletes/1/plan/days/2026-10-14').request.method).toBe('DELETE');
  });

  it('[AC-83] the coach edits the standard week', async () => {
    const { el, settle } = await render('lena');

    el.querySelector<HTMLButtonElement>('button.edit-template')!.click();
    await settle();
    controller.expectOne('/api/athletes/1/plan/template').flush(template);
    await settle();

    const tuesday = el.querySelector<HTMLElement>('.template-day[data-day="2"]')!;
    expect(el.querySelector('.template-day[data-day="1"]')!.querySelectorAll('.edit-session')).toHaveLength(1);
    tuesday.querySelector<HTMLButtonElement>('button.add-session')!.click();
    await settle();
    el.querySelector<HTMLButtonElement>('button.save-template')!.click();
    await settle();

    const put = controller.expectOne((r) => r.method === 'PUT' && r.url === '/api/athletes/1/plan/template');
    expect(put.request.body.days).toHaveLength(7);
    expect(put.request.body.days[1]).toMatchObject({ dayOfWeek: 2 });
    expect(put.request.body.days[1].sessions).toHaveLength(1);
  });
});
