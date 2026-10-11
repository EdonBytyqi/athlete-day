import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from '../../api/persona.interceptor';
import { PersonaService } from '../../api/persona.service';
import { SessionsSection } from './sessions.section';

const session = (id: number, date: string) => ({
  id, athleteId: 1, athleteCode: 'ATH-001', date, type: 'Strength', durationMinutes: 60, rpe: 7, load: 420, loggedBy: 'lena',
});
const note = (id: number, sessionId: number, text: string) => ({
  id, sessionId, athleteId: 1, sessionDate: '2026-10-10', sessionType: 'Strength', sessionLoad: 420, text, author: 'lena', createdAt: '2026-10-10T08:00:00Z',
});

describe('SessionsSection', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [SessionsSection],
      providers: [provideHttpClient(withInterceptors([personaInterceptor])), provideHttpClientTesting()],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  async function render(user: string, withNotes = true) {
    TestBed.inject(PersonaService).switchTo(user);
    const fixture = TestBed.createComponent(SessionsSection);
    fixture.componentRef.setInput('id', '1');
    fixture.detectChanges();
    controller.expectOne('/api/athletes/1/sessions').flush([session(12, '2026-10-11'), session(11, '2026-10-10')]);
    if (withNotes) controller.expectOne('/api/athletes/1/session-notes').flush([note(5, 11, 'Felt heavy in the warm-up')]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    return { el, fixture };
  }

  const row = (el: HTMLElement, id: number) => el.querySelector<HTMLElement>(`.session[data-session="${id}"]`)!;

  it('[AC-74] lists sessions with their notes, and the coach adds a note', async () => {
    const { el, fixture } = await render('lena');

    expect(row(el, 11).querySelector('.session-note')?.textContent).toContain('Felt heavy in the warm-up');
    expect(row(el, 12).querySelector('.session-note')).toBeNull();

    const text = row(el, 12).querySelector<HTMLTextAreaElement>('textarea')!;
    text.value = 'Sharp and fast';
    text.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    row(el, 12).querySelector<HTMLButtonElement>('button.add-note')!.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const post = controller.expectOne((r) => r.method === 'POST' && r.url === '/api/sessions/12/notes');
    expect(post.request.body).toEqual({ text: 'Sharp and fast' });
  });

  it('[AC-74] the physio reads notes but does not add any', async () => {
    const { el } = await render('jonas');

    expect(row(el, 11).querySelector('.session-note')).toBeTruthy();
    expect(el.querySelector('button.add-note')).toBeNull();
  });

  it('[AC-74] nutrition sees sessions without notes', async () => {
    const { el } = await render('mia', false);

    controller.expectNone('/api/athletes/1/session-notes');
    expect(el.querySelectorAll('.session')).toHaveLength(2);
    expect(el.querySelector('.notes-restricted')).toBeTruthy();
  });
});
