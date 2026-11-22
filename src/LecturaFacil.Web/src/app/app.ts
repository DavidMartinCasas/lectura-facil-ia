// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LOGIN_PATH } from './core/auth/auth.guard';
import { AuthService } from './core/auth/auth.service';
import { PublicConfigService } from './core/config/public-config.service';
import { APP_TEXTS } from './core/ui-texts';

/**
 * Marco de la aplicación: barra lateral con la marca, la acción principal ("Nuevo documento"), la navegación y la
 * cuenta; a la derecha, el área de trabajo. En pantallas estrechas la barra lateral pasa a ser una cabecera.
 */
@Component({
  selector: 'app-root',
  imports: [MatButtonModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <a class="skip-link" href="#main">{{ texts.skipToContent }}</a>
    <div class="shell">
      <header class="sidebar">
        <a class="brand" routerLink="/">
          <svg class="brand-mark" viewBox="0 0 28 28" aria-hidden="true" focusable="false">
            <rect x="1" y="1" width="26" height="26" rx="7" class="brand-mark__tile" />
            <rect x="7" y="8" width="14" height="2.6" rx="1.3" class="brand-mark__line" />
            <rect x="7" y="12.7" width="10" height="2.6" rx="1.3" class="brand-mark__line" />
            <rect x="7" y="17.4" width="12" height="2.6" rx="1.3" class="brand-mark__line" />
          </svg>
          <span>{{ texts.name }}</span>
        </a>

        <nav class="nav" [attr.aria-label]="texts.mainNavigation">
          <a
            class="nav-new"
            routerLink="/nuevo"
            routerLinkActive="active"
            ariaCurrentWhenActive="page"
          >
            <svg viewBox="0 0 20 20" aria-hidden="true" focusable="false">
              <path d="M10 4v12M4 10h12" />
            </svg>
            {{ texts.navNew }}
          </a>
          <a
            routerLink="/documentos"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: true }"
            ariaCurrentWhenActive="page"
          >
            <svg viewBox="0 0 20 20" aria-hidden="true" focusable="false">
              <path
                d="M5 2.75h6.5l3.75 3.75v10.75H5z M11.25 2.75v4h4 M7.75 10.25h4.5 M7.75 13.25h4.5"
              />
            </svg>
            {{ texts.navList }}
          </a>
          <a routerLink="/ejemplos" routerLinkActive="active" ariaCurrentWhenActive="page">
            <svg viewBox="0 0 20 20" aria-hidden="true" focusable="false">
              <path
                d="M2.75 4.5h5.5c.97 0 1.75.78 1.75 1.75v10c0-.83-.67-1.5-1.5-1.5H2.75z M17.25 4.5h-5.5c-.97 0-1.75.78-1.75 1.75v10c0-.83.67-1.5 1.5-1.5h5.75z"
              />
            </svg>
            {{ texts.navSamples }}
          </a>
        </nav>

        @if (auth.user() !== undefined) {
          <div class="account">
            @if (auth.user(); as user) {
              <p class="user">
                <span class="user-label">{{ texts.signedInAs }}</span>
                <span class="user-email">{{ user.email }}</span>
              </p>
              <button mat-stroked-button type="button" (click)="logout()">
                {{ texts.logout }}
              </button>
            } @else if (auth.user() === null) {
              <a mat-flat-button routerLink="/entrar">{{ texts.navLogin }}</a>
            }
          </div>
        }
      </header>

      <div class="workspace">
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
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .shell {
      display: grid;
      grid-template-columns: 16rem minmax(0, 1fr);
      min-block-size: 100dvh;
    }
    .sidebar {
      position: sticky;
      inset-block-start: 0;
      block-size: 100dvh;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
      padding: 1.25rem 0.875rem;
      background: var(--lf-surface);
      border-inline-end: 1px solid var(--lf-rule);
      overflow-y: auto;
    }
    .brand {
      display: inline-flex;
      align-items: center;
      gap: 0.625rem;
      min-block-size: 44px;
      padding-inline: 0.5rem;
      color: var(--lf-ink);
      text-decoration: none;
      font-size: 1.125rem;
      font-weight: 700;
      letter-spacing: -0.005em;
    }
    .brand-mark {
      inline-size: 28px;
      block-size: 28px;
      flex: none;
    }
    .brand-mark__tile {
      fill: var(--lf-primary);
    }
    .brand-mark__line {
      fill: var(--lf-on-primary);
    }
    .nav {
      display: grid;
      gap: 0.25rem;
    }
    .nav a {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      min-block-size: 44px;
      padding-inline: 0.75rem;
      border-radius: var(--lf-radius-s);
      color: var(--lf-muted);
      text-decoration: none;
      font-size: 0.9375rem;
      font-weight: 600;
    }
    .nav svg {
      inline-size: 20px;
      block-size: 20px;
      flex: none;
      fill: none;
      stroke: currentColor;
      stroke-width: 1.6;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
    .nav a:hover {
      color: var(--lf-ink);
      background: var(--lf-page);
    }
    .nav a.active {
      color: var(--lf-ink);
      background: var(--lf-primary-soft);
    }
    /* La acción principal se distingue del resto de la navegación. */
    .nav .nav-new {
      margin-block-end: 0.75rem;
      color: var(--lf-on-primary);
      background: var(--lf-primary);
    }
    .nav .nav-new:hover,
    .nav .nav-new.active {
      color: var(--lf-on-primary);
      background: color-mix(in srgb, var(--lf-primary) 85%, var(--lf-ink));
    }
    .nav .nav-new svg {
      stroke-width: 2;
    }
    .account {
      display: grid;
      gap: 0.625rem;
      margin-block-start: auto;
      padding: 1rem 0.5rem 0;
      border-block-start: 1px solid var(--lf-rule);
    }
    .user {
      display: grid;
      margin: 0;
      line-height: 1.35;
    }
    .user-label {
      font-size: 0.8125rem;
      color: var(--lf-muted);
    }
    .user-email {
      font-size: 0.875rem;
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .account button,
    .account a {
      justify-self: start;
    }
    .workspace {
      min-inline-size: 0;
      display: flex;
      flex-direction: column;
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
    main {
      flex: 1;
    }
    main:focus {
      outline: none;
    }
    /* Pantallas estrechas: la barra lateral pasa a ser una cabecera con la navegación debajo. */
    @media (max-width: 63.99rem) {
      .shell {
        grid-template-columns: minmax(0, 1fr);
      }
      .sidebar {
        position: static;
        block-size: auto;
        flex-direction: row;
        flex-wrap: wrap;
        align-items: center;
        gap: 0.5rem 1rem;
        padding: 0.5rem var(--lf-gutter) 0.625rem;
        border-inline-end: 0;
        border-block-end: 1px solid var(--lf-rule);
        overflow: visible;
      }
      .brand {
        padding-inline: 0;
      }
      .account {
        order: 2;
        margin: 0 0 0 auto;
        padding: 0;
        border: 0;
        display: flex;
        align-items: center;
        gap: 0.75rem;
      }
      .user-label {
        position: absolute;
        inline-size: 1px;
        block-size: 1px;
        overflow: hidden;
        clip-path: inset(50%);
        white-space: nowrap;
      }
      .user-email {
        font-weight: 400;
        font-size: 0.8125rem;
        color: var(--lf-muted);
      }
      .nav {
        order: 3;
        inline-size: 100%;
        display: flex;
        gap: 0.375rem;
        overflow-x: auto;
      }
      .nav a {
        white-space: nowrap;
        padding-inline: 0.625rem;
      }
      .nav .nav-new {
        margin: 0 0.25rem 0 0;
      }
    }
    /* Móvil: la navegación cabe en una línea sin desplazarse; solo la acción principal lleva icono. */
    @media (max-width: 40rem) {
      .nav {
        gap: 0.25rem;
        overflow-x: visible;
      }
      .nav a {
        padding-inline: 0.5rem;
        font-size: 0.875rem;
      }
      .nav a:not(.nav-new) svg {
        display: none;
      }
      .nav .nav-new {
        gap: 0.375rem;
        padding-inline: 0.625rem 0.75rem;
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
