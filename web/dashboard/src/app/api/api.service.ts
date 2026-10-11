import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { TranslationKey } from '../i18n/catalogue';
import { I18n } from '../i18n/i18n.service';
import {
  AthleteDetails,
  AthleteSummary,
  Benchmark,
  Goal,
  Progress,
  ProgressReview,
  ReviewRequest,
  SessionNote,
  AthleteRequest,
  AuditEntry,
  DashboardRow,
  ImportJob,
  KitchenRow,
  LoadTrend,
  Measurement,
  PhysioNote,
  PlannedSessionRequest,
  PlanTemplate,
  Sport,
  TrainingSession,
  WeekPlan,
  WellnessDay,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  athletes() {
    return this.http.get<AthleteSummary[]>('/api/athletes');
  }

  // ---- athlete space (spec v1.7) ----

  progress(athleteId: number, weeks: number) {
    return this.http.get<Progress>(`/api/athletes/${athleteId}/progress`, { params: { weeks } });
  }

  benchmarks(athleteId: number, weeks = 12) {
    return this.http.get<Benchmark[]>(`/api/athletes/${athleteId}/benchmarks`, { params: { weeks } });
  }

  setGoal(athleteId: number, testType: string, goal: { target: number; targetDate: string | null; note: string | null }) {
    return this.http.put<Goal>(`/api/athletes/${athleteId}/goals/${testType}`, goal);
  }

  removeGoal(athleteId: number, testType: string) {
    return this.http.delete<void>(`/api/athletes/${athleteId}/goals/${testType}`);
  }

  sessionNotes(athleteId: number) {
    return this.http.get<SessionNote[]>(`/api/athletes/${athleteId}/session-notes`);
  }

  addSessionNote(sessionId: number, text: string) {
    return this.http.post<SessionNote>(`/api/sessions/${sessionId}/notes`, { text });
  }

  reviews(athleteId: number) {
    return this.http.get<ProgressReview[]>(`/api/athletes/${athleteId}/reviews`);
  }

  addReview(athleteId: number, review: ReviewRequest) {
    return this.http.post<ProgressReview>(`/api/athletes/${athleteId}/reviews`, review);
  }

  // ---- training plans (spec v1.8) ----

  planWeek(athleteId: number, start: string | null) {
    return this.http.get<WeekPlan>(`/api/athletes/${athleteId}/plan/week`, start ? { params: { start } } : {});
  }

  planTemplate(athleteId: number) {
    return this.http.get<PlanTemplate>(`/api/athletes/${athleteId}/plan/template`);
  }

  setPlanTemplate(athleteId: number, days: { dayOfWeek: number; sessions: PlannedSessionRequest[] }[]) {
    return this.http.put<PlanTemplate>(`/api/athletes/${athleteId}/plan/template`, { days });
  }

  changePlanDay(athleteId: number, date: string, change: { reason: string | null; sessions: PlannedSessionRequest[] }) {
    return this.http.put<void>(`/api/athletes/${athleteId}/plan/days/${date}`, change);
  }

  resetPlanDay(athleteId: number, date: string) {
    return this.http.delete<void>(`/api/athletes/${athleteId}/plan/days/${date}`);
  }

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

  /** Without a period the API uses today; with one, the figures are as of its end date (AC-58, AC-59). */
  dashboard(period?: { from: string; to: string }) {
    return this.http.get<DashboardRow[]>('/api/dashboard', period ? { params: period } : {});
  }

  sports() {
    return this.http.get<Sport[]>('/api/sports');
  }

  addSport(name: string) {
    return this.http.post<Sport>('/api/sports', { name });
  }

  updateSport(id: number, change: { name: string; retired: boolean }) {
    return this.http.put<Sport>(`/api/sports/${id}`, change);
  }

  loadTrend(athleteId: number, days = 28) {
    return this.http.get<LoadTrend>(`/api/athletes/${athleteId}/load-trend`, { params: { days } });
  }

  wellness(athleteId: number, days = 14) {
    return this.http.get<WellnessDay[]>(`/api/athletes/${athleteId}/wellness`, { params: { days } });
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
export function describeError(error: unknown, role: string, t: I18n['t']): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 403) return t('errors.forbidden', { role: t(`roles.${role}` as TranslationKey) });
    if (error.status === 401) return t('errors.unauthorized');
    if (error.status === 0) return t('errors.unreachable');
    // Validation messages already come in the request's language (Accept-Language).
    const fieldErrors = error.error?.errors as Record<string, string[]> | undefined;
    if (fieldErrors) return Object.values(fieldErrors).flat().join(' ');
    return t('errors.status', { status: error.status, text: error.statusText });
  }
  return t('errors.generic');
}
