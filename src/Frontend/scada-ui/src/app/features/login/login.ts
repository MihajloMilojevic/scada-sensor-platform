import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-wrap">
      <div class="login-card">
        <h1>SCADA Platform</h1>
        <input [(ngModel)]="username" placeholder="Username" />
        <input [(ngModel)]="password" type="password" placeholder="Password" (keyup.enter)="login()" />
        <button (click)="login()" [disabled]="loading">
          {{ loading ? 'Logging in…' : 'Login' }}
        </button>
        <p class="error" *ngIf="error">{{ error }}</p>
      </div>
    </div>
  `,
  styles: [`
    .login-wrap { display:flex; height:100vh; align-items:center; justify-content:center; background:#0a0e1a; }
    .login-card { background:#111827; padding:2rem; border-radius:8px; min-width:300px; display:flex; flex-direction:column; gap:1rem; }
    h1 { color:#60a5fa; margin:0 0 0.5rem; font-size:1.4rem; }
    input { padding:0.6rem; border:1px solid #374151; border-radius:4px; background:#1f2937; color:#f9fafb; font-size:1rem; }
    button { padding:0.7rem; background:#3b82f6; color:#fff; border:none; border-radius:4px; cursor:pointer; font-size:1rem; }
    button:disabled { opacity:0.6; cursor:default; }
    .error { color:#ef4444; margin:0; }
  `]
})
export class LoginComponent {
  username = 'admin';
  password = 'admin123';
  loading = false;
  error = '';

  constructor(private auth: AuthService, private router: Router) {}

  async login(): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      await this.auth.login(this.username, this.password);
      this.router.navigate(['/dashboard']);
    } catch {
      this.error = 'Invalid credentials';
    } finally {
      this.loading = false;
    }
  }
}
