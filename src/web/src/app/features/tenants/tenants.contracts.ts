export type TenantIsolationMode = 'Shared' | 'Dedicated';

export interface TenantOutput {
  tenantId: string;
  tenantKey: string;
  displayName: string;
  contactEmail: string | null;
  isActive: boolean;
  isolationMode: TenantIsolationMode | number;
  createdAt: string;
}

export interface CreateTenantRequest {
  tenantKey: string;
  displayName: string;
  contactEmail: string | null;
  isolationMode: TenantIsolationMode | number;
}

export interface UpdateTenantRequest {
  displayName: string;
  contactEmail: string | null;
}
