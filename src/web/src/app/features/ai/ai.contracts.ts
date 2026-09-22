export interface AgentOutput {
  agentId: string;
  name: string;
  instructions: string;
  toolNames: string[];
  isActive: boolean;
  isDefault: boolean;
  createdAt: string;
  model: string | null;
}

export interface CreateAgentRequest {
  name: string;
  instructions: string;
  toolNames: string[];
  model: string | null;
}

/** PUT replaces the whole agent: a missing or null `model` resets it to the system default. */
export interface UpdateAgentRequest {
  name: string;
  instructions: string;
  toolNames: string[];
  model: string | null;
}

export interface AgentFileOutput {
  fileId: string;
  name: string;
  content?: string | null;
}

export interface ModelOutput {
  id: string;
  name: string;
  contextLength: number | null;
  inputPricePerToken: number | null;
  outputPricePerToken: number | null;
}

export type ComparisonStatus = 'Succeeded' | 'Failed' | 'TimedOut';

export interface ComparisonAttachmentInput {
  name: string;
  content: string;
}

export interface CompareModelsRequest {
  agentId: string;
  prompt: string;
  models: string[];
  attachments: ComparisonAttachmentInput[];
}

export interface ComparisonResultOutput {
  model: string;
  status: ComparisonStatus;
  reply: string | null;
  inputTokens: number;
  outputTokens: number;
  cost: number | null;
  latencyMs: number;
  iterationsUsed: number;
  errorCode: string | null;
}

export interface ComparisonOutput {
  comparisonId: string;
  agentId: string;
  agentName: string;
  prompt: string;
  attachments: { name: string }[];
  createdAt: string;
  createdByUserId: string | null;
  totalCost: number | null;
  results: ComparisonResultOutput[];
}

export interface ComparisonSummaryOutput {
  comparisonId: string;
  agentId: string;
  agentName: string;
  promptPreview: string;
  models: string[];
  totalCost: number | null;
  createdAt: string;
}

export const KNOWN_AGENT_TOOLS = [
  { name: 'get_users_summary', label: 'Resumo de utilizadores' },
  { name: 'get_tenant_info', label: 'Informação de tenants' },
  { name: 'list_agent_files', label: 'Listar ficheiros' },
  { name: 'read_agent_file', label: 'Ler ficheiro' },
] as const;

export interface ConversationSummary {
  conversationId: string;
  title: string;
  agentId: string;
  lastActivityAt: string;
  itemCount: number;
}

export interface ConversationItemOutput {
  itemId: string;
  role: string;
  content: string;
  sequence: number;
  createdAt: string;
}

export interface ConversationOutput {
  conversationId: string;
  title: string;
  agentId: string;
  createdAt: string;
  lastActivityAt: string;
  items: ConversationItemOutput[];
}
