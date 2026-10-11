import { Component, DestroyRef, ElementRef, afterNextRender, inject, input, output, viewChild } from '@angular/core';

let nextId = 0;

/**
 * A native modal <dialog>: focus moves into it, Escape closes it, and focus returns to where it was.
 * Render it with @if; it opens itself and emits `closed` when the user dismisses it.
 */
@Component({
  selector: 'app-modal-dialog',
  template: `
    <dialog #dialog [class]="kind()" [attr.aria-labelledby]="headingId" (close)="closed.emit()">
      <h2 [id]="headingId">{{ heading() }}</h2>
      <ng-content />
    </dialog>
  `,
})
export class ModalDialog {
  readonly heading = input.required<string>();
  /** CSS class on the <dialog>, e.g. "confirm". */
  readonly kind = input('');
  readonly closed = output<void>();

  protected readonly headingId = `dialog-heading-${++nextId}`;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    const opener = document.activeElement as HTMLElement | null;

    afterNextRender(() => {
      const dialog = this.dialog().nativeElement;
      // jsdom (unit tests) has no showModal; the open attribute is enough there.
      if (typeof dialog.showModal === 'function') dialog.showModal();
      else dialog.setAttribute('open', '');
    });

    inject(DestroyRef).onDestroy(() => opener?.focus());
  }
}
