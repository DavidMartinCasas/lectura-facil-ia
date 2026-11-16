// SPDX-License-Identifier: Apache-2.0
import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

/** Guarda en el equipo un fichero que ya ha generado el servidor. No genera ni modifica nada. */
@Injectable({ providedIn: 'root' })
export class FileDownloadService {
  private readonly document = inject(DOCUMENT);

  save(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = this.document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.rel = 'noopener';
    this.document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }
}
