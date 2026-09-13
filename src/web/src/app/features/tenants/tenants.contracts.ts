/** Matches `Api.Shared.TenantIsolationMode` on the wire: STJ has no string enum converter here. */
export enum TenantIsolationMode {
  SharedDb = 0,
  SchemaPerTenant = 1,
  DedicatedDb = 2,
}

export const TENANT_ISOLATION_MODE_LABELS: Record<TenantIsolationMode, string> = {
  [TenantIsolationMode.SharedDb]: 'Partilhado',
  [TenantIsolationMode.SchemaPerTenant]: 'Schema dedicado',
  [TenantIsolationMode.DedicatedDb]: 'Base de dados dedicada',
};

export interface TenantOutput {
  tenantId: string;
  tenantKey: string;
  displayName: string;
  contactEmail: string | null;
  isActive: boolean;
  isolationMode: TenantIsolationMode;
  createdAt: string;
}

export interface CreateTenantRequest {
  tenantKey: string;
  displayName: string;
  contactEmail: string | null;
  isolationMode: TenantIsolationMode;
}

export interface UpdateTenantRequest {
  displayName: string;
  contactEmail: string | null;
}
