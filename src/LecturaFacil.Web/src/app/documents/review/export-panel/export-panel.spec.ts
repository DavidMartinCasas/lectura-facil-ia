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
  const button = () => element.querySelector<HTMLButtonElement>('.export-button')!;
  /** El menú se pinta en una capa fuera del componente. */
  const menuItems = async () => {
    button().click();
    await fixture.whenStable();
    return [...document.querySelectorAll<HTMLButtonElement>('[role="menuitem"]')];
  };
  return { element, exported, button, menuItems };
}

describe('ExportPanelComponent', () => {
  afterEach(() => document.querySelectorAll('.cdk-overlay-container').forEach((c) => c.remove()));

  it('offers the four formats in a menu', async () => {
    const { menuItems } = await render(true);

    const options = (await menuItems()).map((item) => item.textContent?.trim());

    expect(options).toEqual(['DOCX (Word o LibreOffice)', 'PDF', 'Markdown', 'Texto']);
  });

  it('exports in the format the person chooses', async () => {
    const { exported, menuItems } = await render(true);

    (await menuItems())[2].click();

    expect(exported).toEqual(['md']);
  });

  it('is disabled and explains why while there are pending warnings', async () => {
    const { element, button } = await render(false);

    expect(button().disabled).toBe(true);
    const describedBy = button().getAttribute('aria-describedby')!;
    expect(element.querySelector(`#${describedBy}`)?.textContent).toContain(
      'marca como revisado el dato clave con aviso',
    );
  });

  it('shows the progress while exporting', async () => {
    const { button } = await render(true, true);

    expect(button().disabled).toBe(true);
    expect(button().textContent).toContain(REVIEW_TEXTS.exporting);
  });
});
