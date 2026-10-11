import { Component, computed, inject, input } from '@angular/core';
import { I18n } from '../i18n/i18n.service';
import { ReadinessStatus } from '../api/models';

@Component({
  selector: 'app-readiness-badge',
  template: `
    <span class="badge" [class]="'badge ' + status().toLowerCase()">{{ label() }}</span>
    @if (reasons().length) {
      <ul class="reasons">
        @for (r of reasons(); track r) {
          <li>{{ r }}</li>
        }
      </ul>
    }
  `,
})
export class ReadinessBadge {
  readonly status = input.required<ReadinessStatus>();
  readonly reasons = input<string[]>([]);

  private readonly i18n = inject(I18n);
  protected readonly label = computed(() => this.i18n.t(`readiness.${this.status()}`));
}
