// SPDX-License-Identifier: Apache-2.0
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DocumentSubmission } from '../../core/documents/document.models';
import { MAX_PDF_BYTES } from '../../core/documents/upload-validation';
import { UPLOAD_TEXTS } from '../../core/ui-texts';
import { UploadFormComponent } from './upload-form';

describe('UploadFormComponent', () => {
  let fixture: ComponentFixture<UploadFormComponent>;
  let element: HTMLElement;
  let emitted: DocumentSubmission[];

  beforeEach(async () => {
    fixture = TestBed.createComponent(UploadFormComponent);
    emitted = [];
    fixture.componentInstance.submitted.subscribe((submission) => emitted.push(submission));
    await fixture.whenStable();
    element = fixture.nativeElement as HTMLElement;
  });

  async function type(selector: string, value: string): Promise<void> {
    const field = element.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function choosePdfMode(): Promise<void> {
    const toggle = Array.from(
      element.querySelectorAll<HTMLButtonElement>('mat-button-toggle button'),
    ).find((button) => button.textContent?.includes(UPLOAD_TEXTS.modePdf))!;
    toggle.click();
    await fixture.whenStable();
  }

  async function chooseFile(file: File): Promise<void> {
    const input = element.querySelector<HTMLInputElement>('input[type="file"]')!;
    Object.defineProperty(input, 'files', {
      value: { item: () => file, length: 1 },
      configurable: true,
    });
    input.dispatchEvent(new Event('change'));
    await fixture.whenStable();
  }

  async function submit(): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  it('starts with the text mode and an "Adaptar" button', () => {
    expect(element.querySelector('textarea')).not.toBeNull();
    expect(element.querySelector('button[type="submit"]')?.textContent?.trim()).toBe('Adaptar');
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });

  it('emits the pasted text with its title', async () => {
    await type('input[name="title"]', 'Bases');
    await type('textarea', 'Texto que hay que adaptar.');

    await submit();

    expect(emitted).toEqual([{ kind: 'text', text: 'Texto que hay que adaptar.', title: 'Bases' }]);
  });

  it('shows a message next to the field and emits nothing when the text is blank', async () => {
    await submit();

    expect(element.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      UPLOAD_TEXTS.textRequired,
    );
    expect(element.querySelector('textarea')?.getAttribute('aria-describedby')).toBe(
      'upload-error',
    );
    expect(emitted).toEqual([]);
  });

  it('emits a valid PDF', async () => {
    await choosePdfMode();
    const file = new File(['%PDF-1.7'], 'bases.pdf', { type: 'application/pdf' });
    await chooseFile(file);

    await submit();

    expect(element.textContent).toContain('bases.pdf');
    expect(emitted).toEqual([{ kind: 'pdf', file }]);
  });

  it('emits a PDF dropped on the drop zone', async () => {
    await choosePdfMode();
    const file = new File(['%PDF-1.7'], 'arrastrado.pdf', { type: 'application/pdf' });
    const drop = new Event('drop', { cancelable: true });
    Object.defineProperty(drop, 'dataTransfer', {
      value: { files: { item: () => file, length: 1 } },
    });
    element.querySelector('.drop-zone')!.dispatchEvent(drop);
    await fixture.whenStable();

    await submit();

    expect(element.textContent).toContain('arrastrado.pdf');
    expect(emitted).toEqual([{ kind: 'pdf', file }]);
  });

  it('rejects a file larger than 5 MB without emitting', async () => {
    await choosePdfMode();
    await chooseFile(new File(['%PDF-', new Uint8Array(MAX_PDF_BYTES)], 'grande.pdf'));

    await submit();

    expect(element.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      UPLOAD_TEXTS.fileTooLarge,
    );
    expect(emitted).toEqual([]);
  });

  it('rejects a file that is not a PDF without emitting', async () => {
    await choosePdfMode();
    await chooseFile(new File(['hola'], 'notas.pdf'));

    await submit();

    expect(element.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      UPLOAD_TEXTS.fileNotPdf,
    );
    expect(element.querySelector('input[type="file"]')?.getAttribute('aria-invalid')).toBe('true');
    expect(emitted).toEqual([]);
  });

  it('does not emit twice while busy', async () => {
    fixture.componentRef.setInput('busy', true);
    await type('textarea', 'Texto');

    await submit();

    expect(element.querySelector('button[type="submit"]')?.textContent?.trim()).toBe(
      UPLOAD_TEXTS.submitting,
    );
    expect(emitted).toEqual([]);
  });
});
