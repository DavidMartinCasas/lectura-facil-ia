// SPDX-License-Identifier: Apache-2.0
import { UPLOAD_TEXTS } from '../ui-texts';
import { MAX_PDF_BYTES, validatePdf, validateText } from './upload-validation';

describe('validatePdf', () => {
  const pdf = (content: BlobPart[], name = 'bases.pdf') =>
    new File(content, name, { type: 'application/pdf' });

  it('accepts a file that starts with the PDF signature', async () => {
    expect(await validatePdf(pdf(['%PDF-1.7\n…']))).toBeNull();
  });

  it('asks for a file when there is none', async () => {
    expect(await validatePdf(null)).toBe(UPLOAD_TEXTS.fileRequired);
  });

  it('rejects an empty file', async () => {
    expect(await validatePdf(pdf([]))).toBe(UPLOAD_TEXTS.fileEmpty);
  });

  it('rejects a file larger than 5 MB', async () => {
    const big = pdf(['%PDF-', new Uint8Array(MAX_PDF_BYTES)]);

    expect(await validatePdf(big)).toBe(UPLOAD_TEXTS.fileTooLarge);
  });

  it('rejects a file with a .pdf name but without the PDF signature', async () => {
    expect(await validatePdf(pdf(['<html>no soy un PDF</html>']))).toBe(UPLOAD_TEXTS.fileNotPdf);
  });
});

describe('validateText', () => {
  it('asks for a text when it is blank', () => {
    expect(validateText('   \n')).toBe(UPLOAD_TEXTS.textRequired);
  });

  it('accepts any text: the server checks its length', () => {
    expect(validateText('Hola')).toBeNull();
  });
});
