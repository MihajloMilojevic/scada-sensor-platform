import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: 'login',     loadComponent: () => import('./features/login/login').then(m => m.LoginComponent) },
  { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard').then(m => m.DashboardComponent), canActivate: [authGuard] },
  { path: 'alarms',    loadComponent: () => import('./features/alarms/alarms').then(m => m.AlarmsComponent),   canActivate: [authGuard] },
  { path: 'sensors',   loadComponent: () => import('./features/sensors/sensors').then(m => m.SensorsComponent), canActivate: [authGuard] },
  { path: 'history',  loadComponent: () => import('./features/history/history').then(m => m.HistoryComponent),  canActivate: [authGuard] },
  { path: 'reports',  loadComponent: () => import('./features/reports/reports').then(m => m.ReportsComponent),  canActivate: [authGuard] },
  { path: '',          redirectTo: '/dashboard', pathMatch: 'full' },
  { path: '**',        redirectTo: '/dashboard' }
];
