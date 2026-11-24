// SPDX-License-Identifier: Apache-2.0
// Respuestas de ejemplo de la API para los tests (el ejemplo de la ayuda al alquiler, sin datos personales).
import { Adaptation, DocumentDetails, DocumentStatus } from '../core/documents/document.models';

export const ORIGINAL_TEXT =
  'La ayuda ascenderá al 40 % de la renta mensual, con un máximo de 250 €. Se publica en el BOP.';

export function adaptationFixture(overrides: Partial<Adaptation> = {}): Adaptation {
  return {
    version: 1,
    title: 'Ayudas para el alquiler',
    sections: [
      {
        question: '¿Cuánto dinero dan?',
        sentences: ['Le damos una parte del alquiler.', 'Como mucho, 250 euros al mes.'],
      },
    ],
    glossary: [{ term: 'BOP', definition: 'Boletín Oficial de la Provincia.' }],
    pendingHumanValidation: true,
    promptVersion: 'adapter.v1',
    createdAt: '2026-11-16T10:05:00Z',
    guidelines: {
      summary: '11 de 12 pautas cumplidas',
      compliantCount: 11,
      totalCount: 12,
      items: [
        {
          code: 'FRASES_CORTAS',
          description: 'Frases cortas',
          severity: 'High',
          status: 'Compliant',
          findingCount: 0,
          findings: [],
        },
        {
          code: 'SIN_PORCENTAJES',
          description: 'Sin porcentajes',
          severity: 'Medium',
          status: 'NotCompliant',
          findingCount: 1,
          findings: [
            {
              fragment: '40 %',
              start: 10,
              end: 14,
              severity: 'Medium',
              suggestion: 'Explica la cantidad sin porcentajes.',
            },
          ],
        },
      ],
    },
    verification: {
      status: 'Verified',
      summary: '1 conservado, 0 a revisar, 1 omitido',
      conservedCount: 1,
      reviewCount: 0,
      omittedCount: 1,
      keyFacts: [
        {
          id: 0,
          type: 'Amount',
          originalValue: '40 % de la renta mensual',
          adaptedValue: null,
          status: 'Omitted',
          reason: 'NotInAdaptation',
          reasonMessage: 'No aparece en la adaptación',
          acknowledged: false,
          requiresAcknowledgement: true,
        },
        {
          id: 1,
          type: 'Amount',
          originalValue: 'máximo de 250 €',
          adaptedValue: 'Como mucho, 250 euros al mes',
          status: 'Conserved',
          reason: 'None',
          reasonMessage: '',
          acknowledged: false,
          requiresAcknowledgement: false,
        },
      ],
    },
    provider: 'Servidor propio',
    pendingKeyFactCount: 1,
    canExport: false,
    ...overrides,
  };
}

export function documentFixture(
  status: DocumentStatus,
  overrides: Partial<DocumentDetails> = {},
): DocumentDetails {
  const adapted = status === 'Draft';
  return {
    id: 'doc-1',
    title: 'Bases de la convocatoria',
    status,
    createdAt: '2026-11-16T10:00:00Z',
    updatedAt: '2026-11-16T10:00:00Z',
    error: null,
    adaptation: adapted ? adaptationFixture() : null,
    original: {
      text: ORIGINAL_TEXT,
      findings: [
        {
          guidelineCode: 'SIN_PORCENTAJES',
          guideline: 'Sin porcentajes',
          fragment: '40 %',
          start: 22,
          end: 26,
          severity: 'Medium',
          suggestion: 'Explica la cantidad sin porcentajes.',
        },
        {
          guidelineCode: 'SIGLAS',
          guideline: 'Siglas explicadas',
          fragment: 'BOP',
          start: 89,
          end: 92,
          severity: 'Medium',
          suggestion: 'Explica qué significa la sigla.',
        },
      ],
    },
    readability: adapted
      ? {
          original: { value: 38.4, grade: 'VeryDifficult', gradeLabel: 'Muy difícil' },
          adapted: { value: 71.2, grade: 'QuiteEasy', gradeLabel: 'Bastante fácil' },
          improvement: 32.8,
        }
      : null,
    editable: adapted,
    canRetry: false,
    canSendToValidation: false,
    validation: null,
    validations: [],
    ...overrides,
  };
}
