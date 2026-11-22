// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { THEME_KEY, ThemeService } from './theme.service';

describe('ThemeService', () => {
  const root = document.documentElement;

  beforeEach(() => {
    localStorage.removeItem(THEME_KEY);
    root.removeAttribute('data-theme');
  });

  afterEach(() => {
    localStorage.removeItem(THEME_KEY);
    root.removeAttribute('data-theme');
  });

  it('follows the system theme by default', () => {
    const service = TestBed.inject(ThemeService);

    expect(service.mode()).toBe('system');
    expect(root.hasAttribute('data-theme')).toBe(false);
  });

  it('applies and remembers the chosen theme', () => {
    TestBed.inject(ThemeService).set('dark');

    expect(root.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem(THEME_KEY)).toBe('dark');
  });

  it('starts with the remembered theme', () => {
    localStorage.setItem(THEME_KEY, 'light');

    expect(TestBed.inject(ThemeService).mode()).toBe('light');
    expect(root.getAttribute('data-theme')).toBe('light');
  });

  it('ignores an unknown stored value', () => {
    localStorage.setItem(THEME_KEY, 'rosa');

    expect(TestBed.inject(ThemeService).mode()).toBe('system');
  });

  it('goes from system to light, dark and back to system', () => {
    const service = TestBed.inject(ThemeService);

    service.next();
    expect(service.mode()).toBe('light');
    service.next();
    expect(service.mode()).toBe('dark');
    service.next();
    expect(service.mode()).toBe('system');
    expect(root.hasAttribute('data-theme')).toBe(false);
    expect(localStorage.getItem(THEME_KEY)).toBeNull();
  });
});
