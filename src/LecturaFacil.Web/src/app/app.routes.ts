// SPDX-License-Identifier: Apache-2.0
import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Lectura Fácil IA',
    loadComponent: () => import('./home/home-page').then((m) => m.HomePage),
  },
  { path: '**', redirectTo: '' },
];
