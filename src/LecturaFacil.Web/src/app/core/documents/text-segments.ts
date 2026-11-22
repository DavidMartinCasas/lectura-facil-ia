// SPDX-License-Identifier: Apache-2.0

/** Rango de un texto con índices UTF-16 (los de JavaScript y .NET), con el fin excluido. */
export interface TextRange {
  readonly start: number;
  readonly end: number;
}

/** Trozo de un texto y los rangos que lo cubren (sus posiciones en la lista de rangos de entrada). */
export interface TextSegment {
  readonly text: string;
  readonly start: number;
  readonly end: number;
  readonly ranges: readonly number[];
}

/**
 * Parte `text` en trozos consecutivos para pintar el marcado con nodos de texto, nunca con HTML. Cada trozo lleva los
 * rangos que lo cubren: con rangos solapados, la zona común es un trozo propio con los dos. Los rangos vacíos o fuera
 * del texto se ignoran, y ningún corte parte un emoji (un par sustituto UTF-16) por la mitad.
 */
export function segmentText(text: string, ranges: readonly TextRange[]): TextSegment[] {
  if (text.length === 0) {
    return [];
  }

  const valid = ranges
    .map((range, index) => ({
      index,
      start: safeBoundary(text, clamp(range.start, text.length)),
      end: safeBoundary(text, clamp(range.end, text.length)),
    }))
    .filter((range) => Number.isInteger(range.start) && Number.isInteger(range.end))
    .filter((range) => range.start < range.end);

  const cuts = [
    ...new Set([0, text.length, ...valid.flatMap((range) => [range.start, range.end])]),
  ];
  cuts.sort((left, right) => left - right);

  const segments: TextSegment[] = [];
  for (let i = 0; i < cuts.length - 1; i++) {
    const start = cuts[i];
    const end = cuts[i + 1];
    const covering = valid
      .filter((range) => range.start <= start && range.end >= end)
      .map((range) => range.index);
    const previous = segments.at(-1);
    if (previous && sameRanges(previous.ranges, covering)) {
      segments[segments.length - 1] = { ...previous, end, text: text.slice(previous.start, end) };
    } else {
      segments.push({ text: text.slice(start, end), start, end, ranges: covering });
    }
  }
  return segments;
}

/**
 * Dónde está `fragment` dentro de `text` (la primera vez), o `null` si no está. Si no aparece tal cual, se busca sin
 * distinguir mayúsculas. Sirve para señalar en el original un dato clave, que el verificador copia del texto.
 */
export function findFragment(text: string, fragment: string): TextRange | null {
  const needle = fragment.trim();
  if (needle.length === 0) {
    return null;
  }
  let start = text.indexOf(needle);
  if (start < 0) {
    const lower = text.toLocaleLowerCase('es');
    const lowerNeedle = needle.toLocaleLowerCase('es');
    // Solo si pasar a minúsculas no cambia las posiciones.
    start = lower.length === text.length ? lower.indexOf(lowerNeedle) : -1;
  }
  return start < 0 ? null : { start, end: start + needle.length };
}

function clamp(position: number, length: number): number {
  return Math.min(Math.max(position, 0), length);
}

/** Si el corte cae entre las dos mitades de un par sustituto, lo mueve al principio del par. */
function safeBoundary(text: string, position: number): number {
  const isLowSurrogate = (code: number) => code >= 0xdc00 && code <= 0xdfff;
  const isHighSurrogate = (code: number) => code >= 0xd800 && code <= 0xdbff;
  return position > 0 &&
    position < text.length &&
    isLowSurrogate(text.charCodeAt(position)) &&
    isHighSurrogate(text.charCodeAt(position - 1))
    ? position - 1
    : position;
}

function sameRanges(left: readonly number[], right: readonly number[]): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}
