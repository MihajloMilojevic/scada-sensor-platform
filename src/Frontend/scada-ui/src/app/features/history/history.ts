import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AnalyticsApiService } from '../../core/services/analytics-api.service';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page">
      <h2>Sensor History</h2>

      <div class="filters">
        <label>From: <input type="datetime-local" [(ngModel)]="from" /></label>
        <label>To: <input type="datetime-local" [(ngModel)]="to" /></label>
        <button (click)="load()">Load</button>
      </div>

      @if (loading()) {
        <p class="status">Loading...</p>
      } @else if (error()) {
        <p class="error">{{ error() }}</p>
      } @else if (rows().length === 0) {
        <p class="status">No data for selected range.</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Time</th>
              <th>Sensor</th>
              <th>Value</th>
              <th>Alarm Priority</th>
            </tr>
          </thead>
          <tbody>
            @for (r of rows(); track r.time + r.sensorId) {
              <tr [class.alarm]="r.alarmPriority > 0">
                <td>{{ r.time | date:'medium' }}</td>
                <td>{{ r.sensorId }}</td>
                <td>{{ r.value | number:'1.2-2' }}</td>
                <td>{{ r.alarmPriority }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .page { padding: 1.5rem; }
    h2 { margin: 0 0 1rem; color: #e2e8f0; }
    .filters { display: flex; gap: 1rem; align-items: center; margin-bottom: 1.5rem; flex-wrap: wrap; }
    label { color: #94a3b8; display: flex; flex-direction: column; gap: 0.25rem; }
    input { background: #1e293b; border: 1px solid #334155; color: #e2e8f0; padding: 0.4rem 0.6rem; border-radius: 4px; }
    button { background: #3b82f6; color: white; border: none; padding: 0.5rem 1.2rem; border-radius: 4px; cursor: pointer; align-self: flex-end; }
    button:hover { background: #2563eb; }
    .status { color: #94a3b8; }
    .error { color: #f87171; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.6rem 0.8rem; text-align: left; border-bottom: 1px solid #1e293b; color: #e2e8f0; font-size: 0.875rem; }
    th { color: #94a3b8; font-weight: 500; }
    tr.alarm td { color: #fca5a5; }
  `]
})
export class HistoryComponent implements OnInit {
  rows = signal<any[]>([]);
  loading = signal(false);
  error = signal('');

  from = this.defaultFrom();
  to = this.defaultTo();

  constructor(private api: AnalyticsApiService) {}

  ngOnInit() { this.load(); }

  async load() {
    this.loading.set(true);
    this.error.set('');
    try {
      const data = await this.api.getHistory(
        new Date(this.from).toISOString(),
        new Date(this.to).toISOString()
      );
      this.rows.set(data ?? []);
    } catch {
      this.error.set('Failed to load history.');
    } finally {
      this.loading.set(false);
    }
  }

  private defaultFrom() {
    const d = new Date();
    d.setHours(d.getHours() - 1);
    return d.toISOString().slice(0, 16);
  }

  private defaultTo() {
    return new Date().toISOString().slice(0, 16);
  }
}
