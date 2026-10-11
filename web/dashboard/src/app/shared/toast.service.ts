import { Service, signal } from '@angular/core';

export interface Toast {
  message: string;
  actionLabel?: string;
  action?: () => void;
}

const VISIBLE_MS = 8000;

/** One short confirmation at a time, optionally with an action such as Undo. */
@Service()
export class ToastService {
  private readonly toast = signal<Toast | null>(null);
  private timer: ReturnType<typeof setTimeout> | undefined;

  readonly current = this.toast.asReadonly();

  show(message: string, action?: { label: string; run: () => void }): void {
    clearTimeout(this.timer);
    this.toast.set({ message, actionLabel: action?.label, action: action?.run });
    this.timer = setTimeout(() => this.dismiss(), VISIBLE_MS);
  }

  /** Runs the toast's action (e.g. Undo) and closes it. */
  act(): void {
    const action = this.toast()?.action;
    this.dismiss();
    action?.();
  }

  dismiss(): void {
    clearTimeout(this.timer);
    this.toast.set(null);
  }
}
