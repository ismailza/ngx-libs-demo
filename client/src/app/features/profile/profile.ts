import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AuthService } from '@ismailza/ngx-auth-client';

/** `loadProfile()` asks the adapter for the profile and fills `auth.profile()`. */
@Component({
  selector: 'app-profile',
  imports: [DatePipe],
  templateUrl: './profile.html',
  styleUrl: './profile.css',
})
export default class Profile implements OnInit {
  protected readonly auth = inject(AuthService);
  protected readonly error = signal<string | null>(null);

  protected readonly memberSince = computed(() => {
    const value = this.auth.profile()?.attributes?.['memberSince'];
    return typeof value === 'string' ? value : null;
  });

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.error.set(null);
    try {
      await this.auth.loadProfile();
    } catch {
      this.error.set('Could not load the profile.');
    }
  }
}
