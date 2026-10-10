import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ReadinessStatus } from '../api/models';

@Component({
  selector: 'app-readiness-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
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

  protected readonly label = computed(() =>
    ({ Ok: 'Ready', CheckIn: 'Check in', Missing: 'No check-in' })[this.status()],
  );
}
