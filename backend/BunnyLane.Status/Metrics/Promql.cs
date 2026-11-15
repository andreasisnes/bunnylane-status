namespace BunnyLane.Status.Metrics;

/// <summary>Every PromQL string the app runs, in one place. Visitors never influence these.</summary>
public static class Promql
{
    // nodes
    public const string NodeReady = "kube_node_status_condition{condition=\"Ready\",status=\"true\"}";
    public const string NodeCount = "count(kube_node_info)";
    public const string NodeRole = "kube_node_role";
    public const string NodeCpu = "1 - avg by (instance) (rate(node_cpu_seconds_total{mode=\"idle\"}[5m]))";
    public const string NodeMem = "1 - node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes";
    public const string NodeLoad = "node_load1";
    public const string NodeUptime = "time() - node_boot_time_seconds";
    public const string NodeNames = "node_uname_info{job!=\"vps\"}";

    // network (workers' VLAN interface; WAN is the VPS eth0)
    public const string NetRx = "sum by (instance) (rate(node_network_receive_bytes_total{job!=\"vps\",device=\"eth0\"}[2m])) * 8";
    public const string NetTx = "sum by (instance) (rate(node_network_transmit_bytes_total{job!=\"vps\",device=\"eth0\"}[2m])) * 8";
    public const string WanRx = "rate(node_network_receive_bytes_total{job=\"vps\",device=\"eth0\"}[2m]) * 8";
    public const string WanTx = "rate(node_network_transmit_bytes_total{job=\"vps\",device=\"eth0\"}[2m]) * 8";

    // storage
    public const string LonghornCapacity = "longhorn_node_storage_capacity_bytes";
    public const string LonghornUsage = "longhorn_node_storage_usage_bytes";
    public const string LonghornVolumeRobustness = "longhorn_volume_robustness";
    public const string LonghornVolumeSize = "longhorn_volume_actual_size_bytes";
    public const string LonghornDegraded = "count(longhorn_volume_robustness >= 2) or vector(0)";
    public const string NasUsed = "kubelet_volume_stats_used_bytes{persistentvolumeclaim=\"unifi-nas-media\"}";
    public const string NasCapacity = "kubelet_volume_stats_capacity_bytes{persistentvolumeclaim=\"unifi-nas-media\"}";

    // gpu (node_exporter drm collector + hwmon)
    public const string GpuBusy = "node_drm_gpu_busy_percent";
    public const string GpuVramUsed = "node_drm_memory_vram_used_bytes";
    public const string GpuVramSize = "node_drm_memory_vram_size_bytes";
    // hwmon's chip label is a PCI path; the driver name is only in the info metric, hence the join. One value per node.
    public const string GpuTemp = "max by (instance) (node_hwmon_temp_celsius * on (instance, chip) group_left() node_hwmon_chip_names{chip_name=\"amdgpu\"})";

    // vps monthly counters (raw, so callers can sum positive deltas and tolerate resets)
    public const string VpsRx = "node_network_receive_bytes_total{job=\"vps\",device=\"eth0\"}";
    public const string VpsTx = "node_network_transmit_bytes_total{job=\"vps\",device=\"eth0\"}";
    public const string VpsUp = "up{job=\"vps\"}";

    // health, counts only
    public const string DeploymentsUnavailable = "count(kube_deployment_status_replicas_available < kube_deployment_spec_replicas) or vector(0)";
    public const string FluxNotReady = "count(gotk_reconcile_condition{type=\"Ready\",status=\"False\"} == 1) or vector(0)";
}
