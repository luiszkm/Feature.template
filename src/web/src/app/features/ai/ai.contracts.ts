export interface AgentOutput {
  agentId: string;
  name: string;
  instructions: string;
  toolNames: string[];
  isActive: boolean;
  isDefault: boolean;
  createdAt: string;
}

export interface CreateAgentRequest {
  name: string;
  instructions: string;
  toolNames: string[];
}

export interface UpdateAgentRequest {
  name: string;
  instructions: string;
  toolNames: string[];
}

export interface AgentFileOutput {
  fileId: string;
  name: string;
  content?: string | null;
}

export const KNOWN_AGENT_TOOLS = [
  { name: 'get_users_summary', label: 'Resumo de utilizadores' },
  { name: 'get_tenant_info', label: 'Informação de tenants' },
  { name: 'list_agent_files', label: 'Listar ficheiros' },
  { name: 'read_agent_file', label: 'Ler ficheiro' },
] as const;
