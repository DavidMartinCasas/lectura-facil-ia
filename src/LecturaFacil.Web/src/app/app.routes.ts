// SPDX-License-Identifier: Apache-2.0
import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { APP_TEXTS, LIST_TEXTS, LOGIN_TEXTS, UPLOAD_TEXTS } from './core/ui-texts';

export const routes: Routes = [
  {
    path: '',
    title: APP_TEXTS.name,
    loadComponent: () => import('./home/home-page').then((m) => m.HomePage),
  },
  {
    path: 'entrar',
    title: `${LOGIN_TEXTS.pageTitle} · ${APP_TEXTS.name}`,
    loadComponent: () => import('./auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'nuevo',
    canActivate: [authGuard],
    title: `${UPLOAD_TEXTS.pageTitle} · ${APP_TEXTS.name}`,
    loadComponent: () =>
      import('./documents/pages/new-document-page').then((m) => m.NewDocumentPage),
  },
  {
    path: 'documentos',
    canActivate: [authGuard],
    title: `${LIST_TEXTS.pageTitle} · ${APP_TEXTS.name}`,
    loadComponent: () => import('./documents/pages/documents-page').then((m) => m.DocumentsPage),
  },
  {
    path: 'documentos/:id',
    canActivate: [authGuard],
    title: `Documento · ${APP_TEXTS.name}`,
    loadComponent: () => import('./documents/pages/document-page').then((m) => m.DocumentPage),
  },
  { path: '**', redirectTo: '' },
];
