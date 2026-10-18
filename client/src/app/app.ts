import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ApiLoadingService } from '@ismailza/ngx-api-client';
import { AuthService } from '@ismailza/ngx-auth-client';
import { Header } from './layout/header/header';
import { SessionPanel } from './layout/session-panel/session-panel';
import { Toasts } from './layout/toasts/toasts';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Header, SessionPanel, Toasts],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly loading = inject(ApiLoadingService);
}
