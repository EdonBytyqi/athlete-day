import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { personaInterceptor } from '../../api/persona.interceptor';
import { PersonaService } from '../../api/persona.service';
import { DocumentsSection } from './documents.section';

const doc = (id: number, type: string, title: string, fileName: string, sizeBytes = 48_000) => ({
  id, athleteId: 1, type, title, fileName, contentType: 'application/pdf', sizeBytes,
  sha256: 'ab'.repeat(32), uploadedBy: 'jonas', uploadedAt: '2026-10-11T08:30:00+00:00',
});
const documents = [doc(7, 'Medical', 'MRI report', 'mri-knee.pdf'), doc(3, 'Certificate', 'Anti-doping course', 'anti-doping.pdf')];

describe('DocumentsSection', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [DocumentsSection],
      providers: [provideHttpClient(withInterceptors([personaInterceptor])), provideHttpClientTesting()],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  async function render(user: string, list: unknown[] | null = documents) {
    TestBed.inject(PersonaService).switchTo(user);
    const fixture = TestBed.createComponent(DocumentsSection);
    fixture.componentRef.setInput('id', '1');
    fixture.detectChanges();
    if (list) controller.expectOne('/api/athletes/1/documents').flush(list);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    };
    return { el, settle };
  }

  const typeOptions = (el: HTMLElement) =>
    [...el.querySelectorAll<HTMLOptionElement>('form.upload-document select[name="type"] option')].map((o) => o.value);

  it('[AC-91] lists the documents and offers only the types the physio may upload', async () => {
    const { el } = await render('jonas');

    const rows = el.querySelectorAll<HTMLElement>('.document-row');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('MRI report');
    expect(rows[0].textContent).toContain('mri-knee.pdf');
    expect(rows[0].textContent).toContain('jonas');
    expect(typeOptions(el)).toEqual(['Medical', 'Certificate', 'General']);
  });

  it('[AC-91] the coach may upload certificates and general documents only', async () => {
    const { el } = await render('lena', [documents[1]]);

    expect(typeOptions(el)).toEqual(['Certificate', 'General']);
  });

  it('[AC-91] other roles see a notice and nothing is requested', async () => {
    const { el } = await render('mia', null);

    expect(el.querySelector('.documents-restricted')).not.toBeNull();
    expect(el.querySelector('form.upload-document')).toBeNull();
    controller.expectNone('/api/athletes/1/documents');
  });

  it('[AC-91] uploads a file with type and title, then reloads the list', async () => {
    const { el, settle } = await render('lena', []);
    const form = el.querySelector<HTMLFormElement>('form.upload-document')!;
    const file = new File(['%PDF-1.4'], 'licence.pdf', { type: 'application/pdf' });
    const input = form.querySelector<HTMLInputElement>('input[type="file"]')!;
    Object.defineProperty(input, 'files', { value: [file] });
    input.dispatchEvent(new Event('change'));
    const select = form.querySelector<HTMLSelectElement>('select[name="type"]')!;
    select.value = 'General';
    select.dispatchEvent(new Event('change'));
    const title = form.querySelector<HTMLInputElement>('input[name="title"]')!;
    title.value = 'Coaching licence';
    title.dispatchEvent(new Event('input'));
    await settle();

    form.querySelector<HTMLButtonElement>('button.upload')!.click();
    await settle();

    const post = controller.expectOne((r) => r.method === 'POST' && r.url === '/api/athletes/1/documents');
    const body = post.request.body as FormData;
    expect(body.get('type')).toBe('General');
    expect(body.get('title')).toBe('Coaching licence');
    expect((body.get('file') as File).name).toBe('licence.pdf');
    post.flush(doc(9, 'General', 'Coaching licence', 'licence.pdf'), { status: 201, statusText: 'Created' });
    await settle();
    controller.expectOne('/api/athletes/1/documents').flush([doc(9, 'General', 'Coaching licence', 'licence.pdf')]);
    await settle();

    expect(el.querySelectorAll('.document-row')).toHaveLength(1);
  });

  it('[AC-91] downloads through the API and deletes after a confirmation', async () => {
    const createObjectURL = vi.fn(() => 'blob:document');
    const revokeObjectURL = vi.fn();
    Object.assign(URL, { createObjectURL, revokeObjectURL });
    const { el, settle } = await render('jonas');
    const first = () => el.querySelector<HTMLElement>('.document-row')!;

    first().querySelector<HTMLButtonElement>('button.download-document')!.click();
    await settle();
    const download = controller.expectOne('/api/athletes/1/documents/7/content');
    expect(download.request.responseType).toBe('blob');
    download.flush(new Blob(['%PDF-1.4'], { type: 'application/pdf' }));
    await settle();
    expect(createObjectURL).toHaveBeenCalled();

    first().querySelector<HTMLButtonElement>('button.delete-document')!.click();
    await settle();
    controller.expectNone((r) => r.method === 'DELETE');
    first().querySelector<HTMLButtonElement>('button.confirm-delete')!.click();
    await settle();

    const del = controller.expectOne((r) => r.method === 'DELETE');
    expect(del.request.url).toBe('/api/athletes/1/documents/7');
    del.flush(null);
    await settle();
    controller.expectOne('/api/athletes/1/documents').flush([documents[1]]);
    await settle();
    expect(el.querySelectorAll('.document-row')).toHaveLength(1);
  });
});
