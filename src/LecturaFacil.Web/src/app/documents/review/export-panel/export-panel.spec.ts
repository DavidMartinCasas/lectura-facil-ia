// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { ExportFormat } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';
import { ExportPanelComponent } from './export-panel';

async function render(canExport: boolean, exporting = false) {
  const fixture = TestBed.createComponent(ExportPanelComponent);
  fixture.componentRef.setInput('canExport', canExport);
  fixture.componentRef.setInput('exporting', exporting);
  fixture.componentRef.setInput(
    'blockedMessage',
    'Para exportar, marca como revisado el dato clave con aviso.',
  );
  const exported: ExportFormat[] = [];
  fixture.componentInstance.exportAdaptation.subscribe((format) => exported.push(format));
  await fixture.whenStable();
  const element = fixture.nativeElement as HTMLElement;
  const button = () => element.querySelector<HTMLButtonElement>('button')!;
  return { fixture, element, exported, button };
}

describe('ExportPanelComponent', () => {
  it('offers the four formats with DOCX selected by default', async () => {
    const { element, exported, button } = await render(true);

    const options = [...element.querySelectorAll('mat-radio-button')].map((option) =>
      option.textContent?.trim(),
    );
    expect(options).toEqual(['DOCX (Word o LibreOffice)', 'PDF', 'Markdown', 'Texto']);
    button().click();

    expect(exported).toEqual(['docx']);
  });

  it('exports in the format the person chooses', async () => {
    const { fixture, element, exported, button } = await render(true);

    element.querySelectorAll<HTMLInputElement>('input[type="radio"]')[2].click();
    await fixture.whenStable();
    button().click();

    expect(exported).toEqual(['md']);
  });

  it('is disabled and explains why while there are pending warnings', async () => {
    const { element, exported, button } = await render(false);

    expect(button().disabled).toBe(true);
    expect(element.textContent).toContain('marca como revisado el dato clave con aviso');
    button().click();
    expect(exported).toEqual([]);
  });

  it('shows the progress and does not export twice', async () => {
    const { exported, button } = await render(true, true);

    expect(button().disabled).toBe(true);
    expect(button().textContent).toContain(REVIEW_TEXTS.exporting);
    button().click();
    expect(exported).toEqual([]);
  });
});
