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
import { TenantForm } from './tenant-form';

const TENANTS = '/api/v1/tenants';

const TENANT = {
  tenantId: 'tenant-1',
  tenantKey: 'dev',
  displayName: 'Development',
  contactEmail: 'dev@example.com',
  isActive: true,
  isolationMode: 'Shared',
  createdAt: '2026-01-02T10:00:00Z',
};

describe('TenantForm', () => {
  it('201 navega para tenants', async () => {
    server.use(api.post(TENANTS, () => HttpResponse.json(TENANT, { status: 201 })));
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(TenantForm);
    await settle(fixture, 2);
    await type(fixture, 'tenantKey', 'qa');
    await type(fixture, 'displayName', 'Quality');
    await click(fixture, 'submit');

    await waitFor(fixture, () => expect(TestBed.inject(Router).url).toBe('/tenants'));
  });

  it('409 marca o campo chave', async () => {
    server.use(
      api.post(TENANTS, () =>
        problem(409, {
          title: 'Business rule violation',
          detail: "Tenant key 'qa' is already taken.",
          status: 409,
        }),
      ),
    );
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(TenantForm);
    await settle(fixture, 2);
    await type(fixture, 'tenantKey', 'qa');
    await type(fixture, 'displayName', 'Quality');
    await click(fixture, 'submit');

    expect(text(fixture, 'tenantKey-error')).toBe("Tenant key 'qa' is already taken.");
    expect(el<HTMLInputElement>(fixture, 'displayName').value).toBe('Quality');
  });

  it('200 substitui os dados em ecra', async () => {
    server.use(
      api.get(`${TENANTS}/tenant-1`, () => HttpResponse.json(TENANT)),
      api.put(`${TENANTS}/tenant-1`, () =>
        HttpResponse.json({ ...TENANT, displayName: 'Dev Environment' }),
      ),
    );
    provideRouteStub({ id: 'tenant-1' });
    authenticate();

    const fixture = TestBed.createComponent(TenantForm);
    await settle(fixture);
    await type(fixture, 'displayName', 'Dev Environment');
    await click(fixture, 'submit');

    expect(text(fixture, 'field-displayName')).toBe('Dev Environment');
  });

  it('400 marca os campos', async () => {
    server.use(
      api.get(`${TENANTS}/tenant-1`, () => HttpResponse.json(TENANT)),
      api.put(`${TENANTS}/tenant-1`, () =>
        problem(400, {
          title: 'Validation failed',
          status: 400,
          errors: { DisplayName: ["'Display Name' must not be empty."] },
        }),
      ),
    );
    provideRouteStub({ id: 'tenant-1' });
    authenticate();

    const fixture = TestBed.createComponent(TenantForm);
    await settle(fixture);
    await click(fixture, 'submit');

    expect(text(fixture, 'displayName-error')).toBe("'Display Name' must not be empty.");
  });

  it('mostra os sete campos', async () => {
    server.use(api.get(`${TENANTS}/tenant-1`, () => HttpResponse.json(TENANT)));
    provideRouteStub({ id: 'tenant-1' });
    authenticate();

    const fixture = TestBed.createComponent(TenantForm);
    await settle(fixture);

    expect(text(fixture, 'field-tenantId')).toBe('tenant-1');
    expect(text(fixture, 'field-tenantKey')).toBe('dev');
    expect(text(fixture, 'field-displayName')).toBe('Development');
    expect(text(fixture, 'field-contactEmail')).toBe('dev@example.com');
    expect(text(fixture, 'field-isActive')).toBe('Sim');
    expect(text(fixture, 'field-isolationMode')).toBe('Shared');
    expect(text(fixture, 'field-createdAt')).not.toBe('');
  });
});
