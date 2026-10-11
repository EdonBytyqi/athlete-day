import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { personaInterceptor } from '../../api/persona.interceptor';
import { PersonaService } from '../../api/persona.service';
import { RealtimeService } from '../../api/realtime.service';
import { I18n } from '../../i18n/i18n.service';
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

const insights = [
  { kind: 'LoadSpike', tone: 'Attention', testType: null, figures: { ratio: 1.42, acute: 620, chronic: 437 } },
  { kind: 'PersonalBest', tone: 'Positive', testType: 'CMJ', figures: { value: 43, previous: 41.5 } },
  { kind: 'MissingCheckIns', tone: 'Info', testType: null, figures: { checkIns: 2 } },
];

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

  async function render(user: string, found: unknown[] = []) {
    TestBed.inject(PersonaService).switchTo(user);
    const fixture = TestBed.createComponent(OverviewSection);
    fixture.componentRef.setInput('id', '2');
    fixture.detectChanges();
    controller.expectOne((r) => r.url === '/api/athletes/2/progress').flush(progress);
    controller.expectOne((r) => r.url === '/api/athletes/2/load-trend').flush({ athleteId: 2, code: 'ATH-002', points: [] });
    controller.expectOne('/api/athletes/2/insights').flush(found);
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

  it('[AC-99] lists the insights as sentences with their figures, attention first', async () => {
    const fixture = await render('lena', insights);
    const el = fixture.nativeElement as HTMLElement;

    const items = el.querySelectorAll<HTMLElement>('.insight');
    expect(items).toHaveLength(3);
    expect(items[0].dataset['tone']).toBe('Attention');
    expect(items[0].textContent).toContain('1.42');
    expect(items[0].textContent).toContain('620');
    expect(items[1].dataset['tone']).toBe('Positive');
    expect(items[1].textContent).toContain('Counter-movement jump');
    expect(items[1].textContent).toContain('43');
    expect(items[2].textContent).toContain('2');
    expect(el.querySelector('.insights-empty')).toBeNull();
  });

  it('[AC-99] speaks German and says when there is nothing to report', async () => {
    TestBed.inject(I18n).setLanguage('de');
    const fixture = await render('mia', [insights[0]]);
    const el = fixture.nativeElement as HTMLElement;

    // de-CH writes 1.42 with a point (spec section 4); the sentence is German.
    expect(el.querySelector('.insight')?.textContent).toContain('Belastungsspitze: Verhältnis 1.42');

    const empty = await render('mia', []);
    expect((empty.nativeElement as HTMLElement).querySelector('.insights-empty')).not.toBeNull();
  });
});
