import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { ApiService } from '../api/api.service';
import { AuthTokens } from '../api/api.models';

const TOKEN_KEY = 'returnload.admin.token';

/**
 * Holds the admin's authentication state. The access token is kept in a signal (and
 * mirrored to localStorage so a refresh survives). The HTTP interceptor reads
 * {@link token} to authorise API calls.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));

  readonly token = this._token.asReadonly();
  readonly isAuthenticated = computed(() => this._token() !== null);

  login(email: string, password: string): Observable<AuthTokens> {
    return this.api
      .post<AuthTokens>('auth/login', { email, password, deviceId: 'admin-web' })
      .pipe(tap((tokens) => this.setToken(tokens.accessToken)));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this._token.set(null);
  }

  private setToken(token: string): void {
    localStorage.setItem(TOKEN_KEY, token);
    this._token.set(token);
  }
}
