import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: 'dashboard', title: 'Squad · Athlete Day', loadComponent: () => import('./pages/dashboard.page').then((m) => m.DashboardPage) },
  {
    // The athlete space (AC-66): one parent with the selector, one child route per section.
    path: 'athletes/:id',
    title: 'Athlete · Athlete Day',
    loadComponent: () => import('./pages/athlete-space/athlete-space.page').then((m) => m.AthleteSpacePage),
    children: [
      { path: '', loadComponent: () => import('./pages/athlete-space/overview.section').then((m) => m.OverviewSection) },
      { path: 'performance', loadComponent: () => import('./pages/athlete-space/performance.section').then((m) => m.PerformanceSection) },
      { path: 'sessions', loadComponent: () => import('./pages/athlete-space/sessions.section').then((m) => m.SessionsSection) },
      { path: 'reviews', loadComponent: () => import('./pages/athlete-space/reviews.section').then((m) => m.ReviewsSection) },
    ],
  },
  { path: 'kitchen', title: 'Kitchen · Athlete Day', loadComponent: () => import('./pages/kitchen.page').then((m) => m.KitchenPage) },
  { path: 'imports', title: 'Device import · Athlete Day', loadComponent: () => import('./pages/imports.page').then((m) => m.ImportsPage) },
  { path: 'settings', title: 'Settings · Athlete Day', loadComponent: () => import('./pages/settings.page').then((m) => m.SettingsPage) },
  { path: 'audit', title: 'Audit log · Athlete Day', loadComponent: () => import('./pages/audit.page').then((m) => m.AuditPage) },
  { path: '**', redirectTo: 'dashboard' },
];
