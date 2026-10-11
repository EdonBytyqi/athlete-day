/** The Blazor staff input app: port 5090 next to `ng serve`, port 8081 next to the Docker dashboard. */
export function staffBaseUrl(): string {
  return location.port === '4200' ? 'http://localhost:5090' : `${location.protocol}//${location.hostname}:8081`;
}

/** Opens the session form with the athlete already selected. */
export function logSessionUrl(athleteId: number): string {
  return `${staffBaseUrl()}/sessions/new?athlete=${athleteId}`;
}
