import {
  Component,
  OnInit,
  OnDestroy,
  signal,
  ElementRef,
  ViewChild
} from '@angular/core';

import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { AnalyticsApiService } from '../../core/services/analytics-api.service';

import { Chart, registerables } from 'chart.js';
import 'chartjs-adapter-date-fns';

Chart.register(...registerables);

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page">

      <h2>Sensor History</h2>

      <div class="filters">
        <label>
          From:
          <input type="datetime-local" [(ngModel)]="from" />
        </label>

        <label>
          To:
          <input type="datetime-local" [(ngModel)]="to" />
        </label>

        <label>
          Sensor:
          <select [(ngModel)]="selectedSensor" (ngModelChange)="drawChart()">
            <option value="">All sensors</option>

            @for (id of sensorIds(); track id) {
              <option [value]="id">{{ id }}</option>
            }
          </select>
        </label>

        <button (click)="load()">Load</button>
      </div>

      @if (loading()) {
        <p class="status">Loading...</p>
      }

      @if (error()) {
        <p class="error">{{ error() }}</p>
      }

      <div class="chart-wrap">
        <canvas #chartCanvas></canvas>
      </div>

    </div>
  `,
  styles: [`
    .page {
      padding: 1.5rem;
    }

    h2 {
      margin: 0 0 1rem;
      color: #e2e8f0;
    }

    .filters {
      display: flex;
      gap: 1rem;
      align-items: flex-end;
      margin-bottom: 1.5rem;
      flex-wrap: wrap;
    }

    label {
      color: #94a3b8;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      font-size: 0.85rem;
    }

    input, select {
      background: #1e293b;
      border: 1px solid #334155;
      color: #e2e8f0;
      padding: 0.4rem 0.6rem;
      border-radius: 4px;
    }

    button {
      background: #3b82f6;
      color: white;
      border: none;
      padding: 0.5rem 1.2rem;
      border-radius: 4px;
      cursor: pointer;
    }

    button:hover {
      background: #2563eb;
    }

    .status {
      color: #94a3b8;
    }

    .error {
      color: #f87171;
    }

    .chart-wrap {
      background: #1e293b;
      border-radius: 8px;
      padding: 1rem;
      height: 420px;
      position: relative;
    }

    canvas {
      width: 100% !important;
      height: 100% !important;
    }
  `]
})
export class HistoryComponent implements OnInit, OnDestroy {

  @ViewChild('chartCanvas')
  chartCanvas!: ElementRef<HTMLCanvasElement>;

  rows = signal<any[]>([]);
  sensorIds = signal<string[]>([]);
  loading = signal(false);
  error = signal('');

  selectedSensor = '';

  from = this.toLocalStr(new Date(Date.now() - 3600_000));
  to = this.toLocalStr(new Date());

  private chart: Chart | null = null;

  private readonly COLORS = [
    '#60a5fa','#34d399','#f59e0b','#f87171',
    '#a78bfa','#fb923c','#38bdf8','#4ade80'
  ];

  constructor(private api: AnalyticsApiService) {}

  ngOnInit() {
    this.load();
  }

  ngOnDestroy() {
    this.chart?.destroy();
  }

  async load() {
    this.loading.set(true);
    this.error.set('');

    try {
      const from = new Date(this.from).toISOString();
      const to = new Date(this.to).toISOString();

      const data = await this.api.getHistory(from, to) ?? [];

      this.rows.set(data);

      this.sensorIds.set(
        [...new Set(data.map(x => x.sensorId))].sort()
      );

      this.selectedSensor = '';

      setTimeout(() => this.drawChart());

    } catch (e) {
      this.error.set('Failed to load history.');
    } finally {
      this.loading.set(false);
    }
  }

  drawChart() {
    const data = this.selectedSensor
      ? this.rows().filter(x => x.sensorId === this.selectedSensor)
      : this.rows();

    const groups: Record<string, { x: number; y: number }[]> = {};

    for (const r of data) {
      if (!groups[r.sensorId]) groups[r.sensorId] = [];

      groups[r.sensorId].push({
        x: Date.parse(r.timestamp),
        y: Number(r.value)
      });
    }

    const datasets = Object.keys(groups)
      .sort()
      .map((id, i) => ({
        label: id,
        data: groups[id].sort((a, b) => a.x - b.x),
        borderColor: this.COLORS[i % this.COLORS.length],
        pointRadius: 0,
        tension: 0.2
      }));

    if (this.chart) {
      this.chart.destroy();
    }

    this.chart = new Chart(this.chartCanvas.nativeElement, {
      type: 'line',
      data: { datasets },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        parsing: false,
        scales: {
          x: { type: 'time' },
          y: {}
        }
      }
    });
  }

  private toLocalStr(d: Date): string {
    const p = (n: number) => n.toString().padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth()+1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
  }
}
