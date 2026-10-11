import { Component, inject, input, signal } from '@angular/core';
import { I18n } from '../i18n/i18n.service';

let nextId = 0;

/** A small "?" that explains a term on hover or keyboard focus; Escape hides it again (WCAG 1.4.13). */
@Component({
  selector: 'app-info-tip',
  host: { class: 'info-tip-host', '[class.dismissed]': 'dismissed()', '(mouseleave)': 'dismissed.set(false)' },
  template: `
    <button
      type="button"
      class="info-tip"
      [attr.aria-label]="t('squad.tip.about', { label: label() })"
      [attr.aria-describedby]="id"
      (keydown.escape)="dismissed.set(true)"
      (blur)="dismissed.set(false)"
    >
      ?
    </button>
    <span role="tooltip" class="tip" [id]="id">{{ text() }}</span>
  `,
})
export class InfoTip {
  readonly label = input.required<string>();
  readonly text = input.required<string>();

  protected readonly id = `info-tip-${++nextId}`;
  protected readonly dismissed = signal(false);
  protected readonly t = inject(I18n).t;
}
