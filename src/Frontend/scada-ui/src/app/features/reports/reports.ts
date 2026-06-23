import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AnalyticsApiService } from '../../core/services/analytics-api.service';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page">
      <div class="header">
        <h2>Analytics Report</h2>
        <button (click)="load()">Refresh</button>
      </div>

      @if (loading()) {
        <p class="status">Loading summary...</p>
      } @else if (error()) {
        <p class="error">{{ error() }}</p>
      } @else if (summary()) {
        <p class="generated">Generated at {{ summary().generatedAt | date:'medium' }}</p>

        <section>
          <h3>Sensors ({{ summary().sensors?.length ?? 0 }})</h3>
          <table>
            <thead>
              <tr><th>ID</th><th>Name</th><th>Status</th><th>Quality</th></tr>
            </thead>
            <tbody>
              @for (s of summary().sensors; track s.id) {
                <tr>
                  <td>{{ s.id }}</td>
                  <td>{{ s.name }}</td>
                  <td [class]="'status-' + s.status?.toLowerCase()">{{ s.status }}</td>
                  <td [class]="s.quality === 'BAD' ? 'bad' : 'good'">{{ s.quality }}</td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        <section>
          <h3>Recent Consensus Windows ({{ summary().consensus?.length ?? 0 }})</h3>
          <table>
            <thead>
              <tr><th>Window Start</th><th>Window End</th><th>Consensus Value</th><th>Contributing Sensors</th></tr>
            </thead>
            <tbody>
              @for (c of summary().consensus; track c.id) {
                <tr>
                  <td>{{ c.windowStart | date:'medium' }}</td>
                  <td>{{ c.windowEnd | date:'medium' }}</td>
                  <td>{{ c.consensusValue | number:'1.2-2' }}</td>
                  <td>{{ c.contributingSensors }}</td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        <section>
          <h3>Quality Changes ({{ summary().qualityChanges?.length ?? 0 }})</h3>
          @if ((summary().qualityChanges?.length ?? 0) === 0) {
            <p class="status">No quality changes recorded.</p>
          } @else {
            <table>
              <thead>
                <tr><th>Time</th><th>Sensor</th><th>Previous</th><th>New</th><th>Deviation (σ)</th></tr>
              </thead>
              <tbody>
                @for (q of summary().qualityChanges; track q.id) {
                  <tr>
                    <td>{{ q.changedAt | date:'medium' }}</td>
                    <td>{{ q.sensorId }}</td>
                    <td>{{ q.previousQuality }}</td>
                    <td [class]="q.newQuality === 'BAD' ? 'bad' : 'good'">{{ q.newQuality }}</td>
                    <td>{{ q.deviationSigma | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </section>
      }
    </div>
  `,
  styles: [`
    .page { padding: 1.5rem; }
    .header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1rem; }
    h2 { margin: 0; color: #e2e8f0; }
    h3 { color: #94a3b8; font-size: 0.9rem; text-transform: uppercase; letter-spacing: 0.05em; margin: 1.5rem 0 0.75rem; }
    button { background: #3b82f6; color: white; border: none; padding: 0.4rem 1rem; border-radius: 4px; cursor: pointer; }
    button:hover { background: #2563eb; }
    .generated { color: #64748b; font-size: 0.8rem; margin-bottom: 1rem; }
    .status { color: #94a3b8; }
    .error { color: #f87171; }
    section { margin-bottom: 2rem; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.5rem 0.75rem; text-align: left; border-bottom: 1px solid #1e293b; color: #e2e8f0; font-size: 0.85rem; }
    th { color: #64748b; font-weight: 500; }
    .good { color: #4ade80; }
    .bad { color: #f87171; }
    .status-active { color: #4ade80; }
    .status-inactive { color: #94a3b8; }
    .status-blocked { color: #f87171; }
  `]
})
export class ReportsComponent implements OnInit {
  summary = signal<any>(null);
  loading = signal(false);
  error = signal('');

  constructor(private api: AnalyticsApiService) {}

  ngOnInit() { this.load(); }

  async load() {
    this.loading.set(true);
    this.error.set('');
    try {
      const data = await this.api.getSummary();
      this.summary.set(data);
    } catch {
      this.error.set('Failed to load report summary.');
    } finally {
      this.loading.set(false);
    }
  }
}
