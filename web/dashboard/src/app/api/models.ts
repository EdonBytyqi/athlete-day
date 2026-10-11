// Mirrors AthleteDay.Contracts (C#). Enums travel as strings.

export type Role = 'Coach' | 'Physio' | 'Nutrition' | 'Kitchen' | 'Athlete' | 'Admin';
export type ReadinessStatus = 'Ok' | 'CheckIn' | 'Missing';
export type TrainingRestriction = 'None' | 'Modified' | 'NoTraining';
export type ImportStatus = 'Queued' | 'Processing' | 'Completed' | 'Failed';
export type Sex = 'Female' | 'Male' | 'NotStated';

export interface WellnessSummary {
  date: string;
  sleepHours: number;
  soreness: number;
  mood: number;
}

/** Spec v1.1 F11: one morning check in the wellness history. */
export interface WellnessDay extends WellnessSummary {
  flags: string[];
}

export interface DashboardRow {
  athleteId: number;
  code: string;
  displayName: string;
  sport: string;
  /** Age reached this year (only the birth year is stored). */
  age: number | null;
  sex: Sex;
  todayLoad: number;
  acuteLoad: number;
  chronicLoad: number;
  acuteChronicRatio: number | null;
  wellness: WellnessSummary | null;
  readiness: { status: ReadinessStatus; reasons: string[] };
  restriction: TrainingRestriction;
  nutrition: { energyKcal: number; carbsG: number } | null;
  range: DashboardRange;
}

/** Figures over the chosen period; everything else in the row is as of its end date. */
export interface DashboardRange {
  from: string;
  to: string;
  days: number;
  totalLoad: number;
  averageDailyLoad: number;
  sessions: number;
  wellnessDays: number;
  averageSleep: number | null;
  averageSoreness: number | null;
  averageMood: number | null;
}

export interface Sport {
  id: number;
  name: string;
  retired: boolean;
}

export interface LoadTrendPoint {
  date: string;
  dailyLoad: number;
  acute7: number;
  chronic28: number;
}

export interface LoadTrend {
  athleteId: number;
  code: string;
  points: LoadTrendPoint[];
}

export interface TrainingSession {
  id: number;
  athleteId: number;
  athleteCode: string;
  date: string;
  type: string;
  durationMinutes: number;
  rpe: number;
  load: number;
  loggedBy: string;
}

export interface Measurement {
  date: string;
  testType: string;
  value: number;
  unit: string;
  importJobId: string;
}

export interface PhysioNote {
  id: number;
  athleteId: number;
  date: string;
  restriction: TrainingRestriction;
  note: string;
  author: string;
}

export interface KitchenRow {
  athleteCode: string;
  displayName: string;
  energyKcal: number;
  carbsG: number;
  portion: string;
  kitchenNote: string | null;
}

export interface ImportRowError {
  rowNumber: number;
  column: string;
  reason: string;
  rawLine: string;
}

export interface ImportJob {
  id: string;
  fileName: string;
  status: ImportStatus;
  uploadedBy: string;
  uploadedAt: string;
  completedAt: string | null;
  totalRows: number;
  importedRows: number;
  rejectedRows: number;
  failureReason: string | null;
  errors: ImportRowError[];
}

export interface AuditEntry {
  at: string;
  user: string;
  role: string;
  action: string;
  resource: string;
  athleteId: number | null;
}

export interface AthleteDetails {
  id: number;
  code: string;
  displayName: string;
  sportId: number;
  sport: string;
  birthYear: number | null;
  sex: Sex;
  bodyMassKg: number;
  archivedAt: string | null;
}

export interface AthleteRequest {
  displayName: string;
  sportId: number;
  birthYear: number | null;
  sex: Sex;
  bodyMassKg: number;
}
