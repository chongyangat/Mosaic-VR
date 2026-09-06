# Network Performance Monitor

## Overview

`NetworkPerformanceMonitor` is a Unity/Mirror monitoring component for per-link latency, jitter, packet loss, frame timing, and clock-synchronization metadata. It maintains independent history buffers for multiple logical links, publishes updates to runtime UI, and can record or export samples as CSV.

> [!NOTE]
> This document describes the API present in this source snapshot. The public repository omits scenes and required third-party/local packages, so its examples are for code reference and for integration into an authorized complete project.

## Implemented capabilities

- Per-link sampling and independent circular history buffers.
- Current, recent, ranged, and aggregated performance queries.
- Latency distribution statistics: minimum, maximum, median, 95th percentile, and 99th percentile.
- Packet-sequence tracking and packet-loss estimates.
- Eye-tracking and platform-specific MoCap measurement entry points.
- Clock-synchronization validity, offset, RTT, age, uncertainty, and sample-count metadata.
- Link activation and priority controls.
- CSV export and continuous recording.
- Runtime UI for latency, jitter, packet loss, distributions, frame timing, link selection, priority, and charts.
- Optional garbage-collection monitoring.

## Setup

### Automatic setup

Provide an existing Mirror `NetworkManager` to the integration helper:

```csharp
using GameMain;
using Mirror;

public class MonitorBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;

    private void Start()
    {
        NetworkPerformanceMonitor monitor =
            NetworkPerformanceIntegration.SetupNetworkPerformanceMonitor(
                networkManager,
                enableUI: true);

        monitor.OnPerformanceUpdated += HandlePerformanceUpdated;
    }

    private void HandlePerformanceUpdated(
        NetworkPerformanceMonitor.PerformanceData data)
    {
        Debug.Log(
            $"[{data.link}] latency={data.latency:F1} ms, " +
            $"jitter={data.jitter:F1} ms, loss={data.packetLoss:F1}%");
    }
}
```

`SetupNetworkPerformanceMonitor` creates a persistent monitor `GameObject`. When `enableUI` is true, it also creates the runtime performance UI. It returns the existing singleton if one is already active.

### Manual setup

1. Create a `GameObject` named `NetworkPerformanceMonitor`.
2. Add the `NetworkPerformanceMonitor` component.
3. Optionally add and wire a `NetworkPerformanceUI` canvas.
4. Initialize logical links before sampling them, or let the monitor initialize a link when it is first selected.

## Monitor configuration

| Field | Purpose | Default |
| --- | --- | --- |
| Enabled | Enables periodic monitoring. | `true` |
| Sample Interval | Sampling interval for ordinary links. The PC↔VR state-sync link follows the Mirror send interval. | `1.0 s` |
| Buffer Size | Maximum samples retained per link in the circular buffer. | `54,000` |
| Jitter Window Size | Number of latency samples used for the moving jitter calculation. | `20` |
| Report Interval | Interval used when the VR side reports performance data. | `1.0 s` |
| Enable GC Monitoring | Enables garbage-collection monitoring. | `true` |
| `IsShowLog` | Enables detailed diagnostic logging. | `false` |

`NegativeLatencyMode` has two values:

- `Raw`: preserves negative directional latency values so clock-offset direction remains visible.
- `Processed`: applies an absolute value instead of emitting a negative value or a false zero.

## Common usage

### Select and sample a link

```csharp
NetworkPerformanceMonitor monitor = NetworkPerformanceMonitor.Instance;

monitor.InitializeLink("EyeTracking");
monitor.SetLinkPriority(
    "EyeTracking",
    NetworkPerformanceMonitor.LinkPriority.High);
monitor.SetCurrentLink("EyeTracking");
monitor.SamplePerformanceData("EyeTracking");
```

### Query data

