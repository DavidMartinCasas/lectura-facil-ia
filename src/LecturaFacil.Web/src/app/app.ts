// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatToolbarModule],
  template: `
    <mat-toolbar>
      <span>Lectura Fácil IA</span>
    </mat-toolbar>
    <main>
      <router-outlet />
    </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
