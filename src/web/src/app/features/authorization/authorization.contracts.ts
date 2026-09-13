export interface RoleOutput {
  id: string;
  name: string;
  description: string;
}

export interface PermissionOutput {
  id: string;
  name: string;
  description: string;
}

export interface RoleWithPermissionsOutput extends RoleOutput {
  permissions: PermissionOutput[];
}

export interface NamedRequest {
  name: string;
  description: string;
}
