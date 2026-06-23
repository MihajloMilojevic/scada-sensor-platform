import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class AnalyticsApiService {
  constructor(private http: HttpClient, private auth: AuthService) {}

  private headers() {
    return { headers: new HttpHeaders(this.auth.getAuthHeaders()) };
  }

  getSummary(): Promise<any> {
    return firstValueFrom(this.http.get<any>(`/api/reports/summary`, this.headers()));
  }

  getHistory(from: string, to: string): Promise<any[]> {
    const params = new HttpParams().set('from', from).set('to', to);
    return firstValueFrom(
      this.http.get<any[]>(`/api/reports/history`, { headers: new HttpHeaders(this.auth.getAuthHeaders()), params })
    );
  }

  getConsensus(from: string, to: string): Promise<any[]> {
    const params = new HttpParams().set('from', from).set('to', to);
    return firstValueFrom(
      this.http.get<any[]>(`/api/reports/consensus`, { headers: new HttpHeaders(this.auth.getAuthHeaders()), params })
    );
  }

  getQualityChanges(): Promise<any[]> {
    return firstValueFrom(this.http.get<any[]>(`/api/reports/quality-changes`, this.headers()));
  }

  getSensorReport(): Promise<any[]> {
    return firstValueFrom(this.http.get<any[]>(`/api/reports/sensors`, this.headers()));
  }
}
