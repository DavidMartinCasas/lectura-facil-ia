// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { progressSteps } from '../../core/documents/document-progress';
import { DocumentStatus } from '../../core/documents/document.models';
import { ProgressStepsComponent } from './progress-steps';

describe('ProgressStepsComponent', () => {
  async function render(status: DocumentStatus): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(ProgressStepsComponent);
    fixture.componentRef.setInput('steps', progressSteps(status));
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  const items = (element: HTMLElement) =>
    Array.from(element.querySelectorAll('li .label')).map((item) => item.textContent?.trim());

  it('shows every step with its state in words', async () => {
    const element = await render('Processing');

    expect(items(element)).toEqual([
      'Recibido (hecho)',
      'Adaptando (en curso)',
      'Revisando pautas (pendiente)',
      'Verificando (pendiente)',
      'Listo (pendiente)',
    ]);
    expect(element.querySelector('[aria-current="step"]')?.textContent).toContain('Adaptando');
  });

  it('announces the current step to screen readers', async () => {
    const element = await render('Processing');

    expect(element.querySelector('[role="status"]')?.textContent?.trim()).toBe(
      'Paso actual: Adaptando.',
    );
  });

  it('announces when the document is ready', async () => {
    const element = await render('Draft');

    expect(element.querySelector('[role="status"]')?.textContent?.trim()).toBe(
      'El documento está listo.',
    );
    expect(element.querySelector('[aria-current]')).toBeNull();
  });

  it('marks the failed step and announces the failure', async () => {
    const element = await render('Error');

    expect(items(element)[1]).toBe('Adaptando (con error)');
    expect(element.querySelector('[role="status"]')?.textContent?.trim()).toBe(
      'No se ha podido terminar.',
    );
  });
});
