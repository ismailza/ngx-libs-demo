import { Service, signal } from '@angular/core';

export interface Toast {
  readonly id: number;
  readonly kind: 'success' | 'error';
  readonly message: string;
  readonly detail?: string;
}

const MAX_VISIBLE = 4;
const SUCCESS_MS = 4000;
const ERROR_MS = 8000;

/** App-wide toasts, fed by the ngx-api-client handlers. */
@Service()
export class Notifications {
  private nextId = 0;
  private readonly _toasts = signal<readonly Toast[]>([]);
  readonly toasts = this._toasts.asReadonly();

  success(message: string): void {
    this.push({ kind: 'success', message });
  }

  error(message: string, detail?: string): void {
    this.push({ kind: 'error', message, detail });
  }

  dismiss(id: number): void {
    this._toasts.update((toasts) => toasts.filter((toast) => toast.id !== id));
  }

  private push(toast: Omit<Toast, 'id'>): void {
    const id = this.nextId++;
    this._toasts.update((toasts) => [...toasts.slice(-(MAX_VISIBLE - 1)), { ...toast, id }]);
    setTimeout(() => this.dismiss(id), toast.kind === 'error' ? ERROR_MS : SUCCESS_MS);
  }
}
