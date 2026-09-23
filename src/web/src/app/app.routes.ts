import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from './core/guards/guards';
import { Permissions } from './core/permissions';
import { Forbidden, NotFound } from './shared/screens';

/** The workflow editor asks before discarding unsaved changes. */
const leaveEditor = (component: { canLeave(): Promise<boolean> }) => component.canLeave();

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
        path: 'users/:userId/edit',
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
      {
        path: 'ai/conversations',
        loadComponent: () =>
          import('./features/ai/conversations-list').then((m) => m.ConversationsList),
      },
      {
        path: 'ai/conversations/:conversationId',
        loadComponent: () => import('./features/ai/chat').then((m) => m.Chat),
      },
      {
        path: 'ai/agents',
        loadComponent: () => import('./features/ai/agents-list').then((m) => m.AgentsList),
        canActivate: [permissionGuard(Permissions.agentRead)],
      },
      {
        path: 'ai/workflows',
        loadComponent: () => import('./features/ai/workflows-list').then((m) => m.WorkflowsList),
        canActivate: [permissionGuard(Permissions.agentRead)],
      },
      {
        path: 'ai/workflows/new',
        loadComponent: () => import('./features/ai/workflow-editor').then((m) => m.WorkflowEditor),
        canActivate: [permissionGuard(Permissions.agentManage)],
        canDeactivate: [leaveEditor],
      },
      {
        path: 'ai/workflows/:workflowId',
        loadComponent: () => import('./features/ai/workflow-editor').then((m) => m.WorkflowEditor),
        canActivate: [permissionGuard(Permissions.agentRead)],
        canDeactivate: [leaveEditor],
      },
      {
        path: 'ai/usage',
        loadComponent: () => import('./features/ai/usage').then((m) => m.Usage),
        canActivate: [permissionGuard(Permissions.agentRead)],
      },
      {
        path: 'ai/agents/new',
        loadComponent: () => import('./features/ai/agent-form').then((m) => m.AgentForm),
        canActivate: [permissionGuard(Permissions.agentManage)],
      },
      {
        path: 'ai/agents/:agentId',
        loadComponent: () => import('./features/ai/agent-form').then((m) => m.AgentForm),
        canActivate: [permissionGuard(Permissions.agentRead)],
      },
    ],
  },
  { path: '**', component: NotFound },
];
