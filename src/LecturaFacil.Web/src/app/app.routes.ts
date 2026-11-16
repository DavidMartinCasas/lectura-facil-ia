// SPDX-License-Identifier: Apache-2.0
import { Routes } from '@angular/router';
import { APP_TEXTS, LIST_TEXTS, UPLOAD_TEXTS } from './core/ui-texts';

export const routes: Routes = [
  {
    path: '',
    title: APP_TEXTS.name,
    loadComponent: () => import('./home/home-page').then((m) => m.HomePage),
  },
  {
    path: 'nuevo',
    title: `${UPLOAD_TEXTS.pageTitle} · ${APP_TEXTS.name}`,
    loadComponent: () =>
      import('./documents/pages/new-document-page').then((m) => m.NewDocumentPage),
  },
  {
    path: 'documentos',
    title: `${LIST_TEXTS.pageTitle} · ${APP_TEXTS.name}`,
    loadComponent: () => import('./documents/pages/documents-page').then((m) => m.DocumentsPage),
  },
  {
    path: 'documentos/:id',
    title: `Documento · ${APP_TEXTS.name}`,
    loadComponent: () => import('./documents/pages/document-page').then((m) => m.DocumentPage),
  },
  { path: '**', redirectTo: '' },
];
