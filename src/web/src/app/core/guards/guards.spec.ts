import { authToken, waitUntil } from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { SessionStore } from '../session/session.store';

describe('authGuard', () => {
  it('redireciona com redirectTo e volta', async () => {
    const router = TestBed.inject(Router);
    const session = TestBed.inject(SessionStore);

    await router.navigateByUrl('/tenants');
    await waitUntil(() => expect(router.url).toBe('/login?redirectTo=%2Ftenants'));

    session.apply(authToken());
    const redirectTo = new URL(`http://localhost${router.url}`).searchParams.get('redirectTo');
    await router.navigateByUrl(redirectTo ?? '/');
    await waitUntil(() => expect(router.url).toBe('/tenants'));
  });
});
