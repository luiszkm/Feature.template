import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from './core/guards/guards';
import { Permissions } from './core/permissions';
import { Forbidden, NotFound } from './shared/screens';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/identity/login').then((m) => m.Login),
  },
  { path: 'forbidden', component: Forbidden },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'users' },
      {
        path: 'users',
        loadComponent: () => import('./features/identity/users-list').then((m) => m.UsersList),
      },
      {
        path: 'users/new',
        loadComponent: () => import('./features/identity/user-form').then((m) => m.UserForm),
      },
      {
        path: 'users/:userId',
        loadComponent: () => import('./features/identity/user-detail').then((m) => m.UserDetail),
      },
      {
        path: 'users/:userId/roles',
        loadComponent: () => import('./features/authorization/user-roles').then((m) => m.UserRoles),
        canActivate: [permissionGuard(Permissions.roleRead)],
      },
      {
        path: 'roles',
        loadComponent: () => import('./features/authorization/roles-list').then((m) => m.RolesList),
        canActivate: [permissionGuard(Permissions.roleRead)],
      },
      {
        path: 'roles/:roleId',
        loadComponent: () =>
          import('./features/authorization/role-detail').then((m) => m.RoleDetail),
        canActivate: [permissionGuard(Permissions.roleRead)],
      },
      {
        path: 'permissions',
        loadComponent: () =>
          import('./features/authorization/permissions-list').then((m) => m.PermissionsList),
        canActivate: [permissionGuard(Permissions.permissionRead)],
      },
      {
        path: 'tenants',
        loadComponent: () => import('./features/tenants/tenants-list').then((m) => m.TenantsList),
        canActivate: [permissionGuard(Permissions.tenantsRead)],
      },
      {
        path: 'tenants/new',
        loadComponent: () => import('./features/tenants/tenant-form').then((m) => m.TenantForm),
        canActivate: [permissionGuard(Permissions.tenantsManage)],
      },
      {
        path: 'tenants/:id',
        loadComponent: () => import('./features/tenants/tenant-form').then((m) => m.TenantForm),
        canActivate: [permissionGuard(Permissions.tenantsRead)],
      },
      { path: 'ai', loadComponent: () => import('./features/ai/chat').then((m) => m.Chat) },
    ],
  },
  { path: '**', component: NotFound },
];
