import type { en } from './en';

/** The shape of a catalogue: the English one with every text widened to `string` (ADR-09). */
type Widen<T> = { [K in keyof T]: T[K] extends string ? string : Widen<T[K]> };
export type Catalogue = Widen<typeof en>;

type Paths<T, Prefix extends string = ''> = {
  [K in keyof T & string]: T[K] extends string ? `${Prefix}${K}` : Paths<T[K], `${Prefix}${K}.`>;
}[keyof T & string];

/** Every text key, e.g. `'squad.title'`. A typo is a compile error. */
export type TranslationKey = Paths<Catalogue>;

export type TranslationParams = Record<string, string | number>;

export function lookup(catalogue: Catalogue, key: string): string | undefined {
  const value = key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], catalogue);
  return typeof value === 'string' ? value : undefined;
}

/** Replaces `{name}` placeholders; unknown placeholders stay visible so they get noticed. */
export function interpolate(text: string, params?: TranslationParams): string {
  return params ? text.replace(/\{(\w+)\}/g, (match, name: string) => (name in params ? String(params[name]) : match)) : text;
}

/** All keys of a catalogue, sorted, e.g. for the test that compares languages (AC-54). */
export function flattenKeys(obj: object, prefix = ''): string[] {
  return Object.entries(obj)
    .flatMap(([k, v]) => (typeof v === 'string' ? [`${prefix}${k}`] : flattenKeys(v as object, `${prefix}${k}.`)))
    .sort();
}
