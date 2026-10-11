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

// ---- Athlete space (spec v1.7) ----

export type Trend = 'Up' | 'Down' | 'Stable';
export type GoalStatus = 'Open' | 'Reached' | 'Behind';
export type TrendMetric = 'load' | 'sessions' | 'sleep' | 'soreness' | 'mood';

export interface WeeklyProgress {
  weekStart: string;
  totalLoad: number;
  sessions: number;
  averageSleep: number | null;
  averageSoreness: number | null;
  averageMood: number | null;
}

export interface TrendItem {
  metric: TrendMetric;
  recent: number | null;
  previous: number | null;
  trend: Trend;
}

export interface Progress {
  athleteId: number;
  weeks: WeeklyProgress[];
  trends: TrendItem[];
}

export interface TestResult {
  date: string;
  value: number;
}

export interface Goal {
  testType: string;
  target: number;
  targetDate: string | null;
  note: string | null;
  setBy: string;
}

export interface Benchmark {
  testType: string;
  unit: string;
  higherIsBetter: boolean;
  latest: TestResult;
  best: TestResult;
  firstInPeriod: TestResult | null;
  changeInPeriod: number | null;
  squadAverage: number | null;
  goal: Goal | null;
  gapToGoal: number | null;
  status: GoalStatus | null;
  history: TestResult[];
}

export interface SessionNote {
  id: number;
  sessionId: number;
  athleteId: number;
  sessionDate: string;
  sessionType: string;
  sessionLoad: number;
  text: string;
  author: string;
  createdAt: string;
}

export interface ReviewSnapshot {
  weeks: WeeklyProgress[];
  totalLoad: number;
  sessions: number;
  averageSleep: number | null;
  averageSoreness: number | null;
  averageMood: number | null;
  benchmarks: Benchmark[];
}

export interface ProgressReview {
  id: number;
  athleteId: number;
  from: string;
  to: string;
  observations: string | null;
  achievements: string | null;
  challenges: string | null;
  recommendations: string | null;
  author: string;
  createdAt: string;
  snapshot: ReviewSnapshot;
}

export interface ReviewRequest {
  weeks: number;
  observations: string | null;
  achievements: string | null;
  challenges: string | null;
  recommendations: string | null;
}

export interface AthleteSummary {
  id: number;
  code: string;
  displayName: string;
  sport: string;
}

// ---- Training plans (spec v1.8) ----

export type PlanDaySource = 'Standard' | 'Changed';

export interface PlannedSession {
  type: string;
  durationMinutes: number;
  rpe: number;
  note: string | null;
  plannedLoad: number;
}

export interface PlanDay {
  date: string;
  dayOfWeek: number;
  source: PlanDaySource;
  reason: string | null;
  sessions: PlannedSession[];
  plannedLoad: number;
  loggedLoad: number;
  loggedSessions: number;
}

export interface WeekPlan {
  athleteId: number;
  weekStart: string;
  days: PlanDay[];
  plannedLoad: number;
  loggedLoad: number;
}

export interface PlanTemplate {
  athleteId: number;
  days: { dayOfWeek: number; sessions: PlannedSession[] }[];
}

export interface PlannedSessionRequest {
  type: string;
  durationMinutes: number;
  rpe: number;
  note: string | null;
}

// ---- Athlete documents (spec v1.9) ----

export type DocumentType = 'Medical' | 'GovernmentId' | 'Certificate' | 'General';

export interface AthleteDocument {
  id: number;
  athleteId: number;
  type: DocumentType;
  title: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  uploadedBy: string;
  uploadedAt: string;
}
