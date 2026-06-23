import { Injectable, OnDestroy, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { AuthService } from './auth.service';

export interface SensorReading {
  sensorId: string;
  value: number;
  timestamp: string;
  alarmPriority: number;
  quality: string;
}

export interface AlarmEvent {
  sensorId: string;
  value: number;
  timestamp: string;
  alarmPriority: number;
  receivedAt: Date;
}

export interface StatusEvent {
  sensorId: string;
  status: string;
  previousStatus: string;
  reason: string;
  timestamp: string;
  receivedAt: Date;
}

export interface QualityEvent {
  sensorId: string;
  previousQuality: string;
  newQuality: string;
  timestamp: string;
  receivedAt: Date;
}

const GW_WS = '/ws/notifications';

@Injectable({ providedIn: 'root' })
export class NotificationService implements OnDestroy {
  readonly sensorReadings = signal<Record<string, SensorReading>>({});
  readonly alarms         = signal<AlarmEvent[]>([]);
  readonly statusEvents   = signal<StatusEvent[]>([]);
  readonly qualityEvents  = signal<QualityEvent[]>([]);
  readonly connected      = signal(false);

  private hub: signalR.HubConnection | null = null;

  constructor(private auth: AuthService) {}

  connect(): void {
    if (this.hub) return;

    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(GW_WS, {
        accessTokenFactory: () => this.auth.getToken() ?? '',
        withCredentials: false
      })
      .withAutomaticReconnect()
      .build();

    this.hub.on('SensorReading', (batch: SensorReading[]) => {
      const current = { ...this.sensorReadings() };
      for (const r of batch) current[r.sensorId] = r;
      this.sensorReadings.set(current);
    });

    this.hub.on('Alarm', (ev: Omit<AlarmEvent, 'receivedAt'>) => {
      this.alarms.update(a => [{ ...ev, receivedAt: new Date() }, ...a].slice(0, 50));
      const current = { ...this.sensorReadings() };
      if (current[ev.sensorId]) current[ev.sensorId] = { ...current[ev.sensorId], alarmPriority: ev.alarmPriority };
      this.sensorReadings.set(current);
    });

    this.hub.on('StatusChanged', (ev: Omit<StatusEvent, 'receivedAt'>) => {
      this.statusEvents.update(a => [{ ...ev, receivedAt: new Date() }, ...a].slice(0, 20));
    });

    this.hub.on('QualityChanged', (ev: Omit<QualityEvent, 'receivedAt'>) => {
      this.qualityEvents.update(a => [{ ...ev, receivedAt: new Date() }, ...a].slice(0, 20));
    });

    this.hub.onreconnected(() => this.connected.set(true));
    this.hub.onclose(() => this.connected.set(false));

    this.hub.start()
      .then(() => this.connected.set(true))
      .catch(err => console.error('SignalR connection error:', err));
  }

  disconnect(): void {
    this.hub?.stop();
    this.hub = null;
    this.connected.set(false);
  }

  ngOnDestroy(): void { this.disconnect(); }
}
