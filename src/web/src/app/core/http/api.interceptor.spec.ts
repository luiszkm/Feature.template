import { api, authToken, problem, recorder, tokenWith, waitUntil } from '../../../testing';
import { HttpClient, HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpResponse } from 'msw';
import { EMPTY, firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { AUTH_STORAGE_KEY, SessionStore } from '../../core/session/session.store';
import { apiInterceptor } from './api.interceptor';

const USERS = '/api/v1/identity/users';
const REFRESH = '/api/v1/identity/refresh';

/** Runs the interceptor over a request and returns what it handed to the next handler. */
function withTestInterceptor(request: HttpRequest<unknown>): HttpRequest<unknown> {
  let decorated = request;
  TestBed.runInInjectionContext(() => {
    apiInterceptor(request, (next) => {
      decorated = next as HttpRequest<unknown>;
      return EMPTY;
    }).subscribe({ error: () => undefined });
  });
  return decorated;
}

function authenticate(accessToken = tokenWith([])): SessionStore {
  const session = TestBed.inject(SessionStore);
  session.setTenantKey('dev');
  session.apply({ ...authToken(), accessToken });
  return session;
}

describe('apiInterceptor', () => {
  it('adiciona X-Tenant', async () => {
    const requests = recorder();
    server.use(api.get(USERS, () => HttpResponse.json({ data: [] })));

    TestBed.inject(SessionStore).setTenantKey('acme');
    await firstValueFrom(TestBed.inject(HttpClient).get(USERS));

    expect(requests.at(-1)?.headers.get('X-Tenant')).toBe('acme');
  });

  it('envia withCredentials', async () => {
    server.use(api.get(USERS, () => HttpResponse.json({ data: [] })));
    const seen: boolean[] = [];
    TestBed.inject(HttpClient);

    authenticate();
    await firstValueFrom(TestBed.inject(HttpClient).get(USERS, { observe: 'response' })).then(
      (response) => seen.push(response.ok),
    );

    // The request object the interceptor produced is what carries the flag; MSW cannot observe
    // credentials mode, so the interceptor's own output is asserted.
    const request = new HttpRequest('GET', USERS);
    const decorated = withTestInterceptor(request);

    expect(decorated.withCredentials).toBe(true);
    expect(seen).toEqual([true]);
  });

  it('adiciona Authorization Bearer', async () => {
    const requests = recorder();
    server.use(api.get(USERS, () => HttpResponse.json({ data: [] })));

    const session = authenticate();
    await firstValueFrom(TestBed.inject(HttpClient).get(USERS));

    expect(requests.at(-1)?.headers.get('Authorization')).toBe(`Bearer ${session.accessToken()}`);
  });

  it('401 renova e repete', async () => {
    const requests = recorder();
    let served = 0;
    server.use(
      api.get(USERS, () => {
        served += 1;
        return served === 1
          ? problem(401, { title: 'Unauthorized', status: 401 })
          : HttpResponse.json({ totalCount: 1 });
      }),
      api.post(REFRESH, () => HttpResponse.json({ ...authToken(), accessToken: tokenWith([]) })),
    );

    authenticate();
    const body = await firstValueFrom(
      TestBed.inject(HttpClient).get<{ totalCount: number }>(USERS),
    );

    expect(body.totalCount).toBe(1);
    expect(requests.filter((request) => request.url.pathname === REFRESH)).toHaveLength(1);
    expect(requests.filter((request) => request.url.pathname === USERS)).toHaveLength(2);
  });

  it('refresh 401 limpa a sessao', async () => {
    server.use(
      api.get(USERS, () => problem(401, { title: 'Unauthorized', status: 401 })),
      api.post(REFRESH, () => problem(401, { title: 'Unauthorized', status: 401 })),
    );

    const session = authenticate();
    await expect(firstValueFrom(TestBed.inject(HttpClient).get(USERS))).rejects.toBeDefined();

    await waitUntil(() => expect(TestBed.inject(Router).url).toContain('/login'));
    expect(session.accessToken()).toBeNull();
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });

  it('refresh 404 limpa a sessao', async () => {
    server.use(
      api.get(USERS, () => problem(401, { title: 'Unauthorized', status: 401 })),
      api.post(REFRESH, () => problem(404, { title: 'Not found', status: 404 })),
    );

    const session = authenticate();
    await expect(firstValueFrom(TestBed.inject(HttpClient).get(USERS))).rejects.toBeDefined();

    await waitUntil(() => expect(TestBed.inject(Router).url).toContain('/login'));
    expect(session.accessToken()).toBeNull();
  });

  it('refresh 400 limpa a sessao', async () => {
    server.use(
      api.get(USERS, () => problem(401, { title: 'Unauthorized', status: 401 })),
      api.post(REFRESH, () => problem(400, { title: 'Validation failed', status: 400 })),
    );

    const session = authenticate();
    await expect(firstValueFrom(TestBed.inject(HttpClient).get(USERS))).rejects.toBeDefined();

    await waitUntil(() => expect(TestBed.inject(Router).url).toContain('/login'));
    expect(session.accessToken()).toBeNull();
  });

  it('401 em paralelo renova uma vez', async () => {
    const requests = recorder();
    const seen = new Set<string>();
    server.use(
      api.get(USERS, ({ request }) => {
        const marker = new URL(request.url).search;
        if (seen.has(marker)) {
          return HttpResponse.json({ totalCount: 0 });
        }
        seen.add(marker);
        return problem(401, { title: 'Unauthorized', status: 401 });
      }),
      api.post(REFRESH, () => HttpResponse.json({ ...authToken(), accessToken: tokenWith([]) })),
    );

    authenticate();
    const http = TestBed.inject(HttpClient);
    await Promise.all([
      firstValueFrom(http.get(`${USERS}?pageNumber=1`)),
      firstValueFrom(http.get(`${USERS}?pageNumber=2`)),
      firstValueFrom(http.get(`${USERS}?pageNumber=3`)),
    ]);

    expect(requests.filter((request) => request.url.pathname === REFRESH)).toHaveLength(1);
  });

  it('403 abre o ecra forbidden', async () => {
    server.use(api.get(USERS, () => problem(403, { title: 'Forbidden', status: 403 })));

    authenticate();
    await expect(firstValueFrom(TestBed.inject(HttpClient).get(USERS))).rejects.toBeDefined();

    await waitUntil(() => expect(TestBed.inject(Router).url).toBe('/forbidden'));
  });
});
