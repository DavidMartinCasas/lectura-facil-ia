// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { LOGIN_PATH } from './core/auth/auth.guard';
import { AuthService } from './core/auth/auth.service';
import { PublicConfigService } from './core/config/public-config.service';
import { ThemeMode, ThemeService } from './core/theme/theme.service';
import { APP_TEXTS } from './core/ui-texts';

const THEME_LABELS: Record<ThemeMode, string> = {
  system: APP_TEXTS.themeSystem,
  light: APP_TEXTS.themeLight,
  dark: APP_TEXTS.themeDark,
};

/**
 * Marco de la aplicación: una barra superior con la marca, la navegación, el tema y la cuenta; debajo, toda la pantalla para el trabajo. La pantalla de entrar va sin barra: todavía no hay
 * nada a lo que ir. Sin sesión tampoco hay navegación: todas sus pantallas la exigen, así que solo queda «Entrar».
 */
@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly config = inject(PublicConfigService);
  protected readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  protected readonly texts = APP_TEXTS;
  protected readonly logoutFailed = signal(false);

  /** Nombre del tema actual, para el botón de tema. */
  protected readonly themeLabel = computed(() => THEME_LABELS[this.theme.mode()]);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );
  /** Sin barra en la pantalla de entrar. */
  protected readonly bare = computed(() => this.url().split('?')[0] === LOGIN_PATH);

  constructor() {
    this.config.load();
    // La portada no tiene guard: sin esto, la barra no sabría si enseñar el menú y la cuenta o el enlace para entrar.
    if (this.auth.user() === undefined) {
      this.auth.loadSession().subscribe();
    }
  }

  /** Inicial del correo para el círculo de la cuenta. */
  protected initial(email: string): string {
    return email.trim().charAt(0).toLocaleUpperCase('es') || '?';
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
