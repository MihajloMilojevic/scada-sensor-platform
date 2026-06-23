import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';


@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly isLoggedIn = signal(!!localStorage.getItem('access_token'));

  constructor(private http: HttpClient, private router: Router) {}

  async login(username: string, password: string): Promise<void> {
    const res: any = await firstValueFrom(
      this.http.post(`/api/auth/login`, { username, password })
    );
    localStorage.setItem('access_token', res.accessToken);
    localStorage.setItem('refresh_token', res.refreshToken);
    this.isLoggedIn.set(true);
  }

  logout(): void {
    localStorage.clear();
    this.isLoggedIn.set(false);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem('access_token');
  }

  getAuthHeaders(): Record<string, string> {
    const t = this.getToken();
    return t ? { Authorization: `Bearer ${t}` } : {};
  }
}
