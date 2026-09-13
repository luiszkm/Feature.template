import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, shareReplay, tap, throwError } from 'rxjs';
import { API_BASE } from '../api';
import { AuthToken, SessionStore } from '../session/session.store';

/** Query param the login screen turns back into a message. */
export type LogoutReason = 'expired' | 'tenant';

/**
 * Renews the access token. The refresh token itself is never touched here - it rides in the
 * `pt_refresh` cookie. A single in-flight call is shared: `RefreshTokenHandler` rotates and
 * revokes the previous token on every call, so two concurrent refreshes invalidate each other.
 */
@Injectable({ providedIn: 'root' })
export class RefreshCoordinator {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);

  private inFlight: Observable<void> | null = null;

  refresh(): Observable<void> {
    if (this.inFlight) {
      return this.inFlight;
    }

    this.inFlight = this.http.post<AuthToken>(`${API_BASE}/identity/refresh`, {}).pipe(
      tap((token) => this.session.apply(token)),
      map(() => undefined),
      catchError((error: unknown) => {
        this.endSession(reasonFor(error));
        return throwError(() => error);
      }),
      finalize(() => (this.inFlight = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );

    return this.inFlight;
  }

  private endSession(reason: LogoutReason): void {
    this.session.clear();
    void this.router.navigate(['/login'], { queryParams: { reason } });
  }
}

function reasonFor(error: unknown): LogoutReason {
  const status = (error as { status?: number })?.status;
  return status === 409 ? 'tenant' : 'expired';
}
