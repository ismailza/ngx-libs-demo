import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '@ismailza/ngx-auth-client';
import { RUNTIME_CONFIG } from '../../core/config/runtime-config';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.html',
})
export default class Home {
  protected readonly auth = inject(AuthService);
  protected readonly provider = inject(RUNTIME_CONFIG).auth.provider;
}