```csharp
var latest = monitor.GetLatestData("EyeTracking");
var recent = monitor.GetRecentHistoryData(120, "EyeTracking");
var range = monitor.GetHistoryDataInRange(
    startTime: 10.0f,
    endTime: 30.0f,
    link: "EyeTracking");
var average = monitor.GetAverageData(60.0f, "EyeTracking");
```

### Export and record

```csharp
string directory = Application.persistentDataPath;

monitor.ExportToCsv(
    System.IO.Path.Combine(directory, "eye_tracking.csv"),
    "EyeTracking");

monitor.StartRecording(
    System.IO.Path.Combine(directory, "network_session.csv"));

// Run the monitored session here.

string recordedPath = monitor.StopRecording();
Debug.Log($"Recording written to: {recordedPath}");
```

## PerformanceData

Every sample uses `NetworkPerformanceMonitor.PerformanceData`.

| Field | Type | Meaning |
| --- | --- | --- |
| `sampleTimestamp` | `string` | Synchronized collection time formatted for export. |
| `sentTimestamp` | `ulong` | Sender Unix timestamp in milliseconds. |
| `latency` | `float` | Measured latency in milliseconds. |
| `jitter` | `float` | Jitter in milliseconds. |
| `packetLoss` | `float` | Packet-loss percentage from 0 to 100. |
| `latencyMin` | `float` | Minimum latency in the distribution. |
| `latencyMax` | `float` | Maximum latency in the distribution. |
| `latencyMedian` | `float` | Median latency. |
| `latency95th` | `float` | 95th-percentile latency. |
| `latency99th` | `float` | 99th-percentile latency. |
| `frameArrivalDelay` | `float` | Frame arrival delay. |
| `frameInterval` | `float` | Frame interval in milliseconds. |
| `frameLossRate` | `float` | Frame-loss rate. |
| `sequenceNumber` | `uint` | Current packet or frame sequence number. |
| `link` | `string` | Logical link identifier. |
| `measurementType` | `string` | Measurement semantics, such as RTT or estimated one-way delay. |
| `syncValid` | `bool` | Whether the directional sample had a fresh, stable clock estimate. |
| `syncState` | `string` | Clock-synchronization state at collection time. |
| `clockOffsetMs` | `double` | Estimated remote-minus-local clock offset in milliseconds. |
| `syncRttMs` | `double` | Network-only RTT of the selected four-timestamp synchronization sample. |
| `syncAgeMs` | `double` | Age of the latest accepted synchronization sample. |
| `offsetUncertaintyMs` | `double` | Upper-bound estimate of offset ambiguity caused by path asymmetry. |
| `syncSampleCount` | `int` | Accepted synchronization samples in the current session. |

## Public API reference

Optional `link` arguments use the current link when omitted or `null`.

### State and events

| Member | Description |
| --- | --- |
| `Instance` | Active singleton instance. |
| `IsEnabled` | Gets or sets periodic monitoring. |
| `CurrentLink` | Gets or sets the selected logical link. |
| `AllLinks` | Lists initialized links. |
| `IsRecording` | Reports whether continuous recording is active. |
| `NegativeLatencyMode` | Returns the configured negative-latency behavior. |
| `StateSyncSampleInterval` | Returns the sampling interval used for PC↔VR state synchronization. |
| `OnPerformanceUpdated` | The only monitor event; publishes a `PerformanceData` sample. |

### Link management and sampling

| Method | Description |
| --- | --- |
| `InitializeLink(string)` | Creates storage and state for a logical link. |
| `SetCurrentLink(string)` | Selects the active link. |
| `ResetStatistics(string link = null)` | Clears statistics for one link or the current link. |
| `SetLinkActive(string, bool)` | Enables or disables a link. |
| `IsLinkActive(string)` | Returns a link's active state. |
| `UpdateLinkActivity(string)` | Marks recent activity for a link. |
| `SetLinkPriority(string, LinkPriority)` | Assigns `Low`, `Medium`, `High`, or `Critical` priority. |
| `GetLinkPriority(string)` | Returns the assigned priority. |
| `GetLinksByPriority()` | Returns links ordered by priority. |
| `SampleAllLinksByPriority()` | Samples enabled links in priority order. |
| `SamplePerformanceData(string)` | Produces a sample for one link. |

