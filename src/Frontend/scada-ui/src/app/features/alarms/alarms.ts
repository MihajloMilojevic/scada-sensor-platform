import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-alarms',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="layout">
      <nav class="navbar">
        <span class="brand">⚡ SCADA Platform</span>
        <div class="nav-links">
          <a routerLink="/dashboard">Dashboard</a>
          <a routerLink="/alarms" class="active">Alarms</a>
          <a routerLink="/sensors">Sensors</a>
        </div>
        <div class="conn-status" [class.online]="ns.connected()">{{ ns.connected() ? '● Live' : '○ Offline' }}</div>
        <button class="logout-btn" (click)="auth.logout()">Logout</button>
      </nav>
      <main class="content">
        <h2>Alarm Feed</h2>
        <div class="alarm-list">
          @for (ev of ns.alarms(); track ev.receivedAt) {
            <div class="alarm-row p{{ ev.alarmPriority }}">
              <span class="ts">{{ ev.receivedAt | date:'HH:mm:ss.SSS' }}</span>
              <span class="badge">P{{ ev.alarmPriority }}</span>
              <span class="sid">{{ ev.sensorId }}</span>
              <span class="val">{{ ev.value | number:'1.2-2' }}</span>
            </div>
          }
          @if (ns.alarms().length === 0) {
            <p class="empty">No alarms yet. All systems normal.</p>
          }
        </div>

        <h2>Status Changes</h2>
        <div class="event-list">
          @for (ev of ns.statusEvents(); track ev.receivedAt) {
            <div class="event-row">
              <span class="ts">{{ ev.receivedAt | date:'HH:mm:ss' }}</span>
              <span class="sid">{{ ev.sensorId }}</span>
              <span>{{ ev.previousStatus }} → {{ ev.status }}</span>
              <span class="reason">{{ ev.reason }}</span>
            </div>
          }
          @if (ns.statusEvents().length === 0) { <p class="empty">No status changes.</p> }
        </div>

        <h2>Quality Changes</h2>
        <div class="event-list">
          @for (ev of ns.qualityEvents(); track ev.receivedAt) {
            <div class="event-row">
              <span class="ts">{{ ev.receivedAt | date:'HH:mm:ss' }}</span>
              <span class="sid">{{ ev.sensorId }}</span>
              <span>{{ ev.previousQuality }} → <strong [class.bad]="ev.newQuality==='BAD'">{{ ev.newQuality }}</strong></span>
            </div>
          }
          @if (ns.qualityEvents().length === 0) { <p class="empty">No quality changes.</p> }
        </div>
      </main>
    </div>
  `,
  styles: [`
    .layout { display:flex; flex-direction:column; min-height:100vh; background:#0a0e1a; color:#f9fafb; }
    .navbar { display:flex; align-items:center; gap:1.5rem; padding:0.75rem 1.5rem; background:#111827; border-bottom:1px solid #374151; }
    .brand { color:#60a5fa; font-weight:700; font-size:1.1rem; }
    .nav-links { display:flex; gap:1rem; }
    .nav-links a { color:#9ca3af; text-decoration:none; padding:0.25rem 0.5rem; border-radius:4px; }
    .nav-links a.active,.nav-links a:hover { color:#f9fafb; background:#1f2937; }
    .conn-status { margin-left:auto; color:#6b7280; font-size:0.9rem; }
    .conn-status.online { color:#10b981; }
    .logout-btn { padding:0.4rem 0.8rem; background:#374151; color:#f9fafb; border:none; border-radius:4px; cursor:pointer; }
    .content { padding:1.5rem; display:flex; flex-direction:column; gap:1.5rem; }
    h2 { color:#d1d5db; margin:0; font-size:1rem; letter-spacing:0.05em; text-transform:uppercase; }
    .alarm-list,.event-list { display:flex; flex-direction:column; gap:0.3rem; }
    .alarm-row { display:flex; gap:1rem; align-items:center; padding:0.5rem 0.75rem; border-radius:4px; background:#1f2937; font-size:0.9rem; }
    .alarm-row.p1 { border-left:4px solid #ca8a04; }
    .alarm-row.p2 { border-left:4px solid #ea580c; }
    .alarm-row.p3 { border-left:4px solid #dc2626; }
    .badge { background:#dc2626; color:#fff; font-size:0.7rem; padding:0.1rem 0.4rem; border-radius:9px; }
    .ts { color:#6b7280; font-size:0.8rem; min-width:90px; }
    .sid { font-weight:600; min-width:90px; }
    .val { margin-left:auto; color:#60a5fa; }
    .event-row { display:flex; gap:1rem; padding:0.4rem 0.75rem; border-radius:4px; background:#111827; font-size:0.85rem; }
    .reason { color:#9ca3af; }
    .bad { color:#ef4444; }
    .empty { color:#6b7280; }
  `]
})
export class AlarmsComponent {
  constructor(public ns: NotificationService, public auth: AuthService) {}
}
