import { Component, computed, inject, input } from '@angular/core';
import { PersonaService } from '../../api/persona.service';
import { I18n } from '../../i18n/i18n.service';
import { DocumentsPanel } from '../../shared/documents-panel';

/** The athlete's documents for coach and physio (AC-91); each sees only the types it needs (ADR-15). */
@Component({
  selector: 'app-documents-section',
  imports: [DocumentsPanel],
  template: `
    <section class="panel">
      <h2>{{ t('documents.title') }}</h2>
      <p class="muted">{{ t('documents.lead') }}</p>
      @if (!canUse()) {
        <p class="notice documents-restricted">{{ t('documents.restricted') }}</p>
      }
    </section>

    @if (canUse()) {
      <app-documents-panel [athleteId]="athleteId()" />
    }
  `,
})
export class DocumentsSection {
  private readonly persona = inject(PersonaService);
  protected readonly t = inject(I18n).t;

  readonly id = input.required<string>();

  protected readonly athleteId = computed(() => Number(this.id()));
  /** Identity documents are kept by the admin on the Settings page, not here. */
  protected readonly canUse = computed(() => ['Coach', 'Physio'].includes(this.persona.current().role));
}
