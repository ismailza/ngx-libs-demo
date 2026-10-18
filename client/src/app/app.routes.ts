import { Routes } from '@angular/router';
import { authGuard, type AuthRouteData } from '@ismailza/ngx-auth-client';
import { canSignInInApp, guestGuard, usesLocalProvider } from './core/auth/route-guards';

export const routes: Routes = [
  { path: '', title: 'Home', loadComponent: () => import('./features/home/home') },
  {
    path: 'login',
    title: 'Sign in',
    canMatch: [canSignInInApp],
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login'),
  },
  {
    path: 'register',
    title: 'Create account',
    canMatch: [usesLocalProvider],
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register/register'),
  },
  {
    path: 'notes',
    title: 'My notes',
    canActivate: [authGuard],
    loadComponent: () => import('./features/notes/notes'),
  },
  {
    path: 'playground',
    title: 'API playground',
    canActivate: [authGuard],
    loadComponent: () => import('./features/playground/playground'),
  },
  {
    path: 'profile',
    title: 'Profile',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile'),
  },
  {
    path: 'admin',
    title: 'Admin',
    canMatch: [usesLocalProvider],
    canActivate: [authGuard],
    data: { auth: { anyOf: ['admin'] } } satisfies AuthRouteData,
    loadComponent: () => import('./features/admin/admin'),
  },
  {
    path: 'forbidden',
    title: 'Access denied',
    loadComponent: () => import('./features/forbidden/forbidden'),
  },
  { path: '**', redirectTo: '' },
];
