namespace BunnyLane.Status.Metrics;

/// <summary>A node as the public sees it: an alias like <c>cp-1</c> or <c>w-2</c>, never the hostname.</summary>
public sealed record NodeRef(string Hostname, string Instance, string Alias, bool IsControlPlane);

public sealed class NodeMap(IReadOnlyList<NodeRef> nodes)
{
    public IReadOnlyList<NodeRef> Nodes => nodes;
    public IEnumerable<NodeRef> Workers => nodes.Where(n => !n.IsControlPlane);

    public string AliasForInstance(string instance) => nodes.FirstOrDefault(n => n.Instance == instance)?.Alias ?? "?";
    public string AliasForHost(string hostname) => nodes.FirstOrDefault(n => n.Hostname == hostname)?.Alias ?? "?";

    /// <summary>Joins kube_node_role with node_uname_info and hands out aliases in hostname order.</summary>
    public static async Task<NodeMap> LoadAsync(IPrometheus prom, CancellationToken ct)
    {
        var roles = await prom.QueryAsync(Promql.NodeRole, ct);
        var names = await prom.QueryAsync(Promql.NodeNames, ct);
        var controlPlanes = roles.Where(r => r.Labels.GetValueOrDefault("role") == "control-plane")
            .Select(r => r.Labels.GetValueOrDefault("node", "")).ToHashSet();

        var hosts = names.Select(n => (Host: n.Labels.GetValueOrDefault("nodename", ""), Instance: n.Labels.GetValueOrDefault("instance", "")))
            .Where(h => h.Host.Length > 0).OrderBy(h => h.Host, StringComparer.Ordinal).ToList();

        var cp = 0; var w = 0;
        var refs = hosts.Select(h =>
        {
            var isCp = controlPlanes.Contains(h.Host);
            return new NodeRef(h.Host, h.Instance, isCp ? $"cp-{++cp}" : $"w-{++w}", isCp);
        }).ToList();
        return new NodeMap(refs);
    }

    /// <summary>Instant values keyed by the alias of their <c>instance</c> label.</summary>
    public Dictionary<string, double> ByInstance(Instant[] values) =>
        values.GroupBy(v => AliasForInstance(v.Labels.GetValueOrDefault("instance", "")))
            .ToDictionary(g => g.Key, g => g.First().Value);

    public Dictionary<string, double> ByHost(Instant[] values) =>
        values.GroupBy(v => AliasForHost(v.Labels.GetValueOrDefault("node", "")))
            .ToDictionary(g => g.Key, g => g.First().Value);
}
