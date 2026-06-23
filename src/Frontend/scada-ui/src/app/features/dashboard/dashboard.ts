import { Component, OnInit, OnDestroy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, SensorReading } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="layout">
      <nav class="navbar">
        <span class="brand">⚡ SCADA Platform</span>
        <div class="nav-links">
          <a routerLink="/dashboard" class="active">Dashboard</a>
          <a routerLink="/alarms">Alarms</a>
          <a routerLink="/sensors">Sensors</a>
          <a routerLink="/history">History</a>
          <a routerLink="/reports">Reports</a>
        </div>
        <div class="conn-status" [class.online]="ns.connected()">
          {{ ns.connected() ? '● Live' : '○ Offline' }}
        </div>
        <button class="logout-btn" (click)="logout()">Logout</button>
      </nav>

      <main class="content">
        <h2>Live Sensor Grid</h2>
        <div class="sensor-grid">
          @for (entry of sensors(); track entry.sensorId) {
            <div class="sensor-card" [class]="alarmClass(entry.alarmPriority)">
              <div class="sensor-id">{{ entry.sensorId }}</div>
              <div class="value">{{ entry.value | number:'1.1-1' }}</div>
              <div class="meta">
                <span class="alarm-badge" *ngIf="entry.alarmPriority > 0">P{{ entry.alarmPriority }}</span>
                <span class="quality" [class.bad]="entry.quality === 'BAD'">{{ entry.quality }}</span>
              </div>
              <div class="ts">{{ entry.timestamp | date:'HH:mm:ss' }}</div>
            </div>
          }
          @if (sensors().length === 0) {
            <p class="waiting">Waiting for sensor data…</p>
          }
        </div>

        <h2>Recent Events</h2>
        <div class="events">
          @for (ev of recentEvents(); track ev.receivedAt) {
            <div class="event-row" [class]="ev.type">
              <span class="ev-time">{{ ev.receivedAt | date:'HH:mm:ss' }}</span>
              <span class="ev-type">{{ ev.type.toUpperCase() }}</span>
              <span class="ev-msg">{{ ev.message }}</span>
            </div>
          }
          @if (recentEvents().length === 0) {
            <p class="waiting">No events yet.</p>
          }
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
    .nav-links a.active, .nav-links a:hover { color:#f9fafb; background:#1f2937; }
    .conn-status { margin-left:auto; color:#6b7280; font-size:0.9rem; }
    .conn-status.online { color:#10b981; }
    .logout-btn { padding:0.4rem 0.8rem; background:#374151; color:#f9fafb; border:none; border-radius:4px; cursor:pointer; }
    .content { padding:1.5rem; }
    h2 { color:#d1d5db; margin:0 0 1rem; font-size:1rem; letter-spacing:0.05em; text-transform:uppercase; }
    .sensor-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(180px,1fr)); gap:1rem; margin-bottom:2rem; }
    .sensor-card { padding:1rem; border-radius:8px; background:#1f2937; border:2px solid #374151; transition:border-color 0.3s; }
    .sensor-card.p1 { border-color:#ca8a04; background:#1a1500; }
    .sensor-card.p2 { border-color:#ea580c; background:#1a0a00; }
    .sensor-card.p3 { border-color:#dc2626; background:#1a0000; animation:pulse 1s infinite; }
    @keyframes pulse { 0%,100%{opacity:1} 50%{opacity:.7} }
    .sensor-id { font-size:0.85rem; color:#9ca3af; }
    .value { font-size:2rem; font-weight:700; color:#f9fafb; margin:0.25rem 0; }
    .meta { display:flex; gap:0.5rem; align-items:center; }
    .alarm-badge { background:#dc2626; color:#fff; font-size:0.7rem; padding:0.1rem 0.4rem; border-radius:9px; }
    .quality { font-size:0.75rem; color:#10b981; }
    .quality.bad { color:#ef4444; }
    .ts { font-size:0.7rem; color:#6b7280; margin-top:0.25rem; }
    .waiting { color:#6b7280; }
    .events { display:flex; flex-direction:column; gap:0.4rem; }
    .event-row { display:flex; gap:1rem; align-items:center; padding:0.5rem 0.75rem; border-radius:4px; background:#111827; font-size:0.85rem; }
    .event-row.alarm { border-left:3px solid #dc2626; }
    .event-row.status { border-left:3px solid #3b82f6; }
    .event-row.quality { border-left:3px solid #f59e0b; }
    .ev-time { color:#6b7280; min-width:60px; }
    .ev-type { font-size:0.7rem; padding:0.1rem 0.4rem; border-radius:4px; background:#374151; }
    .ev-msg { color:#d1d5db; }
  `]
})
export class DashboardComponent implements OnInit, OnDestroy {
  constructor(
    public ns: NotificationService,
    private auth: AuthService,
    private router: Router
  ) {}

  sensors = computed(() =>
    Object.values(this.ns.sensorReadings()).sort((a, b) => a.sensorId.localeCompare(b.sensorId))
  );

  recentEvents = computed(() => {
    const alarms  = this.ns.alarms().slice(0, 5).map(e => ({
      type: 'alarm', message: `${e.sensorId} value=${e.value.toFixed(1)} P${e.alarmPriority}`,
      receivedAt: e.receivedAt
    }));
    const status  = this.ns.statusEvents().slice(0, 5).map(e => ({
      type: 'status', message: `${e.sensorId} ${e.previousStatus}→${e.status} (${e.reason})`,
      receivedAt: e.receivedAt
    }));
    const quality = this.ns.qualityEvents().slice(0, 5).map(e => ({
      type: 'quality', message: `${e.sensorId} quality ${e.previousQuality}→${e.newQuality}`,
      receivedAt: e.receivedAt
    }));
    return [...alarms, ...status, ...quality]
      .sort((a, b) => b.receivedAt.getTime() - a.receivedAt.getTime())
      .slice(0, 15);
  });

  alarmClass(p: number): string {
    return p === 1 ? 'p1' : p === 2 ? 'p2' : p === 3 ? 'p3' : '';
  }

  ngOnInit(): void { this.ns.connect(); }
  ngOnDestroy(): void {}

  logout(): void { this.auth.logout(); }
}
