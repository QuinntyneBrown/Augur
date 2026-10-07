import { Routes } from '@angular/router';
import { Home } from './pages/home/home';

/** The home page loads with the app; every other page is loaded on demand. */
export const routes: Routes = [
  { path: '', component: Home, title: 'Home | Contoso Orders' },
  {
    path: '**',
    loadComponent: () => import('./pages/not-found/not-found').then((m) => m.NotFound),
    title: 'Page not found | Contoso Orders',
  },
];
