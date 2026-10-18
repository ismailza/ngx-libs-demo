import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** `authGuard` sends signed-in users here when they lack a route's required role. */
@Component({
  selector: 'app-forbidden',
  imports: [RouterLink],
  templateUrl: './forbidden.html',
})
export default class Forbidden {}
