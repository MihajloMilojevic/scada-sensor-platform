import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';


@Injectable({ providedIn: 'root' })
export class SensorApiService {
  constructor(private http: HttpClient, private auth: AuthService) {}

  private headers() {
    return { headers: new HttpHeaders(this.auth.getAuthHeaders()) };
  }

  getSensors(): Promise<any[]> {
    return firstValueFrom(this.http.get<any[]>(`/api/sensors`, this.headers()));
  }

  activate(id: string): Promise<any> {
    return firstValueFrom(this.http.post(`/api/sensors/${id}/activate`, {}, this.headers()));
  }

  deactivate(id: string): Promise<any> {
    return firstValueFrom(this.http.post(`/api/sensors/${id}/deactivate`, {}, this.headers()));
  }

  block(id: string): Promise<any> {
    return firstValueFrom(this.http.post(`/api/sensors/${id}/block`, {}, this.headers()));
  }
}
