import { TestBed } from '@angular/core/testing';
import { I18n } from './i18n.service';
import { de } from './de';
import { en } from './en';
import { flattenKeys } from './catalogue';

describe('I18n', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  it('[AC-54] the English and German catalogues contain the same texts', () => {
    expect(flattenKeys(de)).toEqual(flattenKeys(en));
    for (const [key, value] of Object.entries(flatten(de))) {
      expect(value.trim(), key).not.toBe('');
    }
  });

  it('[AC-55] formats dates and numbers for the language', () => {
    const i18n = TestBed.inject(I18n);

    expect(i18n.date('2026-10-11')).toBe('11 Oct 2026');
    expect(i18n.number(1234.5, 1)).toBe('1,234.5');

    i18n.setLanguage('de');
    expect(i18n.date('2026-10-11')).toBe('11.10.2026');
    // The Swiss grouping mark is ' or ’ depending on the browser's locale data (CLDR version); both are correct.
    expect(i18n.number(1234.5, 1)).toMatch(/^1['’]234\.5$/);
  });

  it('[AC-53] translates keys with parameters and remembers the choice', () => {
    const i18n = TestBed.inject(I18n);
    expect(i18n.t('squad.archivedToast', { name: 'Nora' })).toBe('Nora archived.');

    i18n.setLanguage('de');
    expect(i18n.t('squad.archivedToast', { name: 'Nora' })).toBe('Nora archiviert.');

    TestBed.resetTestingModule();
    expect(TestBed.inject(I18n).lang()).toBe('de');
  });
});

function flatten(obj: object, prefix = ''): Record<string, string> {
  return Object.entries(obj).reduce<Record<string, string>>((acc, [k, v]) => {
    const key = prefix ? `${prefix}.${k}` : k;
    return typeof v === 'string' ? { ...acc, [key]: v } : { ...acc, ...flatten(v as object, key) };
  }, {});
}
