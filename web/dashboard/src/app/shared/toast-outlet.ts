import { Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Component({
  selector: 'app-toast-outlet',
  template: `
    <!-- The live region stays in the DOM so screen readers announce each new message. -->
    <div class="toast-region" role="status" aria-live="polite">
      @if (toasts.current(); as toast) {
        <div class="toast">
          <span>{{ toast.message }}</span>
          @if (toast.actionLabel) {
            <button type="button" class="link-button" (click)="toasts.act()">{{ toast.actionLabel }}</button>
          }
          <button type="button" class="icon-button" aria-label="Dismiss" (click)="toasts.dismiss()">×</button>
        </div>
      }
    </div>
  `,
})
export class ToastOutlet {
  protected readonly toasts = inject(ToastService);
}
