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

  /** Claims decoded from the current access token (null when signed out / malformed). */
  private readonly claims = computed(() => decodeJwtPayload(this._token()));

  /** The signed-in user's email, read from the token — not hard-coded. */
  readonly email = computed(() => (this.claims()?.['email'] as string | undefined) ?? null);

  /** The user's roles. The JWT carries `role` as a string (one) or array (several). */
  readonly roles = computed<readonly string[]>(() => {
    const claim = this.claims()?.['role'];
    if (claim == null) return [];
    return Array.isArray(claim) ? (claim as string[]) : [String(claim)];
  });

  /** A concise display label for the account (highest-privilege role, else email). */
  readonly displayRole = computed(() => this.roles()[0] ?? 'Account');

  /** Internal staff (mirrors backend InternalStaff policy) — may view the ops console. */
  readonly isInternalStaff = computed(() =>
    this.roles().some((r) => ['PlatformAdmin', 'Operations', 'Finance', 'Support'].includes(r)),
  );

  /** May approve/reject documents (mirrors backend CanVerifyDocuments = Operations). */
  readonly canVerifyDocuments = computed(() => this.hasRole('Operations'));

  /** May approve/activate vehicles + act on the fleet (mirrors backend InternalStaff on those endpoints). */
  readonly canManageFleet = computed(() => this.isInternalStaff());

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

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

/** Decodes a JWT's payload segment (base64url) without verifying the signature. */
function decodeJwtPayload(token: string | null): Record<string, unknown> | null {
  if (!token) return null;
  const segment = token.split('.')[1];
  if (!segment) return null;
  try {
    const json = atob(segment.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
}
