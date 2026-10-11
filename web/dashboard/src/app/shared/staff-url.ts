import { Language } from '../i18n/i18n.service';

/** The Blazor staff input app: port 5090 next to `ng serve`, port 8081 next to the Docker dashboard. */
export function staffBaseUrl(lang: Language): string {
  const base = location.port === '4200' ? 'http://localhost:5090' : `${location.protocol}//${location.hostname}:8081`;
  return `${base}/?lang=${lang}`;
}

/** Opens the session form with the athlete already selected, in the dashboard's language. */
export function logSessionUrl(athleteId: number, lang: Language): string {
  const base = location.port === '4200' ? 'http://localhost:5090' : `${location.protocol}//${location.hostname}:8081`;
  return `${base}/sessions/new?athlete=${athleteId}&lang=${lang}`;
}
