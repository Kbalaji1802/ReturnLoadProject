import { Routes } from '@angular/router';

import { MainLayout } from './layouts/main-layout/main-layout';

/**
 * Application routes. Feature views are lazy-loaded standalone components nested
 * inside the main layout, so the console frame renders once and only feature
 * chunks are fetched on navigation.
 */
export const routes: Routes = [
  {
    path: '',
    component: MainLayout,
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        title: 'Dashboard · ReturnLoad Admin',
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
