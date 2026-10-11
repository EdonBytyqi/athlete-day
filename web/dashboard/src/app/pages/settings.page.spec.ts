import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from '../api/persona.interceptor';
import { PersonaService } from '../api/persona.service';
import { SettingsPage } from './settings.page';

describe('SettingsPage', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [SettingsPage],
      providers: [provideHttpClient(withInterceptors([personaInterceptor])), provideHttpClientTesting()],
    });
    controller = TestBed.inject(HttpTestingController);
    TestBed.inject(PersonaService).switchTo('admin');
  });

  async function render() {
    const fixture = TestBed.createComponent(SettingsPage);
    fixture.detectChanges();
    controller.expectOne('/api/sports').flush([
      { id: 1, name: 'Rowing', retired: false },
      { id: 2, name: 'Fencing', retired: true },
    ]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    };
    return { fixture, el, settle };
  }

  const row = (el: HTMLElement, name: string) =>
    [...el.querySelectorAll<HTMLElement>('.sport-row')].find((r) => r.querySelector<HTMLInputElement>('input.sport-name')?.value === name)!;

  it('[AC-65] lists the sports and marks retired ones', async () => {
    const { el } = await render();

    expect(el.querySelectorAll('.sport-row')).toHaveLength(2);
    expect(row(el, 'Fencing').classList.contains('retired')).toBe(true);
    expect(row(el, 'Rowing').classList.contains('retired')).toBe(false);
  });

  it('[AC-65] adds, renames and retires sports', async () => {
    const { el, settle } = await render();

    const name = el.querySelector<HTMLInputElement>('input[name="newSport"]')!;
    name.value = 'Judo';
    name.dispatchEvent(new Event('input'));
    el.querySelector<HTMLButtonElement>('button.add-sport')!.click();
    await settle();
    const add = controller.expectOne((r) => r.method === 'POST' && r.url === '/api/sports');
    expect(add.request.body).toEqual({ name: 'Judo' });
    add.flush({ id: 3, name: 'Judo', retired: false }, { status: 201, statusText: 'Created' });
    await settle();
    controller.expectOne((r) => r.method === 'GET' && r.url === '/api/sports').flush([
      { id: 1, name: 'Rowing', retired: false },
      { id: 2, name: 'Fencing', retired: true },
      { id: 3, name: 'Judo', retired: false },
    ]);
    await settle();

    const rowing = row(el, 'Rowing');
    const input = rowing.querySelector<HTMLInputElement>('input.sport-name')!;
    input.value = 'Rowing (sweep)';
    input.dispatchEvent(new Event('input'));
    rowing.querySelector<HTMLButtonElement>('button.save')!.click();
    await settle();
    const rename = controller.expectOne((r) => r.method === 'PUT' && r.url === '/api/sports/1');
    expect(rename.request.body).toEqual({ name: 'Rowing (sweep)', retired: false });
    rename.flush({ id: 1, name: 'Rowing (sweep)', retired: false });
    await settle();
    controller.expectOne((r) => r.method === 'GET' && r.url === '/api/sports').flush([
      { id: 1, name: 'Rowing (sweep)', retired: false },
      { id: 2, name: 'Fencing', retired: true },
    ]);
    await settle();

    row(el, 'Rowing (sweep)').querySelector<HTMLButtonElement>('button.retire')!.click();
    await settle();
    const retire = controller.expectOne((r) => r.method === 'PUT' && r.url === '/api/sports/1');
    expect(retire.request.body).toEqual({ name: 'Rowing (sweep)', retired: true });
  });
});
