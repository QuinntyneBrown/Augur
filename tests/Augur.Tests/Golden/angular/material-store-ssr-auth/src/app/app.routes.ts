import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';
import { Home } from './pages/home/home';

/** The home page loads with the app; every other page is loaded on demand. */
export const routes: Routes = [
  { path: '', component: Home, title: 'Home | Contoso Orders' },
  {
    path: 'notes',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/notes/notes').then((m) => m.Notes),
    title: 'Notes | Contoso Orders',
  },
  {
    path: 'sign-in',
    loadComponent: () => import('./auth/sign-in').then((m) => m.SignIn),
    title: 'Sign in | Contoso Orders',
  },
  {
    path: 'sign-out',
    loadComponent: () => import('./auth/sign-out').then((m) => m.SignOut),
    title: 'Sign out | Contoso Orders',
  },
  {
    path: '**',
    loadComponent: () => import('./pages/not-found/not-found').then((m) => m.NotFound),
    title: 'Page not found | Contoso Orders',
  },
];
