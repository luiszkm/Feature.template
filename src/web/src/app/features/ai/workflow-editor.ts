import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  InjectionToken,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { CdkDrag, CdkDragEnd, CdkDragMove } from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE, PaginatedList, listParams } from '../../core/api';
import { Permissions } from '../../core/permissions';
import { SessionStore } from '../../core/session/session.store';
import { ConfirmService } from '../../shared/confirm';
import { Problem, fieldError, parseProblem } from '../../shared/problem-details';
import { AiAvailability } from './ai-availability';
import {
  AgentOutput,
  WorkflowEdge,
  WorkflowNode,
  WorkflowRunOutput,
  WorkflowRunStatus,
  WorkflowRunStepOutput,
  WorkflowRunSummaryOutput,
  WorkflowStepStatus,
} from './ai.contracts';
import { WorkflowsClient } from './workflows-list';

/** How often an open run is re-read while it is queued or running. */
export const WORKFLOW_RUN_POLL_MS = new InjectionToken<number>('WORKFLOW_RUN_POLL_MS', {
  providedIn: 'root',
  factory: () => 2000,
});

export const NODE_WIDTH = 180;
export const NODE_HEIGHT = 64;

export const CYCLE_MESSAGE = 'Esta ligação criaria um ciclo';
export const DUPLICATE_EDGE_MESSAGE = 'Ligação já existe';

const STEP_LABELS: Record<WorkflowStepStatus, string> = {
  Pending: 'Pendente',
  Running: 'Em execução',
  Succeeded: 'Concluído',
  Failed: 'Falhou',
  Skipped: 'Ignorado',
};

const RUN_LABELS: Record<WorkflowRunStatus, string> = {
  Queued: 'Na fila',
  Running: 'Em execução',
  Succeeded: 'Concluído',
  Failed: 'Falhou',
};

/** Keys of the nodes reachable from `start` by following edges forward. */
function reachable(edges: readonly WorkflowEdge[], start: string): Set<string> {
  const seen = new Set<string>();
  const stack = [start];
  while (stack.length > 0) {
    const key = stack.pop()!;
    for (const edge of edges) {
      if (edge.from === key && !seen.has(edge.to)) {
        seen.add(edge.to);
        stack.push(edge.to);
      }
    }
  }
  return seen;
}

/** `Triagem de Pedidos` → `triagem-de-pedidos`, then `-2`, `-3` until it is free. */
export function uniqueKey(name: string, taken: readonly string[]): string {
  const base =
    name
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .slice(0, 40) || 'no';
  let key = base;
  for (let n = 2; taken.includes(key); n++) {
    key = `${base}-${n}`;
  }
  return key;
}

function formatCost(cost: number | null): string {
  return cost === null ? '—' : `$${cost}`;
}

