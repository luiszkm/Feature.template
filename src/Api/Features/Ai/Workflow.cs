using Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Features.Ai;

/// <summary>A saved graph of agent nodes linked by dependency edges; runs copy it (see <see cref="WorkflowRun"/>).</summary>
public sealed class Workflow : AggregateRoot, IMultiTenantEntity
{
    private readonly List<WorkflowNode> _nodes = [];
    private readonly List<WorkflowEdge> _edges = [];

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime UpdatedAt { get; private set; }
    /// <summary>In the order they were given; a relational read does not keep it, so it is stored.</summary>
    public IReadOnlyList<WorkflowNode> Nodes => _nodes.OrderBy(n => n.Position).ToList();
    public IReadOnlyList<WorkflowEdge> Edges => _edges;

    private Workflow() { }

    public static Workflow Create(
        Guid tenantId,
        string name,
        string? description,
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges)
    {
        if (tenantId == Guid.Empty)
            throw new BusinessRuleException("TenantId is required.");

        var now = DateTime.UtcNow;
        var workflow = new Workflow { TenantId = tenantId, CreatedAt = now };
        workflow.Apply(name, description, nodes, edges, now);
        return workflow;
    }

    /// <summary>Replaces everything; <c>UpdatedAt</c> always moves forward, even within one clock tick.</summary>
    public void Replace(string name, string? description, IReadOnlyList<WorkflowNode> nodes, IReadOnlyList<WorkflowEdge> edges)
    {
        var now = DateTime.UtcNow;
        Apply(name, description, nodes, edges, now > UpdatedAt ? now : UpdatedAt.AddTicks(1));
    }

    public void Deactivate() => IsActive = false;

    private void Apply(
        string name,
        string? description,
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges,
        DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        _nodes.Clear();
        _nodes.AddRange(nodes.Select((node, position) => node.At(position)));
        _edges.Clear();
        _edges.AddRange(edges);
        UpdatedAt = updatedAt;
    }
}

public sealed class WorkflowNode
{
    public string Key { get; private set; } = string.Empty;
    public Guid AgentId { get; private set; }
    public string? Instruction { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public int Position { get; private set; }

    private WorkflowNode() { }

    public WorkflowNode(string key, Guid agentId, string? instruction, double x, double y)
    {
        Key = key;
        AgentId = agentId;
        Instruction = string.IsNullOrWhiteSpace(instruction) ? null : instruction;
        X = x;
        Y = y;
    }

    internal WorkflowNode At(int position)
    {
        Position = position;
        return this;
    }
}

public sealed class WorkflowEdge
{
    public string FromKey { get; private set; } = string.Empty;
    public string ToKey { get; private set; } = string.Empty;

    private WorkflowEdge() { }

    public WorkflowEdge(string fromKey, string toKey)
    {
        FromKey = fromKey;
        ToKey = toKey;
    }
}

/// <summary>Graph questions both the validator and the runner ask.</summary>
public static class WorkflowGraph
{
    /// <summary>The keys left on a cycle (Kahn's algorithm), or null when the graph is acyclic.</summary>
    public static IReadOnlyList<string>? FindCycle(IReadOnlyList<string> keys, IReadOnlyList<(string From, string To)> edges)
    {
        var inDegree = keys.Distinct(StringComparer.Ordinal).ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
        foreach (var (_, to) in edges)
            if (inDegree.ContainsKey(to))
                inDegree[to]++;

        var queue = new Queue<string>(inDegree.Where(p => p.Value == 0).Select(p => p.Key));
        var visited = 0;
        while (queue.Count > 0)
        {
            var key = queue.Dequeue();
            visited++;
            foreach (var (from, to) in edges)
            {
                if (from != key || !inDegree.ContainsKey(to))
                    continue;
                if (--inDegree[to] == 0)
                    queue.Enqueue(to);
            }
        }

        return visited == inDegree.Count ? null : inDegree.Where(p => p.Value > 0).Select(p => p.Key).ToList();
    }

    /// <summary>Predecessor keys of each node, in the order the nodes are listed.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Predecessors(
        IReadOnlyList<string> keys,
        IReadOnlyList<(string From, string To)> edges)
    {
        var position = keys.Select((key, index) => (key, index)).ToDictionary(p => p.key, p => p.index, StringComparer.Ordinal);
        return keys.ToDictionary(
            key => key,
            key => (IReadOnlyList<string>)edges
                .Where(e => e.To == key)
                .Select(e => e.From)
                .OrderBy(from => position.GetValueOrDefault(from, int.MaxValue))
                .ToList(),
            StringComparer.Ordinal);
    }
}

public interface IWorkflowRepository
{
    Task<Workflow?> GetByIdAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<PaginatedListOutput<Workflow>> ListActiveAsync(ListQuery listQuery, CancellationToken cancellationToken = default);
    Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default);
}

internal sealed class WorkflowRepository(AppDbContext db) : IWorkflowRepository
{
    public Task<Workflow?> GetByIdAsync(Guid workflowId, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.GetByIdAsync(db.Set<Workflow>(), workflowId, cancellationToken);

    public Task<PaginatedListOutput<Workflow>> ListActiveAsync(ListQuery listQuery, CancellationToken cancellationToken = default) =>
        db.Set<Workflow>()
            .AsNoTracking()
            .Where(w => w.IsActive)
            .OrderByDescending(w => w.UpdatedAt)
            .ThenBy(w => w.Id)
            .ToPaginatedListAsync(listQuery, cancellationToken);

    public Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default) =>
        EfRepositoryHelpers.AddAsync(db.Set<Workflow>(), workflow, cancellationToken);
}

internal sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> entity)
    {
        entity.ToTable("AiWorkflows");
        entity.HasKey(w => w.Id);
        entity.Property(w => w.Name).HasMaxLength(200).IsRequired();
        entity.Property(w => w.Description).HasMaxLength(1000);
        entity.HasIndex(w => new { w.TenantId, w.UpdatedAt });

        entity.OwnsMany(w => w.Nodes, node =>
        {
            node.ToTable("AiWorkflowNodes");
            node.WithOwner().HasForeignKey("WorkflowId");
            node.Property<int>("Id");
            node.HasKey("Id");
            node.Property(n => n.Key).HasMaxLength(50).IsRequired();
            node.Property(n => n.Instruction).HasMaxLength(2000);
            node.HasIndex("WorkflowId", nameof(WorkflowNode.Key)).IsUnique();
            node.HasOne<Agent>().WithMany().HasForeignKey(n => n.AgentId).OnDelete(DeleteBehavior.Restrict);
        });
        entity.Navigation(w => w.Nodes)
            .HasField("_nodes")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();

        entity.OwnsMany(w => w.Edges, edge =>
        {
            edge.ToTable("AiWorkflowEdges");
            edge.WithOwner().HasForeignKey("WorkflowId");
            edge.Property<int>("Id");
            edge.HasKey("Id");
            edge.Property(e => e.FromKey).HasMaxLength(50).IsRequired();
            edge.Property(e => e.ToKey).HasMaxLength(50).IsRequired();
            edge.HasIndex("WorkflowId", nameof(WorkflowEdge.FromKey), nameof(WorkflowEdge.ToKey)).IsUnique();
        });
        entity.Navigation(w => w.Edges)
            .HasField("_edges")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
    }
}
