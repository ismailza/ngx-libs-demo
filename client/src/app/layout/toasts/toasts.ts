import { Component, computed, inject } from '@angular/core';
import { Notifications } from '../../core/notifications/notifications';

/** Successes are announced politely, errors assertively. */
@Component({
  selector: 'app-toasts',
  templateUrl: './toasts.html',
  styleUrl: './toasts.css',
})
export class Toasts {
  protected readonly notifications = inject(Notifications);
  protected readonly successes = computed(() =>
    this.notifications.toasts().filter((t) => t.kind === 'success'),
  );
  protected readonly errors = computed(() =>
    this.notifications.toasts().filter((t) => t.kind === 'error'),
  );
}
