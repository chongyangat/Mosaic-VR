# GazeDataSimulator Guide

## Overview

`GazeDataSimulator` generates synthetic `EyeGaze` samples and sends them to a configured IP address and port over UDP. It is intended for development and receiver testing when live headset gaze data is unavailable.

The implementation is compiled only when `GAZE_DATA_SIMULATOR` is defined. In the current application registration path, it is created only for the `MANAGER_SERVER` build.

## Features

- Compile-time control through `GAZE_DATA_SIMULATOR`.
- Configurable send interval, target IP address, and target port.
- Independent left-eye, right-eye, and binocular validity controls.
- Randomized gaze-point generation.
- Singleton access and automatic `GameObject` creation.
- Single-sample test transmission through code.
- Optional alternating sparse-point and dense-point modes.
- Configurable sparse/dense durations and dense-mode send interval.
- Configurable dense-point clustering and randomized cluster center.
- Optional restriction to the current camera frustum.
- Optional logging and configurable minimum Z depth.

## Enable and initialize

Add `GAZE_DATA_SIMULATOR` to the relevant Unity scripting define symbols. The current `GameApp_RegisterSystem.cs` registration is equivalent to:

```csharp
#if MANAGER_SERVER && GAZE_DATA_SIMULATOR
GazeDataSimulator.Create();
Log.Debug("GameApp: initialized GazeDataSimulator");
#endif
```

The simulator can also be created and configured manually:

```csharp
#if GAZE_DATA_SIMULATOR
GazeDataSimulator simulator = GazeDataSimulator.Create(
    sendInterval: 0.1f,
    targetIP: "127.0.0.1",
    targetPort: 8082);

simulator.SetSendInterval(0.5f);
simulator.SetTarget("192.168.1.100", 8082);
simulator.TestSendSingleData();
#endif
```

`Create()` defaults to a `0.01` second interval, `127.0.0.1`, and port `8082`. A component added through the Unity Inspector has a serialized default interval of `0.5` seconds. Always configure the sender to match the receiver actually used by the scene or build.

## Inspector configuration

Select the `GameObject` containing `GazeDataSimulator` and configure the following fields in the Inspector:

### Basic settings

| Setting | Description | Serialized default |
| --- | --- | --- |
| Send Interval | Seconds between samples in sparse mode. Values passed to `SetSendInterval` are clamped to at least `0.01`. | `0.5` |
| Target IP | Receiver IP address. | `127.0.0.1` |
| Target Port | Receiver UDP port. | `8082` |
| Enable Logging | Writes transmission and mode-change messages to the Unity log. | Enabled |

### Eye-data settings

| Setting | Description | Serialized default |
| --- | --- | --- |
| Send Left Eye Data | Marks and populates the left-eye sample. | Enabled |
| Send Right Eye Data | Marks and populates the right-eye sample. | Enabled |
| Send Double Eye Data | Calculates and sends a binocular sample. | Enabled |

### Position generation

| Setting | Description | Serialized default |
| --- | --- | --- |
| Limit To Camera View | Restricts generated points to the current camera's visible region. | Disabled |
| Random Range X | Random X range. | `1.0` |
| Random Range Y | Random Y range. | `1.0` |
| Minimum Z Value | Minimum generated depth. | `0.5` |
| Random Range Z | Random Z range. | `3.0` |

### Dense-point mode

| Setting | Description | Serialized default |
| --- | --- | --- |
| Enable Dense Points | Alternates between sparse and clustered dense samples. | Disabled |
| Sparse Duration Min/Max | Duration range for sparse mode. | `1.0` / `5.0` seconds |
| Dense Duration Min/Max | Duration range for dense mode. | `1.0` / `5.0` seconds |
| Dense Send Interval | Send interval while dense mode is active. | `0.05` seconds |
| Dense Position Factor | Cluster spread; smaller values create tighter clusters. | `0.3` |
| Dense Center Range X/Y/Z | Random range used to choose the cluster center. | `1.0` / `1.0` / `3.0` |

## Runtime control

```csharp
#if GAZE_DATA_SIMULATOR
if (GazeDataSimulator.HasInstance)
{
    GazeDataSimulator simulator = GazeDataSimulator.Instance;

    simulator.enabled = true;  // Start automatic transmission.
    simulator.TestSendSingleData();
    simulator.enabled = false; // Stop automatic transmission.
}
#endif
```

## UDP payload

The simulator serializes `GazeNetData` as JSON. A representative payload is:

```json
{
  "timeStamp": 1234567890123,
  "isLeftEyeValid": true,
  "leftEyeGazePos": "(0.123,0.456,1.234)",
  "isRightEyeValid": true,
  "rightEyeGazePos": "(0.234,0.567,1.345)",
  "isDoubleEyesValid": true,
  "doubleEyesGazePos": "(0.178,0.511,1.289)"
}
```

## Performance guidance

- Keep the normal send interval at or above `0.01` seconds.
- Avoid unnecessarily large random ranges.
- Disable the component when simulation is not required.
- Do not set the dense-mode interval so low that it saturates the receiver or local network.
- On lower-performance devices, reduce dense-mode frequency or disable dense-point generation.

## Notes

- `UDPManager` must be initialized and available before a sample can be sent.
- The target IP and port must match the receiver configuration.
- Do not define `GAZE_DATA_SIMULATOR` in a production data-collection build.
- This public source snapshot omits other dependencies required to compile and run the complete application.
