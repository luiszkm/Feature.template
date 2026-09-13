import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from '../session/session.store';

export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionStore);
  const router = inject(Router);

  if (session.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { redirectTo: state.url } });
};

export function permissionGuard(permission: string): CanActivateFn {
  return () => {
    const session = inject(SessionStore);
    const router = inject(Router);
    return session.hasPermission(permission) ? true : router.createUrlTree(['/forbidden']);
  };
}
