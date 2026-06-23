import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SensorApiService } from '../../core/services/sensor-api.service';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-sensors',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="layout">
      <nav class="navbar">
        <span class="brand">⚡ SCADA Platform</span>
        <div class="nav-links">
          <a routerLink="/dashboard">Dashboard</a>
          <a routerLink="/alarms">Alarms</a>
          <a routerLink="/sensors" class="active">Sensors</a>
        </div>
        <div class="conn-status" [class.online]="ns.connected()">{{ ns.connected() ? '● Live' : '○ Offline' }}</div>
        <button class="logout-btn" (click)="auth.logout()">Logout</button>
      </nav>
      <main class="content">
        <div class="header-row">
          <h2>Sensor Management</h2>
          <button class="refresh-btn" (click)="load()">↻ Refresh</button>
        </div>
        <table>
          <thead>
            <tr>
              <th>Sensor ID</th><th>Status</th><th>Quality</th><th>Last Seen</th><th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (s of sensors(); track s.sensorId) {
              <tr>
                <td>{{ s.sensorId }}</td>
                <td><span class="status-badge" [class]="s.status.toLowerCase()">{{ s.status }}</span></td>
                <td><span class="quality-badge" [class]="s.quality.toLowerCase()">{{ s.quality }}</span></td>
                <td class="ts">{{ s.lastSeenAt ? (s.lastSeenAt | date:'HH:mm:ss') : '—' }}</td>
                <td class="actions">
                  <button (click)="activate(s.sensorId)" [disabled]="s.status==='ACTIVE'">Activate</button>
                  <button (click)="deactivate(s.sensorId)" [disabled]="s.status!=='ACTIVE'">Deactivate</button>
                  <button class="block-btn" (click)="block(s.sensorId)">Block 30s</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
        @if (msg()) {
          <p class="msg">{{ msg() }}</p>
        }
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
    .content { padding:1.5rem; }
    .header-row { display:flex; align-items:center; gap:1rem; margin-bottom:1rem; }
    h2 { margin:0; color:#d1d5db; font-size:1rem; letter-spacing:0.05em; text-transform:uppercase; }
    .refresh-btn { padding:0.3rem 0.6rem; background:#374151; color:#f9fafb; border:none; border-radius:4px; cursor:pointer; }
    table { width:100%; border-collapse:collapse; }
    th { background:#111827; color:#9ca3af; font-size:0.75rem; text-transform:uppercase; padding:0.6rem 1rem; text-align:left; }
    td { padding:0.6rem 1rem; border-bottom:1px solid #1f2937; font-size:0.9rem; }
    .status-badge,.quality-badge { padding:0.15rem 0.5rem; border-radius:9px; font-size:0.75rem; font-weight:600; }
    .active { background:#065f46; color:#6ee7b7; }
    .inactive { background:#7f1d1d; color:#fca5a5; }
    .standby { background:#1e3a5f; color:#93c5fd; }
    .good { background:#065f46; color:#6ee7b7; }
    .bad { background:#7f1d1d; color:#fca5a5; }
    .uncertain { background:#78350f; color:#fcd34d; }
    .ts { color:#6b7280; font-size:0.8rem; }
    .actions { display:flex; gap:0.4rem; }
    .actions button { padding:0.2rem 0.5rem; border:1px solid #374151; background:#1f2937; color:#f9fafb; border-radius:4px; cursor:pointer; font-size:0.8rem; }
    .actions button:disabled { opacity:0.4; cursor:default; }
    .actions button.block-btn { border-color:#dc2626; color:#f87171; }
    .msg { color:#10b981; margin-top:1rem; }
  `]
})
export class SensorsComponent implements OnInit {
  sensors = signal<any[]>([]);
  msg = signal('');

  constructor(
    private api: SensorApiService,
    public auth: AuthService,
    public ns: NotificationService
  ) {}

  ngOnInit(): void { this.load(); }

  async load(): Promise<void> {
    try { this.sensors.set(await this.api.getSensors()); } catch {}
  }

  async activate(id: string): Promise<void> {
    try { await this.api.activate(id); this.msg.set(`${id} activated`); await this.load(); } catch {}
  }

  async deactivate(id: string): Promise<void> {
    try { await this.api.deactivate(id); this.msg.set(`${id} deactivated`); await this.load(); } catch {}
  }

  async block(id: string): Promise<void> {
    try { await this.api.block(id); this.msg.set(`${id} blocked for 30s`); await this.load(); } catch {}
  }
}
