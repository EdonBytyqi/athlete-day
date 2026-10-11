import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: 'dashboard', title: 'Squad · Athlete Day', loadComponent: () => import('./pages/dashboard.page').then((m) => m.DashboardPage) },
  { path: 'athletes/:id', title: 'Athlete · Athlete Day', loadComponent: () => import('./pages/athlete.page').then((m) => m.AthletePage) },
  { path: 'kitchen', title: 'Kitchen · Athlete Day', loadComponent: () => import('./pages/kitchen.page').then((m) => m.KitchenPage) },
  { path: 'imports', title: 'Device import · Athlete Day', loadComponent: () => import('./pages/imports.page').then((m) => m.ImportsPage) },
  { path: 'settings', title: 'Settings · Athlete Day', loadComponent: () => import('./pages/settings.page').then((m) => m.SettingsPage) },
  { path: 'audit', title: 'Audit log · Athlete Day', loadComponent: () => import('./pages/audit.page').then((m) => m.AuditPage) },
  { path: '**', redirectTo: 'dashboard' },
];
