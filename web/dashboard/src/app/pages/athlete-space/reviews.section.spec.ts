import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from '../../api/persona.interceptor';
import { PersonaService } from '../../api/persona.service';
import { ReviewsSection } from './reviews.section';

const review = {
  id: 3, athleteId: 1, from: '2026-09-14', to: '2026-10-11',
  observations: 'Steady block', achievements: 'New CMJ best', challenges: null, recommendations: 'Keep the volume',
  author: 'lena', createdAt: '2026-10-11T09:00:00Z',
  snapshot: {
    weeks: [], totalLoad: 9800, sessions: 18, averageSleep: 7.2, averageSoreness: 2.4, averageMood: 4.1,
    benchmarks: [],
  },
};

describe('ReviewsSection', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [ReviewsSection],
      providers: [provideHttpClient(withInterceptors([personaInterceptor])), provideHttpClientTesting()],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  async function render(user: string) {
    TestBed.inject(PersonaService).switchTo(user);
    const fixture = TestBed.createComponent(ReviewsSection);
    fixture.componentRef.setInput('id', '1');
    fixture.detectChanges();
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    };
    return { el, settle };
  }

  it('[AC-77] lists past reviews with their snapshot figures', async () => {
    const { el, settle } = await render('jonas');
    controller.expectOne('/api/athletes/1/reviews').flush([review]);
    await settle();

    const article = el.querySelector('.review')!;
    expect(article.textContent).toContain('Steady block');
    expect(article.querySelector('.snapshot')?.textContent).toContain('9,800');
    expect(el.querySelector('.review-form')).toBeNull(); // the physio reads, the coach writes
  });

  it('[AC-77] the coach writes a review for a chosen period', async () => {
    const { el, settle } = await render('lena');
    controller.expectOne('/api/athletes/1/reviews').flush([]);
    await settle();

    const fourWeeks = [...el.querySelectorAll('.review-weeks button')].find((b) => b.textContent?.trim() === '4 weeks') as HTMLButtonElement;
    fourWeeks.click();
    const observations = el.querySelector<HTMLTextAreaElement>('textarea[name="observations"]')!;
    observations.value = 'Good consistency';
    observations.dispatchEvent(new Event('input'));
    await settle();
    el.querySelector<HTMLButtonElement>('button.save-review')!.click();
    await settle();

    const post = controller.expectOne((r) => r.method === 'POST' && r.url === '/api/athletes/1/reviews');
    expect(post.request.body).toMatchObject({ weeks: 4, observations: 'Good consistency' });
  });

  it('[AC-77] nutrition does not see reviews', async () => {
    const { el } = await render('mia');

    controller.expectNone('/api/athletes/1/reviews');
    expect(el.querySelector('.reviews-restricted')).toBeTruthy();
  });
});
