import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from './persona.interceptor';
import { PersonaService } from './persona.service';

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

  it('sends the current persona with API calls', () => {
    TestBed.inject(PersonaService).switchTo('luca');

    http.get('/api/kitchen/today').subscribe();

    const req = controller.expectOne('/api/kitchen/today');
    expect(req.request.headers.get('X-Demo-User')).toBe('luca');
    expect(req.request.headers.get('X-Demo-Role')).toBe('Kitchen');
    req.flush([]);
  });

  it('leaves non-API requests alone', () => {
    http.get('/assets/config.json').subscribe();

    const req = controller.expectOne('/assets/config.json');
    expect(req.request.headers.has('X-Demo-Role')).toBe(false);
    req.flush({});
  });
});
