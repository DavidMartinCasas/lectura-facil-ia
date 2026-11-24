// SPDX-License-Identifier: Apache-2.0
import {
  ProgressPosition,
  isReady,
  nextPosition,
  progressPosition,
  progressSteps,
  stepsAt,
} from './document-progress';

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

  it('follows the stage the server reports while processing', () => {
    const at = (stage: Parameters<typeof progressSteps>[1]) =>
      progressSteps('Processing', stage).map((step) => step.state);

    expect(at('Adapting')).toEqual(['done', 'current', 'pending', 'pending', 'pending']);
    expect(at('ReviewingGuidelines')).toEqual(['done', 'done', 'current', 'pending', 'pending']);
    expect(at('Verifying')).toEqual(['done', 'done', 'done', 'current', 'pending']);
  });

  it('says which part of a long document it is adapting or verifying', () => {
    const adapting = stepsAt(progressPosition('Processing', 'Adapting', { number: 3, count: 10 }));
    const verifying = stepsAt(progressPosition('Processing', 'Verifying', { number: 2, count: 5 }));

    expect(adapting[1]).toEqual({ label: 'Adaptando', state: 'current', detail: 'parte 3 de 10' });
    expect(verifying[3]).toEqual({
      label: 'Verificando',
      state: 'current',
      detail: 'parte 2 de 5',
    });
    expect(verifying.filter((step) => step.detail)).toHaveLength(1);
  });

  it('says which part it is adapting again', () => {
    const steps = stepsAt(progressPosition('Processing', 'Retrying', { number: 4, count: 6 }));

    expect(steps[1].detail).toBe('la parte 4 de 6');
  });

  it('moves on to the next part of the same step', () => {
    const shown = progressPosition('Processing', 'Adapting', { number: 1, count: 2 });
    const target = progressPosition('Processing', 'Adapting', { number: 2, count: 2 });

    expect(nextPosition(shown, target)).toEqual(target);
    expect(nextPosition(target, target)).toBeNull();
  });

  it('says it is adapting again when the first adaptation broke severe guidelines', () => {
    const steps = progressSteps('Processing', 'Retrying');

    expect(steps[1]).toEqual({ label: 'Adaptando otra vez', state: 'current' });
  });

  it('marks as failed the step the document was in', () => {
    expect(states('Error')).toEqual(['done', 'failed', 'pending', 'pending', 'pending']);
    expect(progressSteps('Error', 'Verifying').map((step) => step.state)).toEqual([
      'done',
      'done',
      'done',
      'failed',
      'pending',
    ]);
  });
});

describe('nextPosition', () => {
  /** Las posiciones que se ven de una a otra, sin contar la primera. */
  function walk(from: ProgressPosition, to: ProgressPosition): string[][] {
    const seen: string[][] = [];
    let shown = from;
    for (let next = nextPosition(shown, to); next !== null; next = nextPosition(shown, to)) {
      shown = next;
      seen.push(stepsAt(shown).map((step) => step.state));
    }
    return seen;
  }

  it('goes through every step in order, even those the server did very fast', () => {
    expect(walk(progressPosition('Received'), progressPosition('Draft'))).toEqual([
      ['done', 'current', 'pending', 'pending', 'pending'],
      ['done', 'done', 'current', 'pending', 'pending'],
      ['done', 'done', 'done', 'current', 'pending'],
      ['done', 'done', 'done', 'done', 'done'],
    ]);
  });

  it('shows an error or a new adaptation straight away', () => {
    const reviewing = progressPosition('Processing', 'ReviewingGuidelines');

    expect(nextPosition(reviewing, progressPosition('Error', 'ReviewingGuidelines'))?.failed).toBe(
      true,
    );
    expect(nextPosition(reviewing, progressPosition('Processing', 'Retrying'))).toEqual(
      progressPosition('Processing', 'Retrying'),
    );
  });

  it('stops when it gets there', () => {
    const ready = progressPosition('Draft');

    expect(nextPosition(ready, ready)).toBeNull();
    expect(isReady(ready)).toBe(true);
    expect(isReady(progressPosition('Processing'))).toBe(false);
  });
});
