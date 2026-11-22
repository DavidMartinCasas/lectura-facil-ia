// SPDX-License-Identifier: Apache-2.0
import { findFragment, segmentText } from './text-segments';

describe('segmentText', () => {
  it('returns the whole text as one plain segment when there are no ranges', () => {
    expect(segmentText('Hola, mundo.', [])).toEqual([
      { text: 'Hola, mundo.', start: 0, end: 12, ranges: [] },
    ]);
  });

  it('returns nothing for an empty text', () => {
    expect(segmentText('', [{ start: 0, end: 3 }])).toEqual([]);
  });

  it('marks a range between two plain segments', () => {
    const segments = segmentText('Se publica en el BOP mañana.', [{ start: 17, end: 20 }]);

    expect(segments.map((segment) => [segment.text, segment.ranges])).toEqual([
      ['Se publica en el ', []],
      ['BOP', [0]],
      [' mañana.', []],
    ]);
  });

  it('keeps contiguous ranges as separate segments', () => {
    const segments = segmentText('abcdef', [
      { start: 0, end: 3 },
      { start: 3, end: 6 },
    ]);

    expect(segments.map((segment) => [segment.text, segment.ranges])).toEqual([
      ['abc', [0]],
      ['def', [1]],
    ]);
  });

  it('splits overlapping ranges so the shared part has both', () => {
    const segments = segmentText('0123456789', [
      { start: 2, end: 6 },
      { start: 4, end: 8 },
    ]);

    expect(segments.map((segment) => [segment.text, segment.ranges])).toEqual([
      ['01', []],
      ['23', [0]],
      ['45', [0, 1]],
      ['67', [1]],
      ['89', []],
    ]);
  });

  it('handles a range nested inside another one', () => {
    const segments = segmentText('0123456789', [
      { start: 0, end: 10 },
      { start: 3, end: 5 },
    ]);

    expect(segments.map((segment) => [segment.text, segment.ranges])).toEqual([
      ['012', [0]],
      ['34', [0, 1]],
      ['56789', [0]],
    ]);
  });

  it('uses UTF-16 positions, as the API does, with accents and emojis', () => {
    const text = 'Créditos 👍 por niños';
    const start = text.indexOf('niños');

    const segments = segmentText(text, [
      { start: 0, end: 8 },
      { start, end: start + 'niños'.length },
    ]);

    expect(segments.map((segment) => segment.text)).toEqual(['Créditos', ' 👍 por ', 'niños']);
    expect(segments.map((segment) => segment.text).join('')).toBe(text);
  });

  it('never splits an emoji in half', () => {
    const text = 'a👍b';

    const segments = segmentText(text, [{ start: 2, end: 4 }]);

    expect(segments.map((segment) => [segment.text, segment.ranges])).toEqual([
      ['a', []],
      ['👍b', [0]],
    ]);
  });

  it('ignores empty, reversed and out-of-range ranges', () => {
    const segments = segmentText('abc', [
      { start: 1, end: 1 },
      { start: 2, end: 1 },
      { start: 5, end: 9 },
      { start: -4, end: 1 },
    ]);

    expect(segments.map((segment) => [segment.text, segment.ranges])).toEqual([
      ['a', [3]],
      ['bc', []],
    ]);
  });

  it('keeps text that looks like HTML as plain text', () => {
    const text = '<script>alert(1)</script>';

    expect(segmentText(text, [{ start: 0, end: 8 }]).map((segment) => segment.text)).toEqual([
      '<script>',
      'alert(1)</script>',
    ]);
  });
});

describe('findFragment', () => {
  const text = 'La ayuda ascenderá al 40 % de la Renta mensual.';

  it('finds the first place where the fragment appears', () => {
    expect(findFragment(text, '40 % de la Renta')).toEqual({ start: 22, end: 38 });
  });

  it('ignores upper and lower case when there is no exact match', () => {
    expect(findFragment(text, 'renta mensual')).toEqual({ start: 33, end: 46 });
  });

  it('returns null when the fragment is not in the text or is blank', () => {
    expect(findFragment(text, '250 euros')).toBeNull();
    expect(findFragment(text, '   ')).toBeNull();
  });
});
