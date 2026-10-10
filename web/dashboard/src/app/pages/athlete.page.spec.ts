import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { AthletePage } from './athlete.page';
import { personaInterceptor } from '../api/persona.interceptor';
import { PersonaService } from '../api/persona.service';
import { RealtimeService } from '../api/realtime.service';

describe('AthletePage – wellness history (F13)', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [AthletePage],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([personaInterceptor])),
        provideHttpClientTesting(),
        { provide: RealtimeService, useValue: { version: signal(0) } },
      ],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  async function render(user: string) {
    TestBed.inject(PersonaService).switchTo(user);
    const fixture = TestBed.createComponent(AthletePage);
    fixture.componentRef.setInput('id', '2');
    fixture.detectChanges();
    controller
      .expectOne('/api/athletes/2')
      .flush({ id: 2, code: 'ATH-002', displayName: 'Elias', sport: 'Ice hockey', birthYear: 2005, sex: 'Male', bodyMassKg: 86, archivedAt: null });
    controller.expectOne((r) => r.url === '/api/athletes/2/load-trend').flush({ athleteId: 2, code: 'ATH-002', points: [] });
    controller.expectOne('/api/athletes/2/sessions').flush([]);
    controller.expectOne('/api/athletes/2/measurements').flush([]);
    return fixture;
  }

  it('[AC-49] shows the history with flagged days highlighted for the coach', async () => {
    const fixture = await render('lena');

    controller.expectOne((r) => r.url === '/api/athletes/2/wellness').flush([
      { date: '2026-10-10', sleepHours: 5.5, soreness: 4, mood: 4, flags: ['Sleep under 6 h (5.5 h)', 'High soreness (4/5)'] },
      { date: '2026-10-09', sleepHours: 8, soreness: 2, mood: 4, flags: [] },
    ]);
    await fixture.whenStable();

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('.wellness-history tbody tr');
    expect(rows).toHaveLength(2);
    expect(rows[0].classList.contains('flagged')).toBe(true);
    expect(rows[0].textContent).toContain('High soreness (4/5)');
    expect(rows[1].classList.contains('flagged')).toBe(false);
  });

  it('[AC-49] requests the history for the physio as well', async () => {
    await render('jonas');

    controller.expectOne((r) => r.url === '/api/athletes/2/wellness').flush([]);
    controller.expectOne('/api/athletes/2/physio-notes').flush([]);
  });

  it('[AC-49] does not request the history for other roles', async () => {
    await render('mia');

    controller.expectNone((r) => r.url === '/api/athletes/2/wellness');
    controller.verify();
  });
});
