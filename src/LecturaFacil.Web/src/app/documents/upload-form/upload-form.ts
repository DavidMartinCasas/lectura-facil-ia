// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { DocumentSubmission } from '../../core/documents/document.models';
import {
  MAX_TITLE_LENGTH,
  validatePdf,
  validateText,
} from '../../core/documents/upload-validation';
import { UPLOAD_TEXTS } from '../../core/ui-texts';

type UploadMode = 'text' | 'pdf';

/**
 * Formulario para pegar un texto o elegir (o arrastrar) un PDF. Valida en el navegador por comodidad y emite lo que
 * hay que enviar; no llama a la API.
 */
@Component({
  selector: 'app-upload-form',
  imports: [MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule],
  templateUrl: './upload-form.html',
  styleUrl: './upload-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UploadFormComponent {
  readonly busy = input(false);
  readonly submitted = output<DocumentSubmission>();

  protected readonly texts = UPLOAD_TEXTS;
  protected readonly maxTitleLength = MAX_TITLE_LENGTH;
  protected readonly mode = signal<UploadMode>('text');
  protected readonly title = signal('');
  protected readonly text = signal('');
  protected readonly file = signal<File | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly dragging = signal(false);

  protected changeMode(mode: UploadMode): void {
    this.mode.set(mode);
    this.error.set(null);
  }

  protected onTextInput(event: Event): void {
    this.text.set((event.target as HTMLTextAreaElement).value);
    this.error.set(null);
  }

  protected onTitleInput(event: Event): void {
    this.title.set((event.target as HTMLInputElement).value);
  }

  protected onFileChange(event: Event): void {
    this.selectFile((event.target as HTMLInputElement).files?.item(0) ?? null);
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  protected onDragLeave(): void {
    this.dragging.set(false);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    this.selectFile(event.dataTransfer?.files.item(0) ?? null);
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }

    if (this.mode() === 'text') {
      const error = validateText(this.text());
      this.error.set(error);
      if (error === null) {
        this.submitted.emit({ kind: 'text', text: this.text(), title: this.title() });
      }
      return;
    }

    const file = this.file();
    const error = await validatePdf(file);
    this.error.set(error);
    if (error === null && file !== null) {
      this.submitted.emit({ kind: 'pdf', file });
    }
  }

  private selectFile(file: File | null): void {
    this.file.set(file);
    this.error.set(null);
  }
}
