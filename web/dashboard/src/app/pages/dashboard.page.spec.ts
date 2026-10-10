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

const row = (overrides: Partial<DashboardRow>): DashboardRow => ({
  athleteId: 1,
  code: 'ATH-001',
  displayName: 'Nora',
  sport: 'Alpine skiing',
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

  it('[AC-15] lists athletes and counts check-ins', async () => {
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();

    controller.expectOne('/api/dashboard').flush([
      row({}),
      row({
        athleteId: 2,
        code: 'ATH-002',
        displayName: 'Elias',
        acuteChronicRatio: 1.72,
        readiness: { status: 'CheckIn', reasons: ['7-day load well above 28-day average (ratio 1.72)'] },
      }),
    ]);
    await fixture.whenStable();

    const el = fixture.nativeElement as HTMLElement;
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
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    controller.expectOne('/api/dashboard').flush([row({})]);
    await fixture.whenStable();

    version.update((v) => v + 1);
    fixture.detectChanges();
    await fixture.whenStable();

    controller.expectOne('/api/dashboard').flush([row({ todayLoad: 900 })]);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('900');
  });
});
