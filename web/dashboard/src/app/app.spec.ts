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

  it('shows only the screens of the current role', async () => {
    const fixture = TestBed.createComponent(App);
    const persona = TestBed.inject(PersonaService);
    const tabs = () =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.tabs a')).map((a) => a.textContent?.trim());

    await fixture.whenStable();
    expect(tabs()).toEqual(['Squad', 'Device import', 'Staff input ↗']);

    persona.switchTo('luca');
    await fixture.whenStable();
    expect(tabs()).toEqual(['Kitchen', 'Staff input ↗']);
  });
});
