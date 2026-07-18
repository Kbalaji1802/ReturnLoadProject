import { Routes } from '@angular/router';

import { MainLayout } from './layouts/main-layout/main-layout';
import { authGuard } from './core/auth/auth.guard';

/**
 * Application routes. /login is public; everything else renders inside the console frame
 * and is protected by {@link authGuard}. Feature views are lazy-loaded standalone components.
 */
export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in · ReturnLoad Admin',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: '',
    component: MainLayout,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', title: 'Dashboard · ReturnLoad Admin', loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard) },
      { path: 'drivers', title: 'Drivers · ReturnLoad Admin', loadComponent: () => import('./features/drivers/drivers').then((m) => m.Drivers) },
      { path: 'documents', title: 'Documents · ReturnLoad Admin', loadComponent: () => import('./features/documents/documents').then((m) => m.Documents) },
      { path: 'loads', title: 'Loads · ReturnLoad Admin', loadComponent: () => import('./features/loads/loads').then((m) => m.Loads) },
      { path: 'trips', title: 'Trips · ReturnLoad Admin', loadComponent: () => import('./features/trips/trips').then((m) => m.Trips) },
      { path: 'settings', title: 'Settings · ReturnLoad Admin', loadComponent: () => import('./features/settings/settings').then((m) => m.Settings) },
    ],
  },
  { path: '**', redirectTo: '' },
];
