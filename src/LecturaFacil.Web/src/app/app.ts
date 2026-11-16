// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { APP_TEXTS } from './core/ui-texts';

@Component({
  selector: 'app-root',
  imports: [MatButtonModule, MatToolbarModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <a class="skip-link" href="#main">{{ texts.skipToContent }}</a>
    <mat-toolbar>
      <a class="brand" routerLink="/">{{ texts.name }}</a>
      <nav [attr.aria-label]="texts.mainNavigation">
        <a mat-button routerLink="/nuevo" routerLinkActive="active" ariaCurrentWhenActive="page">{{
          texts.navNew
        }}</a>
        <a
          mat-button
          routerLink="/documentos"
          routerLinkActive="active"
          [routerLinkActiveOptions]="{ exact: true }"
          ariaCurrentWhenActive="page"
          >{{ texts.navList }}</a
        >
      </nav>
    </mat-toolbar>
    <main id="main" tabindex="-1">
      <router-outlet />
    </main>
  `,
  styles: `
    mat-toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem 1rem;
      height: auto;
      min-height: 64px;
    }
    .brand {
      color: inherit;
      text-decoration: none;
      font: var(--mat-sys-title-large);
    }
    nav {
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem;
    }
    .active {
      text-decoration: underline;
      text-underline-offset: 0.3em;
    }
    main:focus {
      outline: none;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly texts = APP_TEXTS;
}
