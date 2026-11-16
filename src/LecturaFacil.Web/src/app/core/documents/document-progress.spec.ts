// SPDX-License-Identifier: Apache-2.0
import { progressSteps } from './document-progress';

describe('progressSteps', () => {
  const states = (status: Parameters<typeof progressSteps>[0]) =>
    progressSteps(status).map((step) => step.state);

  it('names the five steps in order', () => {
    expect(progressSteps('Received').map((step) => step.label)).toEqual([
      'Recibido',
      'Adaptando',
      'Revisando pautas',
      'Verificando',
      'Listo',
    ]);
  });

  it('shows the first step in progress while the document waits in the queue', () => {
    expect(states('Received')).toEqual(['current', 'pending', 'pending', 'pending', 'pending']);
  });

  it('shows the adaptation in progress while processing', () => {
    expect(states('Processing')).toEqual(['done', 'current', 'pending', 'pending', 'pending']);
  });

  it('marks the adaptation step as failed on error', () => {
    expect(states('Error')).toEqual(['done', 'failed', 'pending', 'pending', 'pending']);
  });

  it.each(['Draft', 'PendingValidation', 'Validated', 'Published'] as const)(
    'marks every step as done when the document is %s',
    (status) => {
      expect(states(status)).toEqual(['done', 'done', 'done', 'done', 'done']);
    },
  );
});
