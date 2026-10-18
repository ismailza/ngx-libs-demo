import { JsonPipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { AuthService } from '@ismailza/ngx-auth-client';
import { RUNTIME_CONFIG } from '../../core/config/runtime-config';

/**
 * Live view of what the adapter holds: decoded claims, roles, and the time
 * left on the access token. Leave it open to watch the background refresh
 * replace the token shortly before it expires.
 */
@Component({
  selector: 'app-session-panel',
  imports: [JsonPipe],
  templateUrl: './session-panel.html',
  styleUrl: './session-panel.css',
})
export class SessionPanel {
  protected readonly auth = inject(AuthService);
  protected readonly provider = inject(RUNTIME_CONFIG).auth.provider;
  private readonly now = signal(Date.now());

  protected readonly expiresIn = computed(() =>
    formatDuration(this.claim('exp') - this.now() / 1000),
  );
  protected readonly issuedAgo = computed(() =>
    formatDuration(this.now() / 1000 - this.claim('iat')),
  );
  protected readonly tokenId = computed(() =>
    String(this.auth.claims()?.['jti'] ?? '').slice(0, 8),
  );

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  private claim(name: 'exp' | 'iat'): number {
    const value = this.auth.claims()?.[name];
    return typeof value === 'number' ? value : 0;
  }
}

function formatDuration(seconds: number): string {
  const s = Math.max(0, Math.round(seconds));
  return `${Math.floor(s / 60)}m ${String(s % 60).padStart(2, '0')}s`;
}
