import { Service, computed, signal } from '@angular/core';
import { Catalogue, TranslationKey, TranslationParams, interpolate, lookup } from './catalogue';
import { de } from './de';
import { en } from './en';

export type Language = 'en' | 'de';

const STORAGE_KEY = 'athlete-day.lang';
const CATALOGUES: Record<Language, Catalogue> = { en, de };

/** Formats per language (section 4 of the spec): en-GB "11 Oct 2026", de-CH "11.10.2026"; numbers 1,234.5 / 1’234.5. */
const LOCALES: Record<Language, string> = { en: 'en-GB', de: 'de-CH' };
const DATE_FORMAT: Record<Language, Intl.DateTimeFormatOptions> = {
  en: { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' },
  de: { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' },
};

/**
 * English and German at runtime (ADR-09): switching is a signal change, so every template that calls `t()`
 * re-renders at once, without a reload. The choice is remembered in this browser.
 */
@Service()
export class I18n {
  private readonly language = signal<Language>(restore());

  readonly lang = this.language.asReadonly();
  readonly locale = computed(() => LOCALES[this.language()]);

  constructor() {
    // Screen readers pick the pronunciation from <html lang>.
    document.documentElement.lang = this.language();
  }

  setLanguage(lang: Language): void {
    this.language.set(lang);
    document.documentElement.lang = lang;
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Storage can be unavailable (private mode); the switch still works for this visit.
    }
  }

  /** Translates a key; an arrow function so components can expose it directly as `t`. */
  readonly t = (key: TranslationKey, params?: TranslationParams): string =>
    interpolate(lookup(CATALOGUES[this.language()], key) ?? key, params);

  /** A calendar date (`yyyy-MM-dd`), without time-zone shifts. */
  date(isoDate: string): string {
    return new Intl.DateTimeFormat(this.locale(), DATE_FORMAT[this.language()]).format(new Date(`${isoDate.slice(0, 10)}T00:00:00Z`));
  }

  /** A point in time in the viewer's time zone, e.g. for the audit log. */
  dateTime(iso: string, options: Intl.DateTimeFormatOptions = { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' }): string {
    return new Intl.DateTimeFormat(this.locale(), options).format(new Date(iso));
  }

  number(value: number, maxFractionDigits = 0, minFractionDigits = maxFractionDigits): string {
    return new Intl.NumberFormat(this.locale(), {
      minimumFractionDigits: minFractionDigits,
      maximumFractionDigits: maxFractionDigits,
    }).format(value);
  }
}

function restore(): Language {
  // ?lang=de opens the dashboard in German, e.g. from a shared link.
  const fromUrl = new URLSearchParams(location.search).get('lang');
  if (fromUrl === 'de' || fromUrl === 'en') return fromUrl;
  try {
    return localStorage.getItem(STORAGE_KEY) === 'de' ? 'de' : 'en';
  } catch {
    return 'en';
  }
}
