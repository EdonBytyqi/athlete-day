import { Component, ElementRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { ApiService, describeError } from '../api/api.service';
import { AthleteDocument, DocumentType } from '../api/models';
import { PersonaService } from '../api/persona.service';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';
import { ToastService } from './toast.service';

/** Need-to-know per document type, as the API applies it (spec section 4, ADR-15). The API stays the authority. */
const READERS: Record<DocumentType, string[]> = {
  Medical: ['Physio'],
  GovernmentId: ['Admin'],
  Certificate: ['Coach', 'Physio'],
  General: ['Coach', 'Physio'],
};
const TYPES: DocumentType[] = ['Medical', 'GovernmentId', 'Certificate', 'General'];
const MAX_BYTES = 10 * 1024 * 1024;

export function documentTypesFor(role: string): DocumentType[] {
  return TYPES.filter((t) => READERS[t].includes(role));
}

/**
 * One athlete's documents (AC-91, AC-92): the list of the types the role may see, an upload form for those types,
 * download through the API and delete after a confirmation.
 */
@Component({
  selector: 'app-documents-panel',
  template: `
    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }

    <form class="upload-document panel" (submit)="upload($event)">
      <h3>{{ t('documents.upload') }}</h3>
      <div class="upload-fields">
        <label>
          {{ t('documents.type') }}
          <select name="type" (change)="type.set($any($event.target).value)">
            @for (option of types(); track option) {
              <option [value]="option" [selected]="option === type()">{{ t(typeLabel(option)) }}</option>
            }
          </select>
        </label>
        <label>
          {{ t('documents.titleLabel') }}
          <input name="title" maxlength="100" autocomplete="off" [value]="title()" (input)="title.set($any($event.target).value)" />
        </label>
        <label>
          {{ t('documents.file') }}
          <input #fileInput type="file" name="file" accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png" (change)="pick($any($event.target).files)" />
        </label>
      </div>
      <p class="muted">{{ t('documents.fileHint') }}</p>
      <div class="dialog-actions">
        <button type="submit" class="upload" [disabled]="!canUpload() || busy()">{{ t('documents.uploadButton') }}</button>
      </div>
    </form>

    <ul class="document-list">
      @for (d of documents(); track d.id) {
        <li class="document-row panel">
          <div class="document-main">
            <span class="tag" [attr.data-type]="d.type">{{ t(typeLabel(d.type)) }}</span>
            <strong>{{ d.title }}</strong>
            <span class="muted document-meta">
              {{ d.fileName }} · {{ size(d.sizeBytes) }} · {{ t('documents.meta', { date: i18n.date(d.uploadedAt), author: d.uploadedBy }) }}
            </span>
          </div>
          <div class="document-actions">
            @if (confirming() === d.id) {
              <span class="confirm-text">{{ t('documents.confirmDelete') }}</span>
              <button type="button" class="secondary cancel-delete" (click)="confirming.set(null)">{{ t('common.cancel') }}</button>
              <button type="button" class="danger confirm-delete" (click)="remove(d)">{{ t('documents.delete') }}</button>
            } @else {
              <button type="button" class="secondary download-document" (click)="download(d)">{{ t('documents.download') }}</button>
              <button type="button" class="secondary delete-document" (click)="confirming.set(d.id)">{{ t('documents.delete') }}</button>
            }
          </div>
        </li>
      } @empty {
        <li class="muted panel">{{ t('documents.empty') }}</li>
      }
    </ul>
  `,
})
export class DocumentsPanel {
  private readonly api = inject(ApiService);
  private readonly persona = inject(PersonaService);
  private readonly toasts = inject(ToastService);
  protected readonly i18n = inject(I18n);
  protected readonly t = this.i18n.t;

  readonly athleteId = input.required<number>();

  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  protected readonly types = computed(() => documentTypesFor(this.persona.current().role));
  protected readonly documents = signal<AthleteDocument[]>([]);
  protected readonly type = signal<DocumentType>('General');
  protected readonly title = signal('');
  protected readonly file = signal<File | null>(null);
  protected readonly confirming = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly canUpload = computed(() => !!this.file() && this.title().trim().length > 0);

  constructor() {
    effect(() => {
      const athleteId = this.athleteId();
      const types = this.types();
      untracked(() => {
        if (!types.includes(this.type())) this.type.set(types[0]);
        this.confirming.set(null);
        this.load(athleteId);
      });
    });
  }

  protected typeLabel(type: DocumentType): TranslationKey {
    return `documents.types.${type}`;
  }

  protected size(bytes: number): string {
    return bytes < 1024 * 1024
      ? `${this.i18n.number(Math.max(1, Math.round(bytes / 1024)))} KB`
      : `${this.i18n.number(bytes / 1024 / 1024, 1, 1)} MB`;
  }

  protected pick(files: FileList | File[] | null): void {
    const file = files?.[0] ?? null;
    if (file && file.size > MAX_BYTES) {
      this.error.set(this.t('documents.tooLarge'));
      this.file.set(null);
      return;
    }
    this.error.set(null);
    this.file.set(file);
  }

  protected upload(event: Event): void {
    event.preventDefault();
    const file = this.file();
    if (!file || !this.canUpload()) return;
    const athleteId = this.athleteId();
    this.busy.set(true);
    this.api.uploadDocument(athleteId, file, this.type(), this.title().trim()).subscribe({
      next: () => {
        this.busy.set(false);
        this.title.set('');
        this.file.set(null);
        const input = this.fileInput()?.nativeElement;
        if (input) input.value = '';
        this.toasts.show(this.t('documents.uploaded'));
        this.load(athleteId);
      },
      error: (e) => {
        this.busy.set(false);
        this.fail(e);
      },
    });
  }

  protected download(d: AthleteDocument): void {
    this.api.documentContent(d.athleteId, d.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = d.fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 10_000);
      },
      error: (e) => this.fail(e),
    });
  }

  protected remove(d: AthleteDocument): void {
    this.api.deleteDocument(d.athleteId, d.id).subscribe({
      next: () => {
        this.confirming.set(null);
        this.toasts.show(this.t('documents.deleted'));
        this.load(d.athleteId);
      },
      error: (e) => this.fail(e),
    });
  }

  private load(athleteId: number): void {
    this.api.documents(athleteId).subscribe({
      next: (list) => {
        this.documents.set(list);
        this.error.set(null);
      },
      error: (e) => this.fail(e),
    });
  }

  private fail(e: unknown): void {
    this.error.set(describeError(e, this.persona.current().role, this.t));
  }
}
