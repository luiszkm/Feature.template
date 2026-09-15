/** Canonical permission names, mirroring docs/security/RBAC_MATRIX.md. */
export const Permissions = {
  userRead: 'identity.user.read',
  userManage: 'identity.user.manage',
  roleRead: 'authorization.role.read',
  roleManage: 'authorization.role.manage',
  permissionRead: 'authorization.permission.read',
  permissionManage: 'authorization.permission.manage',
  tenantsRead: 'tenants.read',
  tenantsManage: 'tenants.manage',
  agentRead: 'ai.agent.read',
  agentManage: 'ai.agent.manage',
} as const;
