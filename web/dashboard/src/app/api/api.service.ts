import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  AthleteDetails,
  AthleteRequest,
  AuditEntry,
  DashboardRow,
  ImportJob,
  KitchenRow,
  LoadTrend,
  Measurement,
  PhysioNote,
  TrainingSession,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  athlete(id: number) {
    return this.http.get<AthleteDetails>(`/api/athletes/${id}`);
  }

  archivedAthletes() {
    return this.http.get<AthleteDetails[]>('/api/athletes/archived');
  }

  addAthlete(request: AthleteRequest) {
    return this.http.post<AthleteDetails>('/api/athletes', request);
  }

  editAthlete(id: number, request: AthleteRequest) {
    return this.http.put<AthleteDetails>(`/api/athletes/${id}`, request);
  }

  /** Archives the athlete (ADR-07): hidden everywhere, history kept, restorable. */
  archiveAthlete(id: number) {
    return this.http.delete<void>(`/api/athletes/${id}`);
  }

  restoreAthlete(id: number) {
    return this.http.post<AthleteDetails>(`/api/athletes/${id}/restore`, null);
  }

  dashboard() {
    return this.http.get<DashboardRow[]>('/api/dashboard');
  }

  loadTrend(athleteId: number, days = 28) {
    return this.http.get<LoadTrend>(`/api/athletes/${athleteId}/load-trend`, { params: { days } });
  }

  sessions(athleteId: number) {
    return this.http.get<TrainingSession[]>(`/api/athletes/${athleteId}/sessions`);
  }

  measurements(athleteId: number) {
    return this.http.get<Measurement[]>(`/api/athletes/${athleteId}/measurements`);
  }

  physioNotes(athleteId: number) {
    return this.http.get<PhysioNote[]>(`/api/athletes/${athleteId}/physio-notes`);
  }

  kitchenToday() {
    return this.http.get<KitchenRow[]>('/api/kitchen/today');
  }

  imports() {
    return this.http.get<ImportJob[]>('/api/imports');
  }

  importJob(id: string) {
    return this.http.get<ImportJob>(`/api/imports/${id}`);
  }

  upload(file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ImportJob>('/api/imports', form);
  }

  audit(take = 100) {
    return this.http.get<AuditEntry[]>('/api/audit', { params: { take } });
  }
}

/** Turns an HTTP error into a sentence for the screen. A 403 is expected behaviour, not a bug. */
export function describeError(error: unknown, role: string): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 403) return `Not available for the ${role} role. That is by design: need-to-know access.`;
    if (error.status === 401) return 'No identity was sent with the request.';
    if (error.status === 0) return 'The API is not reachable. Is it running on http://localhost:5080?';
    const fieldErrors = error.error?.errors as Record<string, string[]> | undefined;
    if (fieldErrors) return Object.values(fieldErrors).flat().join(' ');
    return `The API answered ${error.status} ${error.statusText}.`;
  }
  return 'Something went wrong.';
}
