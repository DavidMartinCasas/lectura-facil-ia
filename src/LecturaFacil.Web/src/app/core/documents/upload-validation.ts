// SPDX-License-Identifier: Apache-2.0
import { UPLOAD_TEXTS } from '../ui-texts';

/**
 * Tamaño máximo del PDF que se comprueba en el navegador. Coincide con el valor por defecto de la API
 * (Limits:MaxFileSizeBytes). Es solo una comodidad: la validación que manda es la del servidor.
 */
export const MAX_PDF_BYTES = 5 * 1024 * 1024;

/** Longitud máxima del título (DocumentTitle.MaxLength en la API). */
export const MAX_TITLE_LENGTH = 200;

/** Firma de un PDF ("%PDF-"), igual que comprueba la API. */
const PDF_SIGNATURE = [0x25, 0x50, 0x44, 0x46, 0x2d] as const;

/** Mensaje de error para el usuario si el fichero no se puede enviar; <c>null</c> si parece un PDF válido. */
export async function validatePdf(file: File | null): Promise<string | null> {
  if (file === null) {
    return UPLOAD_TEXTS.fileRequired;
  }
  if (file.size === 0) {
    return UPLOAD_TEXTS.fileEmpty;
  }
  if (file.size > MAX_PDF_BYTES) {
    return UPLOAD_TEXTS.fileTooLarge;
  }
  return (await hasPdfSignature(file)) ? null : UPLOAD_TEXTS.fileNotPdf;
}

export function validateText(text: string): string | null {
  return text.trim().length === 0 ? UPLOAD_TEXTS.textRequired : null;
}

async function hasPdfSignature(file: File): Promise<boolean> {
  const header = new Uint8Array(await readBytes(file.slice(0, PDF_SIGNATURE.length)));
  return (
    header.length === PDF_SIGNATURE.length &&
    PDF_SIGNATURE.every((byte, index) => header[index] === byte)
  );
}

function readBytes(blob: Blob): Promise<ArrayBuffer> {
  if (typeof blob.arrayBuffer === 'function') {
    return blob.arrayBuffer();
  }
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as ArrayBuffer);
    reader.onerror = () => reject(reader.error);
    reader.readAsArrayBuffer(blob);
  });
}
