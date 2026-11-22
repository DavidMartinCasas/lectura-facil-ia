// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LOGIN_PATH } from './core/auth/auth.guard';
import { AuthService } from './core/auth/auth.service';
import { PublicConfigService } from './core/config/public-config.service';
import { APP_TEXTS } from './core/ui-texts';

@Component({
  selector: 'app-root',
  imports: [MatButtonModule, MatToolbarModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <a class="skip-link" href="#main">{{ texts.skipToContent }}</a>
    <mat-toolbar class="topbar">
      <a class="brand" routerLink="/">
        <svg class="brand-mark" viewBox="0 0 28 28" aria-hidden="true" focusable="false">
          <rect x="1" y="1" width="26" height="26" rx="7" class="brand-mark__tile" />
          <rect x="7" y="8" width="14" height="2.6" rx="1.3" class="brand-mark__line" />
          <rect x="7" y="12.7" width="10" height="2.6" rx="1.3" class="brand-mark__line" />
          <rect x="7" y="17.4" width="12" height="2.6" rx="1.3" class="brand-mark__line" />
        </svg>
        <span>{{ texts.name }}</span>
      </a>
      <nav [attr.aria-label]="texts.mainNavigation">
        <a routerLink="/nuevo" routerLinkActive="active" ariaCurrentWhenActive="page">{{
          texts.navNew
        }}</a>
        <a
          routerLink="/documentos"
          routerLinkActive="active"
          [routerLinkActiveOptions]="{ exact: true }"
          ariaCurrentWhenActive="page"
          >{{ texts.navList }}</a
        >
        <a routerLink="/ejemplos" routerLinkActive="active" ariaCurrentWhenActive="page">{{
          texts.navSamples
        }}</a>
      </nav>
      <div class="session">
        @if (auth.user(); as user) {
          <span class="user">{{ texts.signedInAs }} {{ user.email }}</span>
          <button mat-button type="button" (click)="logout()">{{ texts.logout }}</button>
        } @else if (auth.user() === null) {
          <a mat-flat-button routerLink="/entrar">{{ texts.navLogin }}</a>
        }
      </div>
    </mat-toolbar>
    @if (config.notice(); as notice) {
      <p class="site-notice" role="note">
        <strong>{{ texts.noticeLabel }}:</strong> {{ notice }}
      </p>
    }
    @if (logoutFailed()) {
      <p class="logout-error" role="alert">{{ texts.logoutFailed }}</p>
    }
    <main id="main" tabindex="-1">
      <router-outlet />
    </main>
  `,
  styles: `
    .topbar {
      display: flex;
      flex-wrap: wrap;
      align-items: stretch;
      gap: 0 2rem;
      height: auto;
      min-height: 64px;
      padding-inline: var(--lf-gutter);
      background: var(--lf-surface);
      color: var(--lf-ink);
      border-block-end: 1px solid var(--lf-rule);
      font-family: var(--lf-font-sans);
    }
    .brand {
      display: inline-flex;
      align-items: center;
      gap: 0.625rem;
      color: inherit;
      text-decoration: none;
      font-size: 1.1875rem;
      font-weight: 700;
      letter-spacing: -0.005em;
    }
    .brand-mark {
      inline-size: 28px;
      block-size: 28px;
    }
    .brand-mark__tile {
      fill: var(--lf-primary);
    }
    .brand-mark__line {
      fill: var(--lf-on-primary);
    }
    nav {
      display: flex;
      flex-wrap: wrap;
      align-items: stretch;
      gap: 0.25rem 1.5rem;
    }
    nav a {
      display: inline-flex;
      align-items: center;
      min-block-size: 44px;
      color: var(--lf-muted);
      text-decoration: none;
      font-size: 1rem;
      font-weight: 600;
      border-block-end: 3px solid transparent;
      padding-block-start: 3px;
    }
    nav a:hover {
      color: var(--lf-ink);
    }
    nav a.active {
      color: var(--lf-ink);
      border-block-end-color: var(--lf-primary);
    }
    .session {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.25rem 0.75rem;
      margin-inline-start: auto;
    }
    .user {
      font-size: 0.9375rem;
      color: var(--lf-muted);
    }
    .site-notice,
    .logout-error {
      margin: 0;
      padding: 0.75rem var(--lf-gutter);
      font-size: 0.9375rem;
    }
    .site-notice {
      background: var(--lf-amber-soft);
      color: var(--lf-ink);
      border-block-end: 1px solid var(--lf-rule);
    }
    .logout-error {
      background: var(--lf-danger-soft);
      color: var(--lf-ink);
    }
    main:focus {
      outline: none;
    }
    @media (max-width: 48rem) {
      .topbar {
        gap: 0 1rem;
        padding-block: 0.5rem 0;
      }
      .brand {
        min-block-size: 44px;
      }
      nav {
        order: 3;
        inline-size: 100%;
        flex-wrap: nowrap;
        gap: 0 1.25rem;
        overflow-x: auto;
      }
      nav a {
        font-size: 0.9375rem;
        white-space: nowrap;
      }
      .user {
        font-size: 0.8125rem;
        overflow-wrap: anywhere;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly config = inject(PublicConfigService);
  private readonly router = inject(Router);
  protected readonly texts = APP_TEXTS;
  protected readonly logoutFailed = signal(false);

  constructor() {
    this.config.load();
  }

  /** Cierra la sesión en el servidor y vuelve al login. Si el servidor no la cierra, lo dice y se queda aquí. */
  protected logout(): void {
    this.logoutFailed.set(false);
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl(LOGIN_PATH),
      error: () => this.logoutFailed.set(true),
    });
  }
}
