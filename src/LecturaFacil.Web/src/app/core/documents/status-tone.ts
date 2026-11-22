// SPDX-License-Identifier: Apache-2.0
import { DocumentStatus } from './document.models';

/** Tono visual de cada estado. Solo refuerza el texto del estado: nunca se usa el color sin la etiqueta. */
export type StatusTone = 'neutral' | 'pending' | 'ok' | 'danger';

const TONES: Readonly<Record<DocumentStatus, StatusTone>> = {
  Received: 'neutral',
  Processing: 'neutral',
  Error: 'danger',
  Draft: 'pending',
  PendingValidation: 'pending',
  Validated: 'ok',
  Published: 'ok',
};

export function statusTone(status: DocumentStatus): StatusTone {
  return TONES[status];
}
