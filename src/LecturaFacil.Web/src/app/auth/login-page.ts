// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { safeReturnUrl } from '../core/auth/auth.guard';
import { AuthService } from '../core/auth/auth.service';
import { ApiError } from '../core/errors/api-error';
import { APP_TEXTS, ERROR_TEXTS, LOGIN_TEXTS } from '../core/ui-texts';

/** Pantalla "Entrar": correo y contraseña. Al entrar, va a la pantalla que se pidió o a la portada. */
@Component({
  selector: 'app-login-page',
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule, RouterLink],
  templateUrl: './login-page.html',
  styleUrls: ['../documents/pages/page.scss', './login-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Parámetro `volver` de la URL (enlazado por el router). */
  readonly volver = input<string>();

  protected readonly texts = LOGIN_TEXTS;
  protected readonly appTexts = APP_TEXTS;
  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // Si ya hay sesión, no hace falta entrar. Además, esta llamada deja la cookie antiforgery que necesita el login.
    this.auth.loadSession().subscribe((authenticated) => {
      if (authenticated) {
        this.goToTarget();
      }
    });
  }

  protected onEmailInput(event: Event): void {
    this.email.set((event.target as HTMLInputElement).value);
  }

  protected onPasswordInput(event: Event): void {
    this.password.set((event.target as HTMLInputElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    if (this.busy()) {
      return;
    }
    const email = this.email().trim();
    if (!email || !this.password()) {
      this.error.set(this.texts.required);
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.auth.login(email, this.password()).subscribe({
      next: () => {
        this.busy.set(false);
        this.password.set('');
        this.goToTarget();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.password.set('');
        this.error.set(error instanceof ApiError ? error.message : ERROR_TEXTS.generic);
      },
    });
  }

  private goToTarget(): void {
    void this.router.navigateByUrl(safeReturnUrl(this.volver()));
  }
}
