import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding, withRouterConfig } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from '../../api/persona.interceptor';
import { AthleteSpacePage } from './athlete-space.page';

@Component({ template: '' })
class SectionStub {}

const squad = [
  { id: 1, code: 'ATH-001', displayName: 'Nora', sport: 'Alpine skiing' },
  { id: 2, code: 'ATH-002', displayName: 'Elias', sport: 'Ice hockey' },
  { id: 3, code: 'ATH-003', displayName: 'Lea', sport: 'Sprint' },
];

describe('AthleteSpacePage', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            {
              path: 'athletes/:id',
              component: AthleteSpacePage,
              children: [
                { path: '', component: SectionStub },
                { path: 'performance', component: SectionStub },
                { path: 'sessions', component: SectionStub },
                { path: 'reviews', component: SectionStub },
              ],
            },
          ],
          withComponentInputBinding(),
          withRouterConfig({ paramsInheritanceStrategy: 'always' }),
        ),
        provideHttpClient(withInterceptors([personaInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  async function open(url: string) {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    controller.expectOne('/api/athletes').flush(squad);
    controller.match((r) => /^\/api\/athletes\/\d+$/.test(r.url)).forEach((r) =>
      r.flush({ id: 1, code: 'ATH-001', displayName: 'Nora', sportId: 1, sport: 'Alpine skiing', birthYear: 2002, sex: 'Female', bodyMassKg: 66, archivedAt: null }),
    );
    harness.detectChanges();
    await harness.fixture.whenStable();
    return { harness, el: harness.routeNativeElement as HTMLElement };
  }

  it('[AC-66] has the four sections and a selector grouped by sport', async () => {
    const { el } = await open('/athletes/1');

    expect([...el.querySelectorAll('nav.sections a')].map((a) => a.textContent?.trim())).toEqual([
      'Overview',
      'Performance',
      'Sessions and notes',
      'Reviews',
    ]);
    expect([...el.querySelectorAll('select.athlete-select optgroup')].map((g) => g.getAttribute('label'))).toEqual([
      'Alpine skiing',
      'Ice hockey',
      'Sprint',
    ]);
    expect(el.querySelector('h1')?.textContent).toContain('Nora');
  });

  it('[AC-66] switches to another athlete and keeps the open section', async () => {
    const { el, harness } = await open('/athletes/1/performance');
    const select = el.querySelector<HTMLSelectElement>('select.athlete-select')!;

    select.value = '2';
    select.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/athletes/2/performance');
  });

  it('[AC-66] narrows the selector with a search', async () => {
    const { el, harness } = await open('/athletes/1');
    const search = el.querySelector<HTMLInputElement>('input.athlete-search')!;

    search.value = 'ea';
    search.dispatchEvent(new Event('input'));
    harness.detectChanges();
    await harness.fixture.whenStable();

    // A placeholder appears when the current athlete is filtered out, so picking the only match still counts as a change.
    const options = [...el.querySelectorAll('select.athlete-select option:not([value=""])')].map((o) => o.textContent?.trim());
    expect(el.querySelector('select.athlete-select option[value=""]')?.textContent).toContain('Choose an athlete');
    expect(options).toEqual(['Lea (ATH-003)']);
  });
});