### History and aggregation

| Method | Description |
| --- | --- |
| `GetLatestData(string link = null)` | Returns the latest sample. |
| `GetAllHistoryData(string link = null)` | Returns all retained samples. |
| `GetHistoryDataInRange(float, float, string link = null)` | Returns samples in a time range. |
| `GetHistoryData(float, float, string link = null)` | Alias for the ranged history query. |
| `GetRecentHistoryData(int, string link = null)` | Returns the most recent sample count. |
| `GetHistoryCount(string link = null)` | Returns the number of retained samples. |
| `GetAverageLatency/Jitter/PacketLoss(string link = null)` | Returns one aggregate metric. |
| `GetAverageData(float duration, string link = null)` | Aggregates one link over a duration. |
| `GetAllLinksAggregatedData(float duration)` | Aggregates every link over a duration. |
| `GetAllLinksLatestData()` | Returns the latest sample for every link. |

### Export and recording

| Method | Description |
| --- | --- |
| `ExportData(string, string link = null)` | Compatibility wrapper that exports link history. |
| `ExportToCsv(string, string link = null)` | Exports all retained data for a link. |
| `ExportToCsv(string, float, float, string link = null)` | Exports a time range. |
| `ExportAllLinksToCsv(string)` | Exports every initialized link. |
| `StartRecording(string filePath = null)` | Begins continuous CSV recording. |
| `StopRecording()` | Stops recording and returns the output path. |

### Measurement entry points

| Method | Description |
| --- | --- |
| `UpdateSequenceNumber(uint, string link = null)` | Updates sequence state for packet-loss calculation. |
| `RecordPacketReceived(uint, string link = null)` | Records packet arrival for a link. |
| `OnStateSyncData()` | Records state-synchronization activity. |
| `OnEyeTrackingDataReceived(uint, ulong[, double])` | Records an eye-tracking sample using sender and optional arrival timestamps. |
| `OnMoCapToPCFrameReceived(uint, ulong, float)` | Records a MoCap-to-PC sample in a `MANAGER_SERVER` build. |
| `OnMoCapToVRFrameReceived(uint, ulong, float)` | Records a MoCap-to-VR sample in a non-manager build. |
| `GetSyncedUnixTimeMilliseconds()` | Returns synchronized Unix time as `ulong`. |
| `GetSyncedUnixTimeMillisecondsD()` | Returns synchronized Unix time as `double`. |

## Current implementation limits

- `MarkEventStart` currently changes/selects the event link; `MarkEventEnd` is a stub that returns `0f`. There is no event-delay history API in this snapshot.
- `SetupMoCapMonitoring` is an integration placeholder. The legacy `OnMoCapFrameReceived` overloads log a warning and do not create valid measurements; use the platform-specific timestamped MoCap methods above.
- `ProcessMoCapFrame` and `SimulateMoCapData` call those legacy MoCap overloads and therefore do not generate real performance metrics in this snapshot.
- `NetworkPerformanceWrapper` forwards `INetwork.Send` and `OnReceive`; it does not itself measure message performance.
- There is no alert-threshold system, anomaly event, bandwidth counter, trend API, network-quality assessment API, or automatic optimization API in the checked-in implementation.

## Performance guidance

- Increase the ordinary-link sample interval when high-frequency updates are unnecessary.
- Reduce the per-link buffer size if the default history capacity is excessive for the target device.
- Disable or simplify the runtime UI in production builds that do not need live charts.
- Export or stop recording before terminating a session so buffered data is written to disk.
- Validate clock synchronization before interpreting estimated one-way latency; use `syncValid`, `syncState`, `syncAgeMs`, and `offsetUncertaintyMs` together.
