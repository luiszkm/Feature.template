import { authToken, maybeEl, settle, tokenWith } from '../../../testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { Permissions } from '../permissions';
import { HasPermissionDirective } from './permission.directive';
import { SessionStore } from './session.store';

@Component({
  imports: [HasPermissionDirective],
  template: `<button *appHasPermission="permission" data-testid="action">Ação</button>`,
})
class Host {
  permission: string = Permissions.userManage;
}

const MANAGE_PERMISSIONS = [
  Permissions.userManage,
  Permissions.roleManage,
  Permissions.permissionManage,
  Permissions.tenantsManage,
];

describe('HasPermissionDirective', () => {
  it.each(MANAGE_PERMISSIONS)('esconde acoes sem permissao: %s', async (permission) => {
    const session = TestBed.inject(SessionStore);
    session.apply({ ...authToken({ roles: [] }), accessToken: tokenWith([]) });

    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.permission = permission;
    await settle(fixture, 2);

    expect(maybeEl(fixture, 'action')).toBeNull();

    session.apply({ ...authToken({ roles: [] }), accessToken: tokenWith([permission]) });
    await settle(fixture, 2);

    expect(maybeEl(fixture, 'action')).not.toBeNull();
  });

  it('esconde acoes sem permissao: a role Admin mostra tudo', async () => {
    TestBed.inject(SessionStore).apply({
      ...authToken({ roles: ['Admin'] }),
      accessToken: tokenWith([]),
    });

    const fixture = TestBed.createComponent(Host);
    await settle(fixture, 2);

    expect(maybeEl(fixture, 'action')).not.toBeNull();
  });
});
