import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { personaInterceptor } from '../../api/persona.interceptor';
import { PersonaService } from '../../api/persona.service';
import { RealtimeService } from '../../api/realtime.service';
import { OverviewSection } from './overview.section';

const progress = {
  athleteId: 2,
  weeks: [
    { weekStart: '2026-09-28', totalLoad: 2100, sessions: 4, averageSleep: 7.4, averageSoreness: 2.1, averageMood: 4 },
    { weekStart: '2026-10-05', totalLoad: 3400, sessions: 5, averageSleep: 6.6, averageSoreness: 3.2, averageMood: 4.2 },
  ],
  trends: [
    { metric: 'load', recent: 11000, previous: 8000, trend: 'Up' },
    { metric: 'sessions', recent: 18, previous: 18, trend: 'Stable' },
    { metric: 'sleep', recent: 6.6, previous: 7.3, trend: 'Down' },
    { metric: 'soreness', recent: 3.2, previous: 2.2, trend: 'Up' },
    { metric: 'mood', recent: 4.4, previous: 3.9, trend: 'Up' },
  ],
};

describe('OverviewSection', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [OverviewSection],
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
    const fixture = TestBed.createComponent(OverviewSection);
    fixture.componentRef.setInput('id', '2');
    fixture.detectChanges();
    controller.expectOne((r) => r.url === '/api/athletes/2/progress').flush(progress);
    controller.expectOne((r) => r.url === '/api/athletes/2/load-trend').flush({ athleteId: 2, code: 'ATH-002', points: [] });
    await fixture.whenStable();
    return fixture;
  }

  const kpi = (el: HTMLElement, metric: string) => el.querySelector<HTMLElement>(`.kpi[data-metric="${metric}"]`)!;

  it('[AC-68] shows key figures with a trend and what it means', async () => {
    const fixture = await render('lena');
    const el = fixture.nativeElement as HTMLElement;

    expect(kpi(el, 'sleep').querySelector('.trend')?.classList.contains('trend-down')).toBe(true);
    expect(kpi(el, 'sleep').querySelector('.kpi-note')?.textContent).toContain('Needs attention');
    expect(kpi(el, 'soreness').querySelector('.kpi-note')?.textContent).toContain('Needs attention');
    expect(kpi(el, 'mood').querySelector('.kpi-note')?.textContent).toContain('Good sign');
    expect(kpi(el, 'load').querySelector('.kpi-note')?.textContent).toContain('Change to discuss');
    expect(kpi(el, 'sessions').querySelector('.kpi-note')?.textContent).toContain('Stable');
    expect(el.querySelector('.weekly-chart svg')).toBeTruthy();
  });

  it('[AC-68] changes the period', async () => {
    const fixture = await render('lena');
    const el = fixture.nativeElement as HTMLElement;
    const fourWeeks = [...el.querySelectorAll('.weeks button')].find((b) => b.textContent?.trim() === '4 weeks') as HTMLButtonElement;

    fourWeeks.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const request = controller.expectOne((r) => r.url === '/api/athletes/2/progress');
    expect(request.request.params.get('weeks')).toBe('4');
    expect(fourWeeks.getAttribute('aria-pressed')).toBe('true');
  });

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
