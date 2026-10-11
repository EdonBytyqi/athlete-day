import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';
import { routes } from './app.routes';
import { PersonaService } from './api/persona.service';
import { RealtimeService } from './api/realtime.service';

describe('App shell', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RealtimeService, useValue: { start: () => {}, state: () => 'live', version: () => 0 } },
      ],
    }).compileComponents();
  });

  const tabs = (el: HTMLElement) => Array.from(el.querySelectorAll('.tabs a')).map((a) => a.textContent?.trim());

  it('[AC-03] shows only the screens of the current role', async () => {
    const fixture = TestBed.createComponent(App);
    const persona = TestBed.inject(PersonaService);

    await fixture.whenStable();
    expect(tabs(fixture.nativeElement)).toEqual(['Squad', 'Device import', 'Staff input ↗']);

    persona.switchTo('luca');
    await fixture.whenStable();
    expect(tabs(fixture.nativeElement)).toEqual(['Kitchen', 'Staff input ↗']);
  });

  it('[AC-53] switches all visible text at once and opens the staff app in the same language', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const german = [...el.querySelectorAll('.lang-switch button')].find((b) => b.textContent?.trim() === 'DE');

    (german as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(german?.getAttribute('aria-pressed')).toBe('true');
    expect(tabs(el)).toEqual(['Kader', 'Geräte-Import', 'Erfassung ↗']);
    expect(el.querySelector('.persona span')?.textContent).toContain('Ansicht als');
    expect(el.querySelector<HTMLAnchorElement>('.tabs a[target="_blank"]')?.href).toContain('lang=de');
    expect(localStorage.getItem('athlete-day.lang')).toBe('de');
  });
});