@Component({
  selector: 'app-workflow-editor',
  imports: [
    CdkDrag,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    RouterLink,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(keydown)': 'onKey($event)' },
  template: `
    @if (loading()) {
      <mat-progress-bar mode="indeterminate" data-testid="editor-loading" />
    } @else if (notFound()) {
      <section class="screen" data-testid="workflow-not-found">
        <h1>Workflow não encontrado</h1>
        <a mat-stroked-button routerLink="/ai/workflows" data-testid="back-to-workflows"
          >Voltar aos workflows</a
        >
      </section>
    } @else {
      <header>
        @if (canManage) {
          <mat-form-field class="name">
            <mat-label>Nome</mat-label>
            <input
              matInput
              data-testid="workflow-name"
              [value]="name()"
              (input)="rename($any($event.target).value)"
            />
          </mat-form-field>
          <button
            mat-flat-button
            type="button"
            data-testid="save"
            [disabled]="nodes().length === 0 || saving()"
            (click)="save()"
          >
            Guardar
          </button>
        } @else {
          <h1 data-testid="workflow-title">{{ name() }}</h1>
        }
        @if (run()) {
          <button
            mat-stroked-button
            type="button"
            data-testid="back-to-editor"
            (click)="closeRun()"
          >
            Voltar ao editor
          </button>
        }
      </header>

      @for (field of ['name', 'nodes', 'edges']; track field) {
        @if (message(field); as text) {
          <p class="field-error" [attr.data-testid]="'error-' + field">{{ text }}</p>
        }
      }
      @if (saveProblem() && !hasFieldErrors()) {
        <p class="field-error" data-testid="save-error">
          {{ saveProblem()!.detail || saveProblem()!.title }}
        </p>
      }

      <div class="workspace">
        @if (canManage && !run()) {
          <aside class="palette" data-testid="palette">
            <h2>Adicionar agente</h2>
            @for (agent of activeAgents(); track agent.agentId) {
              <button
                mat-stroked-button
                type="button"
                [attr.data-testid]="'palette-' + agent.agentId"
                (click)="addNode(agent)"
              >
                {{ agent.name }}
              </button>
            }
          </aside>
        }

        <div class="canvas" data-testid="canvas" tabindex="0">
          @if (shownNodes().length === 0) {
            <p class="canvas-empty" data-testid="canvas-empty">Adicione um agente para começar</p>
          }
          <svg class="edges" [attr.width]="canvasWidth()" [attr.height]="canvasHeight()">
            <defs>
              <marker
                id="arrow"
                viewBox="0 0 10 10"
                refX="10"
                refY="5"
                markerWidth="8"
                markerHeight="8"
                orient="auto-start-reverse"
              >
                <path d="M 0 0 L 10 5 L 0 10 z" />
              </marker>
            </defs>
            @for (edge of shownEdges(); track edge.from + '>' + edge.to) {
              <path
                class="edge"
                [class.selected]="isSelectedEdge(edge)"
                marker-end="url(#arrow)"
                [attr.d]="edgePath(edge)"
                [attr.data-testid]="'edge-' + edge.from + '-' + edge.to"
                (click)="selectEdge(edge)"
              />
            }
          </svg>

          @for (node of shownNodes(); track node.key) {
            <div
              class="node"
              cdkDrag
              [cdkDragDisabled]="!editable()"
              [class.selected]="selectedKey() === node.key"
              [class]="'node status-' + (stepOf(node.key)?.status ?? 'none')"
              [style.left.px]="node.x"
              [style.top.px]="node.y"
              [attr.data-testid]="'node-' + node.key"
              (cdkDragMoved)="onDragMoved(node.key, $event)"
              (cdkDragEnded)="onDragEnded(node.key, $event)"
              (click)="clickNode(node.key)"
            >
              <strong data-testid="node-agent">{{ agentName(node.agentId) }}</strong>
              <small data-testid="node-key">{{ node.key }}</small>
              @if (stepOf(node.key); as step) {
                <span class="status" [attr.data-testid]="'node-status-' + node.key">
                  @if (step.status === 'Running') {
                    <mat-progress-spinner mode="indeterminate" diameter="14" />
                  }
                  {{ stepLabel(step.status) }}
                </span>
              }
              @if (editable()) {
                <button
                  type="button"
                  class="port"
                  [class.active]="linkFrom() === node.key"
                  [attr.data-testid]="'port-out-' + node.key"
                  aria-label="Ligar a outro nó"
                  (click)="startLink(node.key, $event)"
                ></button>
              }
            </div>
          }
        </div>

        <aside class="panel" data-testid="panel">
          @if (linkFrom()) {
            <p data-testid="link-hint">Clique no nó de destino</p>
          }
          @if (canvasMessage(); as text) {
            <p class="field-error" data-testid="canvas-message">{{ text }}</p>
          }

          @if (run(); as current) {
            <section data-testid="run-view">
              <h2>Execução</h2>
              <p>
                Estado: <strong data-testid="run-status">{{ runLabel(current.status) }}</strong>
              </p>
              @if (isTerminal(current.status)) {
                <p>
                  Custo total: <span data-testid="run-cost">{{ cost(current.totalCost) }}</span>
                </p>
                <p>
                  Duração: <span data-testid="run-duration">{{ duration(current) }}</span>
                </p>
              }
              @if (selectedStep(); as step) {
                <section data-testid="step-detail">
                  <h3>{{ step.nodeKey }}</h3>
                  @if (step.errorCode) {
                    <p class="field-error" data-testid="step-error">{{ step.errorCode }}</p>
                  }
                  @if (step.output !== null) {
                    <pre data-testid="step-output">{{ step.output }}</pre>
                  }
                  <p data-testid="step-tokens">
                    Tokens: {{ step.inputTokens }} in / {{ step.outputTokens }} out
                  </p>
                  <p data-testid="step-cost">Custo: {{ cost(step.cost) }}</p>
                  <p data-testid="step-latency">Latência: {{ step.latencyMs }} ms</p>
                </section>
              }
            </section>
          } @else {
            @if (selectedNode(); as node) {
              <section data-testid="node-panel">
                <h2>{{ agentName(node.agentId) }}</h2>
                <small>{{ node.key }}</small>
                @if (editable()) {
                  <mat-form-field>
                    <mat-label>Instrução</mat-label>
                    <textarea
                      matInput
                      rows="4"
                      data-testid="node-instruction"
                      [value]="node.instruction ?? ''"
                      (input)="setInstruction(node.key, $any($event.target).value)"
                    ></textarea>
                  </mat-form-field>
                  <button
                    mat-button
                    type="button"
                    data-testid="remove-node"
                    (click)="removeNode(node.key)"
                  >
                    Remover nó
                  </button>
                } @else if (node.instruction) {
                  <p data-testid="node-instruction-text">{{ node.instruction }}</p>
                }
              </section>
            }
            @if (selectedEdge(); as edge) {
              <section data-testid="edge-panel">
                <p>{{ edge.from }} → {{ edge.to }}</p>
                @if (editable()) {
                  <button
                    mat-button
                    type="button"
                    data-testid="remove-edge"
                    (click)="removeEdge(edge)"
                  >
                    Remover ligação
                  </button>
                }
              </section>
            }
          }
        </aside>
      </div>

      @if (workflowId()) {
        @if (canManage) {
          <section class="run-form">
            <mat-form-field>
              <mat-label>Input</mat-label>
              <textarea
                matInput
                rows="3"
                data-testid="run-input"
                [value]="runInput()"
                (input)="runInput.set($any($event.target).value)"
              ></textarea>
            </mat-form-field>
            <button
              mat-flat-button
              type="button"
              data-testid="run-start"
              [disabled]="dirty() || !runInput().trim() || starting()"
              (click)="startRun()"
            >
              Executar
            </button>
            @if (dirty()) {
              <p data-testid="run-hint">Guarde antes de executar</p>
            }
            @if (runProblem(); as problem) {
              <p class="field-error" data-testid="run-error">
                {{ problem.detail || problem.title }}
              </p>
            }
          </section>
        }

        <section data-testid="runs">
          <h2>Execuções</h2>
          @if (runs().length === 0) {
            <p data-testid="runs-empty">Nenhuma execução ainda</p>
          } @else {
            <table data-testid="runs-table">
              <thead>
                <tr>
                  <th>Estado</th>
                  <th>Input</th>
                  <th>Custo</th>
                  <th>Início</th>
                </tr>
              </thead>
              <tbody>
                @for (summary of runs(); track summary.runId) {
                  <tr
                    [attr.data-testid]="'run-row-' + summary.runId"
                    (click)="openRun(summary.runId)"
                  >
                    <td>{{ runLabel(summary.status) }}</td>
                    <td>{{ summary.inputPreview }}</td>
                    <td>{{ cost(summary.totalCost) }}</td>
                    <td>{{ summary.createdAt | date: 'short' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </section>
      }
    }
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      gap: 1rem;
    }
    .name {
      flex: 1;
      max-width: 28rem;
    }
    .workspace {
      display: grid;
      grid-template-columns: auto 1fr 18rem;
      gap: 1rem;
      align-items: start;
    }
    .palette,
    .panel {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .canvas {
      position: relative;
      min-height: 480px;
      overflow: auto;
      border: 1px solid var(--mat-sys-outline-variant, #ccc);
      border-radius: 8px;
      background-image: radial-gradient(var(--mat-sys-outline-variant, #ddd) 1px, transparent 1px);
      background-size: 16px 16px;
    }
    .canvas-empty {
      position: absolute;
      inset: 0;
      display: grid;
      place-items: center;
      margin: 0;
      color: var(--mat-sys-on-surface-variant, #666);
    }
    .edges {
      position: absolute;
      inset: 0;
      overflow: visible;
    }
    .edge {
      fill: none;
      stroke: var(--mat-sys-outline, #888);
      stroke-width: 2;
      cursor: pointer;
    }
    .edge.selected {
      stroke: var(--mat-sys-primary, #3f51b5);
      stroke-width: 3;
    }
    marker path {
      fill: var(--mat-sys-outline, #888);
    }
    .node {
      position: absolute;
      width: ${NODE_WIDTH}px;
      height: ${NODE_HEIGHT}px;
      box-sizing: border-box;
      padding: 0.5rem 1.25rem 0.5rem 0.75rem;
      display: flex;
      flex-direction: column;
      justify-content: center;
      background: var(--mat-sys-surface-container, #fff);
      border: 2px solid var(--mat-sys-outline-variant, #ccc);
      border-radius: 8px;
      cursor: pointer;
      user-select: none;
    }
    .node.selected {
      border-color: var(--mat-sys-primary, #3f51b5);
    }
    .node.status-Running {
      border-color: var(--mat-sys-tertiary, #7d5260);
    }
    .node.status-Succeeded {
      border-color: #2e7d32;
    }
    .node.status-Failed {
      border-color: var(--mat-sys-error, #b3261e);
    }
    .node.status-Skipped {
      opacity: 0.6;
      border-style: dashed;
    }
    .node .status {
      display: flex;
      align-items: center;
      gap: 0.25rem;
      font-size: 0.75rem;
    }
    .port {
      position: absolute;
      right: -8px;
      top: calc(50% - 8px);
      width: 16px;
      height: 16px;
      border-radius: 50%;
      border: 2px solid var(--mat-sys-primary, #3f51b5);
      background: var(--mat-sys-surface, #fff);
      cursor: crosshair;
      padding: 0;
    }
    .port.active {
      background: var(--mat-sys-primary, #3f51b5);
    }
    .field-error {
      color: var(--mat-sys-error, #b3261e);
      font-size: 0.75rem;
    }
    .run-form {
      display: flex;
      align-items: flex-start;
      gap: 1rem;
      margin-top: 1rem;
    }
    .run-form mat-form-field {
      flex: 1;
    }
    table {
      width: 100%;
      border-collapse: collapse;
    }
    tbody tr {
      cursor: pointer;
    }
    td,
    th {
      text-align: left;
      padding: 0.25rem 0.5rem;
    }
    pre {
      white-space: pre-wrap;
    }
    .screen {
      padding: 3rem;
      text-align: center;
    }
  `,
})
export class WorkflowEditor {
  private readonly client = inject(WorkflowsClient);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmService);
  private readonly ai = inject(AiAvailability);
  private readonly pollMs = inject(WORKFLOW_RUN_POLL_MS);

  readonly canManage = inject(SessionStore).hasPermission(Permissions.agentManage);
  readonly workflowId = signal(inject(ActivatedRoute).snapshot.paramMap.get('workflowId'));

  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly agents = signal<AgentOutput[]>([]);
  readonly name = signal('Novo workflow');
  readonly description = signal<string | null>(null);
  readonly nodes = signal<WorkflowNode[]>([]);
  readonly edges = signal<WorkflowEdge[]>([]);
  readonly dirty = signal(false);
  readonly saving = signal(false);
  readonly saveProblem = signal<Problem | null>(null);

  readonly selectedKey = signal<string | null>(null);
  readonly selectedEdgeId = signal<string | null>(null);
  readonly linkFrom = signal<string | null>(null);
  readonly canvasMessage = signal<string | null>(null);
  readonly drag = signal<{ key: string; dx: number; dy: number } | null>(null);

  readonly runs = signal<WorkflowRunSummaryOutput[]>([]);
  readonly run = signal<WorkflowRunOutput | null>(null);
  readonly runInput = signal('');
  readonly starting = signal(false);
  readonly runProblem = signal<Problem | null>(null);

  private destroyed = false;
  private pollGeneration = 0;

  readonly activeAgents = computed(() => this.agents().filter((agent) => agent.isActive));
  readonly editable = computed(() => this.canManage && this.run() === null);
  /** A run is drawn from its own copy of the graph, never from the workflow as it is now. */
  readonly shownNodes = computed(() => this.run()?.nodes ?? this.nodes());
  readonly shownEdges = computed(() => this.run()?.edges ?? this.edges());
  readonly selectedNode = computed(
    () => this.nodes().find((node) => node.key === this.selectedKey()) ?? null,
  );
  readonly selectedEdge = computed(
    () => this.edges().find((edge) => `${edge.from}>${edge.to}` === this.selectedEdgeId()) ?? null,
  );
  readonly selectedStep = computed(() => this.stepOf(this.selectedKey() ?? ''));
  readonly canvasWidth = computed(() =>
    Math.max(800, ...this.shownNodes().map((n) => n.x + NODE_WIDTH + 40)),
  );
  readonly canvasHeight = computed(() =>
    Math.max(480, ...this.shownNodes().map((n) => n.y + NODE_HEIGHT + 40)),
  );
  readonly hasFieldErrors = computed(() =>
    ['name', 'nodes', 'edges'].some((field) => this.message(field) !== null),
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
    });
    void this.load();
  }

  private async load(): Promise<void> {
    const workflowId = this.workflowId();
    try {
      const [agents, workflow] = await Promise.all([
        firstValueFrom(
          this.http.get<PaginatedList<AgentOutput>>(`${API_BASE}/ai/agents`, {
            params: listParams({ pageNumber: 1, pageSize: 100 }),
          }),
        ),
        workflowId ? firstValueFrom(this.client.get(workflowId)) : Promise.resolve(null),
      ]);
      this.agents.set(agents.data);
      if (workflow) {
        this.name.set(workflow.name);
        this.description.set(workflow.description);
        this.nodes.set(workflow.nodes);
        this.edges.set(workflow.edges);
        await this.loadRuns();
      }
    } catch (error: unknown) {
      const problem = parseProblem(error);
      this.ai.learnFrom(problem);
      if (problem.status === 404) {
        this.notFound.set(true);
      } else {
        this.saveProblem.set(problem);
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async loadRuns(): Promise<void> {
    const workflowId = this.workflowId();
    if (!workflowId) {
      return;
    }
    const page = await firstValueFrom(this.client.listRuns(workflowId));
    this.runs.set(page.data);
  }

  // ---- editing ---------------------------------------------------------------------------

  rename(value: string): void {
    this.name.set(value);
    this.touch();
  }

  addNode(agent: AgentOutput): void {
    const nodes = this.nodes();
    const index = nodes.length;
    const key = uniqueKey(
      agent.name,
      nodes.map((node) => node.key),
    );
    this.nodes.set([
      ...nodes,
      {
        key,
        agentId: agent.agentId,
        instruction: null,
        x: 40 + (index % 4) * 220,
        y: 40 + Math.floor(index / 4) * 120,
      },
    ]);
    this.selectedKey.set(key);
    this.touch();
  }

  setInstruction(key: string, instruction: string): void {
    this.nodes.update((nodes) =>
      nodes.map((node) =>
        node.key === key ? { ...node, instruction: instruction || null } : node,
      ),
    );
    this.touch();
  }

  removeNode(key: string): void {
    this.nodes.update((nodes) => nodes.filter((node) => node.key !== key));
    this.edges.update((edges) => edges.filter((edge) => edge.from !== key && edge.to !== key));
    this.selectedKey.set(null);
    this.touch();
  }

  startLink(key: string, event: Event): void {
    event.stopPropagation();
    this.canvasMessage.set(null);
    this.linkFrom.set(this.linkFrom() === key ? null : key);
  }

  clickNode(key: string): void {
    const from = this.linkFrom();
    if (from && this.editable()) {
      this.linkFrom.set(null);
      this.connect(from, key);
      return;
    }
    this.selectedEdgeId.set(null);
    this.selectedKey.set(key);
  }

  private connect(from: string, to: string): void {
    const edges = this.edges();
    if (edges.some((edge) => edge.from === from && edge.to === to)) {
      this.canvasMessage.set(DUPLICATE_EDGE_MESSAGE);
      return;
    }
    if (from === to || reachable(edges, to).has(from)) {
      this.canvasMessage.set(CYCLE_MESSAGE);
      return;
    }
    this.canvasMessage.set(null);
    this.edges.set([...edges, { from, to }]);
    this.touch();
  }

  selectEdge(edge: WorkflowEdge): void {
    this.selectedKey.set(null);
    this.selectedEdgeId.set(`${edge.from}>${edge.to}`);
  }

  isSelectedEdge(edge: WorkflowEdge): boolean {
    return this.selectedEdgeId() === `${edge.from}>${edge.to}`;
  }

  removeEdge(edge: WorkflowEdge): void {
    this.edges.update((edges) => edges.filter((candidate) => candidate !== edge));
    this.selectedEdgeId.set(null);
    this.touch();
  }

  onKey(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.linkFrom.set(null);
      return;
    }
    const edge = this.selectedEdge();
    if ((event.key === 'Delete' || event.key === 'Backspace') && edge && this.editable()) {
      const target = event.target as HTMLElement | null;
      if (target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA') {
        return;
      }
      this.removeEdge(edge);
    }
  }

  onDragMoved(key: string, event: CdkDragMove): void {
    this.drag.set({ key, dx: event.distance.x, dy: event.distance.y });
  }

  onDragEnded(key: string, event: CdkDragEnd): void {
    const { x, y } = event.distance;
    this.drag.set(null);
    event.source.reset();
    if (x === 0 && y === 0) {
      return;
    }
    this.nodes.update((nodes) =>
      nodes.map((node) =>
        node.key === key
          ? { ...node, x: Math.max(0, node.x + x), y: Math.max(0, node.y + y) }
          : node,
      ),
    );
    this.touch();
  }

  /** From the source's right edge to the target's left edge, following a node mid-drag. */
  edgePath(edge: WorkflowEdge): string {
    const from = this.position(edge.from);
    const to = this.position(edge.to);
    if (!from || !to) {
      return '';
    }
    const x1 = from.x + NODE_WIDTH;
    const y1 = from.y + NODE_HEIGHT / 2;
    const x2 = to.x;
    const y2 = to.y + NODE_HEIGHT / 2;
    const bend = Math.max(40, Math.abs(x2 - x1) / 2);
    return `M ${x1} ${y1} C ${x1 + bend} ${y1}, ${x2 - bend} ${y2}, ${x2} ${y2}`;
  }

  private position(key: string): { x: number; y: number } | null {
    const node = this.shownNodes().find((candidate) => candidate.key === key);
    if (!node) {
      return null;
    }
    const drag = this.drag();
    return drag?.key === key
      ? { x: node.x + drag.dx, y: node.y + drag.dy }
      : { x: node.x, y: node.y };
  }

  agentName(agentId: string): string {
    return this.agents().find((agent) => agent.agentId === agentId)?.name ?? 'Agente indisponível';
  }

  message(field: string): string | null {
    const problem = this.saveProblem();
    return problem ? fieldError(problem, field) : null;
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.saveProblem.set(null);
    const request = {
      name: this.name(),
      description: this.description(),
      nodes: this.nodes(),
      edges: this.edges(),
    };
    try {
      const workflowId = this.workflowId();
      const saved = await firstValueFrom(
        workflowId ? this.client.update(workflowId, request) : this.client.create(request),
      );
      this.dirty.set(false);
      if (!workflowId) {
        await this.router.navigate(['/ai/workflows', saved.workflowId]);
      }
    } catch (error: unknown) {
      this.saveProblem.set(parseProblem(error));
    } finally {
      this.saving.set(false);
    }
  }

  /** Route guard: leaving with unsaved changes asks first. */
  async canLeave(): Promise<boolean> {
    if (!this.dirty()) {
      return true;
    }
    return this.confirm.ask({
      title: 'Alterações por guardar',
      message: 'Sair sem guardar as alterações?',
      confirmLabel: 'Sair',
    });
  }

  private touch(): void {
    this.dirty.set(true);
    this.saveProblem.set(null);
  }

  // ---- runs ------------------------------------------------------------------------------

  async startRun(): Promise<void> {
    const workflowId = this.workflowId();
    if (!workflowId) {
      return;
    }
    this.starting.set(true);
    this.runProblem.set(null);
    try {
      const run = await firstValueFrom(this.client.run(workflowId, this.runInput()));
      this.showRun(run);
      await this.loadRuns();
    } catch (error: unknown) {
      this.runProblem.set(parseProblem(error));
    } finally {
      this.starting.set(false);
    }
  }

  async openRun(runId: string): Promise<void> {
    const workflowId = this.workflowId();
    if (!workflowId) {
      return;
    }
    this.showRun(await firstValueFrom(this.client.getRun(workflowId, runId)));
  }

  closeRun(): void {
    this.pollGeneration++;
    this.run.set(null);
    this.selectedKey.set(null);
  }

  private showRun(run: WorkflowRunOutput): void {
    this.selectedKey.set(null);
    this.selectedEdgeId.set(null);
    this.linkFrom.set(null);
    this.run.set(run);
    if (!this.isTerminal(run.status)) {
      void this.poll(run.runId, ++this.pollGeneration);
    }
  }

  /** Re-reads the run until it ends, the screen closes, or another run is opened. */
  private async poll(runId: string, generation: number): Promise<void> {
    const workflowId = this.workflowId()!;
    while (!this.destroyed && generation === this.pollGeneration) {
      await new Promise((resolve) => setTimeout(resolve, this.pollMs));
      if (this.destroyed || generation !== this.pollGeneration) {
        return;
      }
      try {
        const run = await firstValueFrom(this.client.getRun(workflowId, runId));
        if (this.destroyed || generation !== this.pollGeneration) {
          return;
        }
        this.run.set(run);
        if (this.isTerminal(run.status)) {
          await this.loadRuns();
          return;
        }
      } catch {
        // A transient failure keeps polling; the next tick tries again.
      }
    }
  }

  stepOf(key: string): WorkflowRunStepOutput | null {
    return this.run()?.steps.find((step) => step.nodeKey === key) ?? null;
  }

  isTerminal(status: WorkflowRunStatus): boolean {
    return status === 'Succeeded' || status === 'Failed';
  }

  stepLabel(status: WorkflowStepStatus): string {
    return STEP_LABELS[status];
  }

  runLabel(status: WorkflowRunStatus): string {
    return RUN_LABELS[status];
  }

  cost(value: number | null): string {
    return formatCost(value);
  }

  duration(run: WorkflowRunOutput): string {
    if (!run.startedAt || !run.finishedAt) {
      return '—';
    }
    const seconds = (Date.parse(run.finishedAt) - Date.parse(run.startedAt)) / 1000;
    return `${seconds.toFixed(1)} s`;
  }
}
