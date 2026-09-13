import {
  api,
  authenticate,
  click,
  el,
  problem,
  provideRouteStub,
  settle,
  text,
  type,
  waitFor,
} from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { USER_CREATED_MESSAGE, UserForm } from './user-form';

const USERS = '/api/v1/identity/users';
const REGISTER = '/api/v1/identity/register';

const USER = {
  id: 'user-1',
  email: 'ana@example.com',
  firstName: 'Ana',
  lastName: 'Silva',
  createdAt: '2026-01-02T10:00:00Z',
  lastLoginAt: null,
};

async function fillNew(fixture: Awaited<ReturnType<typeof TestBed.createComponent<UserForm>>>) {
  await type(fixture, 'email', 'ana@example.com');
  await type(fixture, 'password', 'Str0ng@Pass');
  await type(fixture, 'firstName', 'Ana');
  await type(fixture, 'lastName', 'Silva');
}

describe('UserForm', () => {
  it('201 navega e mostra o snackbar', async () => {
    server.use(api.post(REGISTER, () => HttpResponse.json(USER, { status: 201 })));
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(UserForm);
    await settle(fixture, 2);
    await fillNew(fixture);
    await click(fixture, 'submit');

    await waitFor(fixture, () => expect(TestBed.inject(Router).url).toBe('/users'));
    expect(document.body.textContent).toContain(USER_CREATED_MESSAGE);
  });

  it('409 marca o campo email', async () => {
    server.use(
      api.post(REGISTER, () =>
        problem(409, {
          title: 'Business rule violation',
          detail: "Email 'ana@example.com' is already registered.",
          status: 409,
        }),
      ),
    );
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(UserForm);
    await settle(fixture, 2);
    await fillNew(fixture);
    await click(fixture, 'submit');

    expect(text(fixture, 'email-error')).toBe("Email 'ana@example.com' is already registered.");
    expect(el<HTMLInputElement>(fixture, 'firstName').value).toBe('Ana');
  });

  it('200 substitui os dados em ecra', async () => {
    server.use(
      api.get(`${USERS}/user-1`, () => HttpResponse.json(USER)),
      api.put(`${USERS}/user-1`, () =>
        HttpResponse.json({ ...USER, firstName: 'Ana Maria', lastName: 'Silva Costa' }),
      ),
    );
    provideRouteStub({ userId: 'user-1' });
    authenticate();

    const fixture = TestBed.createComponent(UserForm);
    await settle(fixture);

    expect(el<HTMLInputElement>(fixture, 'firstName').value).toBe('Ana');

    await type(fixture, 'firstName', 'Ana Maria');
    await click(fixture, 'submit');

    expect(el<HTMLInputElement>(fixture, 'firstName').value).toBe('Ana Maria');
    expect(el<HTMLInputElement>(fixture, 'lastName').value).toBe('Silva Costa');
  });
});
