// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LOGIN_PATH } from './core/auth/auth.guard';
import { AuthService } from './core/auth/auth.service';
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
      <div class="session">
        @if (auth.user(); as user) {
          <span class="user">{{ texts.signedInAs }} {{ user.email }}</span>
          <button mat-stroked-button type="button" (click)="logout()">{{ texts.logout }}</button>
        } @else if (auth.user() === null) {
          <a mat-button routerLink="/entrar">{{ texts.navLogin }}</a>
        }
      </div>
    </mat-toolbar>
    @if (logoutFailed()) {
      <p class="logout-error" role="alert">{{ texts.logoutFailed }}</p>
    }
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
    .session {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin-inline-start: auto;
    }
    .user {
      font: var(--mat-sys-body-medium);
    }
    .logout-error {
      margin: 0;
      padding: 0.75rem 1rem;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    main:focus {
      outline: none;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly texts = APP_TEXTS;
  protected readonly logoutFailed = signal(false);

  /** Cierra la sesión en el servidor y vuelve al login. Si el servidor no la cierra, lo dice y se queda aquí. */
  protected logout(): void {
    this.logoutFailed.set(false);
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl(LOGIN_PATH),
      error: () => this.logoutFailed.set(true),
    });
  }
}
