import { Injectable, computed, signal } from '@angular/core';

export interface UserAuth {
  id: string;
  email: string;
  firstName: string;
  lastLoginAt: string | null;
  roles: string[];
}

/** Mirrors `AuthTokenResponse`: the refresh token is in the `pt_refresh` cookie, not here. */
export interface AuthToken {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  user: UserAuth;
}

interface StoredAuth {
  tenantKey: string;
  user: UserAuth;
}

export const AUTH_STORAGE_KEY = 'pt.auth';
export const TENANT_STORAGE_KEY = 'pt.tenant';
export const DEFAULT_TENANT_KEY = 'dev';

/** The role the API treats as holding every permission (see docs/security/RBAC_MATRIX.md). */
const ADMIN_ROLE = 'Admin';

@Injectable({ providedIn: 'root' })
export class SessionStore {
  /** No token of any kind reaches storage: the access token lives here, the refresh in a cookie. */
  private readonly accessTokenSignal = signal<string | null>(null);
  private readonly userSignal = signal<UserAuth | null>(null);
  private readonly tenantSignal = signal<string>(DEFAULT_TENANT_KEY);

  readonly accessToken = this.accessTokenSignal.asReadonly();
  readonly user = this.userSignal.asReadonly();
  readonly tenantKey = this.tenantSignal.asReadonly();

  readonly isAuthenticated = computed(() => this.userSignal() !== null);

  readonly permissions = computed<ReadonlySet<string>>(
    () => new Set(readPermissionClaims(this.accessTokenSignal())),
  );

  constructor() {
    this.restore();
  }

  restore(): void {
    const stored = readJson<StoredAuth>(localStorage.getItem(AUTH_STORAGE_KEY));
    if (stored) {
      this.userSignal.set(stored.user);
    }
    this.tenantSignal.set(
      localStorage.getItem(TENANT_STORAGE_KEY) ?? stored?.tenantKey ?? DEFAULT_TENANT_KEY,
    );
  }

  setTenantKey(tenantKey: string): void {
    this.tenantSignal.set(tenantKey);
    localStorage.setItem(TENANT_STORAGE_KEY, tenantKey);
  }

  apply(token: AuthToken): void {
    this.accessTokenSignal.set(token.accessToken);
    this.userSignal.set(token.user);
    localStorage.setItem(
      AUTH_STORAGE_KEY,
      JSON.stringify({
        tenantKey: this.tenantSignal(),
        user: token.user,
      } satisfies StoredAuth),
    );
  }

  clear(): void {
    this.accessTokenSignal.set(null);
    this.userSignal.set(null);
    localStorage.removeItem(AUTH_STORAGE_KEY);
  }

  hasPermission(permission: string): boolean {
    if (this.userSignal()?.roles.includes(ADMIN_ROLE)) {
      return true;
    }
    return this.permissions().has(permission);
  }
}

function readJson<T>(raw: string | null): T | null {
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

/**
 * Reads the `permission` claims out of the access token payload. The signature is never
 * checked here - authorization stays server-side; this only decides what to render.
 */
export function readPermissionClaims(accessToken: string | null): string[] {
  if (!accessToken) {
    return [];
  }

  const payload = accessToken.split('.')[1];
  if (!payload) {
    return [];
  }

  try {
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const claims = JSON.parse(json) as Record<string, unknown>;
    const permission = claims['permission'];
    if (typeof permission === 'string') {
      return [permission];
    }
    if (Array.isArray(permission)) {
      return permission.filter((value): value is string => typeof value === 'string');
    }
    return [];
  } catch {
    return [];
  }
}
