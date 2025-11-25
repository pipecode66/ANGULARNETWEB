import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { AuthResponse } from '../models/auth';
import { environment } from '../config';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly tokenKey = 'kanban_token';
  private readonly userKey = 'kanban_user';

  readonly isAuthenticated = signal<boolean>(!!this.token);
  readonly currentUser = signal<string | null>(localStorage.getItem(this.userKey));

  login(username: string, password: string) {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, { username, password })
      .pipe(tap((res) => this.persistSession(res)));
  }

  register(username: string, password: string) {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/register`, { username, password })
      .pipe(tap((res) => this.persistSession(res)));
  }

  logout() {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    this.isAuthenticated.set(false);
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  get token(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  private persistSession(res: AuthResponse) {
    localStorage.setItem(this.tokenKey, res.token);
    localStorage.setItem(this.userKey, res.username);
    this.isAuthenticated.set(true);
    this.currentUser.set(res.username);
  }
}
