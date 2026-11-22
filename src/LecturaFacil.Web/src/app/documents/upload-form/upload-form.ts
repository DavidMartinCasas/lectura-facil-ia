// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { RouterLink } from '@angular/router';
import { DocumentSubmission } from '../../core/documents/document.models';
import {
  MAX_TITLE_LENGTH,
  validatePdf,
  validateText,
} from '../../core/documents/upload-validation';
import { TURNSTILE_TEXTS, UPLOAD_TEXTS } from '../../core/ui-texts';
import { TurnstileWidgetComponent } from '../turnstile-widget/turnstile-widget';

type UploadMode = 'text' | 'pdf';

/**
 * Formulario para pegar un texto o elegir (o arrastrar) un PDF. Valida en el navegador por comodidad y emite lo que
 * hay que enviar; no llama a la API. Si el desafío anti-bots está activo (`turnstileSiteKey`), lo muestra y añade su
 * token al envío; el servidor lo valida siempre.
 */
@Component({
  selector: 'app-upload-form',
  imports: [MatButtonModule, MatButtonToggleModule, RouterLink, TurnstileWidgetComponent],
  templateUrl: './upload-form.html',
  styleUrl: './upload-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UploadFormComponent {
  readonly busy = input(false);
  readonly turnstileSiteKey = input<string | null>(null);
  readonly submitted = output<DocumentSubmission>();

  protected readonly texts = UPLOAD_TEXTS;
  protected readonly maxTitleLength = MAX_TITLE_LENGTH;
  protected readonly mode = signal<UploadMode>('text');
  protected readonly title = signal('');
  protected readonly text = signal('');
  protected readonly file = signal<File | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly dragging = signal(false);
  protected readonly humanToken = signal<string | null>(null);
  private readonly turnstile = viewChild(TurnstileWidgetComponent);
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');

  /** Palabras del texto pegado, para la barra de abajo. */
  protected readonly wordCount = computed(() => {
    const words = this.text().trim();
    return this.texts.words(words === '' ? 0 : words.split(/\s+/u).length);
  });

  protected onHumanToken(token: string | null): void {
    this.humanToken.set(token);
    if (token !== null) {
      this.error.set(null);
    }
  }

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

  /** Quita el fichero elegido y vacía el campo, para poder elegir el mismo otra vez. */
  protected removeFile(): void {
    this.selectFile(null);
    const field = this.fileInput()?.nativeElement;
    if (field) {
      field.value = '';
      field.focus();
    }
  }

  /** Tamaño legible: «850 KB», «1,2 MB». */
  protected fileSize(bytes: number): string {
    const kilobytes = bytes / 1024;
    return kilobytes < 1024
      ? `${Math.max(1, Math.round(kilobytes)).toLocaleString('es')} KB`
      : `${(kilobytes / 1024).toLocaleString('es', { maximumFractionDigits: 1 })} MB`;
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
      const error = validateText(this.text()) ?? this.humanCheckError();
      this.error.set(error);
      if (error === null) {
        this.emit({ kind: 'text', text: this.text(), title: this.title() });
      }
      return;
    }

    const file = this.file();
    const error = (await validatePdf(file)) ?? this.humanCheckError();
    this.error.set(error);
    if (error === null && file !== null) {
      this.emit({ kind: 'pdf', file });
    }
  }

  private humanCheckError(): string | null {
    return this.turnstileSiteKey() !== null && this.humanToken() === null
      ? TURNSTILE_TEXTS.required
      : null;
  }

  /** Envía con el token (si lo hay) y pide un desafío nuevo: cada token vale para un solo envío. */
  private emit(submission: DocumentSubmission): void {
    const token = this.humanToken();
    this.submitted.emit(token === null ? submission : { ...submission, humanToken: token });
    if (token !== null) {
      this.turnstile()?.reset();
    }
  }

  private selectFile(file: File | null): void {
    this.file.set(file);
    this.error.set(null);
  }
}
