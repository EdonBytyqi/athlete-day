import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from './persona.interceptor';
import { PersonaService } from './persona.service';
import { I18n } from '../i18n/i18n.service';

describe('personaInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([personaInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('[AC-04] sends the current persona with API calls', () => {
    TestBed.inject(PersonaService).switchTo('luca');

    http.get('/api/kitchen/today').subscribe();

    const req = controller.expectOne('/api/kitchen/today');
    expect(req.request.headers.get('X-Demo-User')).toBe('luca');
    expect(req.request.headers.get('X-Demo-Role')).toBe('Kitchen');
    req.flush([]);
  });

  it('[AC-57] sends the current language with API calls', () => {
    http.get('/api/dashboard').subscribe();
    const english = controller.expectOne('/api/dashboard');
    expect(english.request.headers.get('Accept-Language')).toBe('en');
    english.flush([]);

    TestBed.inject(I18n).setLanguage('de');
    http.get('/api/dashboard').subscribe();
    const german = controller.expectOne('/api/dashboard');
    expect(german.request.headers.get('Accept-Language')).toBe('de');
    german.flush([]);
  });

  it('[AC-04] leaves non-API requests alone', () => {
    http.get('/assets/config.json').subscribe();

    const req = controller.expectOne('/assets/config.json');
    expect(req.request.headers.has('X-Demo-Role')).toBe(false);
    req.flush({});
  });
});
