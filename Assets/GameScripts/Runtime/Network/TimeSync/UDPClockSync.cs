using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// Four-timestamp UDP clock synchronization for directional latency estimates.
    ///
    /// PC -> VR request: t1 is captured immediately before serialization/send.
    /// VR -> PC response: t2 is captured at the UDP receive boundary and t3
    /// immediately before response serialization/send. PC supplies t4 from its UDP
    /// receive boundary.
    ///
    /// ClockDiff = VR clock - PC clock
    /// Offset = ((t2 - t1) + (t3 - t4)) / 2
    /// Network RTT = (t4 - t1) - (t3 - t2)
    /// </summary>
    public static class UDPClockSync
    {
        public readonly struct ClockSyncSnapshot
        {
            public ClockSyncSnapshot(
                double clockDiff,
                double roundTripTime,
                double age,
                double uncertainty,
                int sampleCount,
                bool isReady,
                string state)
            {
                ClockDiff = clockDiff;
                RoundTripTimeMs = roundTripTime;
                AgeMs = age;
                OffsetUncertaintyMs = uncertainty;
                SampleCount = sampleCount;
                IsReady = isReady;
                State = state;
            }

            public double ClockDiff { get; }
            public double RoundTripTimeMs { get; }
            public double AgeMs { get; }
            public double OffsetUncertaintyMs { get; }
            public int SampleCount { get; }
            public bool IsReady { get; }
            public string State { get; }
        }

        private readonly struct SyncSample
        {
            public SyncSample(double offset, double networkRtt)
            {
                Offset = offset;
                NetworkRtt = networkRtt;
            }

            public double Offset { get; }
            public double NetworkRtt { get; }
        }

        private const int InitialBurstCount = 8;
        private const int MinimumReadySamples = 5;
        private const int SelectedLowRttSamples = 5;
        private const int SampleWindowSize = 20;
        private const float BurstIntervalSeconds = 0.12f;
        private const float ResyncIntervalSeconds = 10f;
        private const double MaximumSyncAgeMs = 30000d;
        private const double MaximumAcceptedNetworkRttMs = 1000d;
        private const double MaximumRemoteProcessingMs = 250d;

        private static readonly object s_syncLock = new object();
        private static readonly List<SyncSample> s_samples = new List<SyncSample>(SampleWindowSize);

        private static double s_clockDiff;
        private static double s_lastRtt = -1d;
        private static double s_offsetUncertaintyMs = -1d;
        private static double s_lastSyncLocalTimeMs;
        private static int s_acceptedSampleCount;
        private static int s_requestSequence;
        private static int s_burstRequestsRemaining;
        private static float s_nextBurstRequestTime;
        private static float s_nextPeriodicBurstTime;
        private static bool s_hasStableEstimate;
        private static bool s_isServer;
        private static readonly Dictionary<int, double> s_pendingRequests = new Dictionary<int, double>();

        private static int s_clientPort = 8081;

        public static double ClockDiff => GetSnapshot().ClockDiff;
        public static bool IsSynced => GetSnapshot().IsReady;
        public static bool IsMeasurementReady => GetSnapshot().IsReady;
        public static double LastRTT => GetSnapshot().RoundTripTimeMs;
        public static double SyncAgeMs => GetSnapshot().AgeMs;
        public static double OffsetUncertaintyMs => GetSnapshot().OffsetUncertaintyMs;
        public static int AcceptedSampleCount => GetSnapshot().SampleCount;
        public static string SyncState => GetSnapshot().State;

        public static double GetLocalUnixTimeMsD()
        {
            return MetaQuestProEyeGazeUDP.UDP.GetHighPrecisionUnixTimeMsD();
        }

        public static ClockSyncSnapshot GetSnapshot()
        {
            lock (s_syncLock)
            {
                double ageMs = s_lastSyncLocalTimeMs > 0d
                    ? Math.Max(0d, GetLocalUnixTimeMsD() - s_lastSyncLocalTimeMs)
                    : -1d;
                bool isReady = s_hasStableEstimate && ageMs >= 0d && ageMs <= MaximumSyncAgeMs;
                string state = isReady
                    ? "Ready"
                    : s_hasStableEstimate ? "Stale" : s_acceptedSampleCount > 0 ? "Collecting" : "Unsynced";

                return new ClockSyncSnapshot(
                    s_clockDiff,
                    s_lastRtt,
                    ageMs,
                    s_offsetUncertaintyMs,
                    s_acceptedSampleCount,
                    isReady,
                    state);
            }
        }

        public static void Initialize(bool isServer)
        {
            lock (s_syncLock)
            {
                s_isServer = isServer;
                ResetEstimateLocked();
                s_burstRequestsRemaining = isServer ? InitialBurstCount : 0;
                s_nextBurstRequestTime = 0f;
                s_nextPeriodicBurstTime = Time.realtimeSinceStartup + ResyncIntervalSeconds;
            }

            Debug.Log($"[UDPClockSync] Initialized as {(isServer ? "Server(PC)" : "Client(VR)")}");
        }

        public static void Update()
        {
            if (!s_isServer)
            {
                return;
            }

            bool shouldSend = false;
            float now = Time.realtimeSinceStartup;
            lock (s_syncLock)
            {
                if (s_burstRequestsRemaining <= 0 && now >= s_nextPeriodicBurstTime)
                {
                    s_burstRequestsRemaining = InitialBurstCount;
                    s_nextBurstRequestTime = now;
                    s_nextPeriodicBurstTime = now + ResyncIntervalSeconds;
                }

                if (s_burstRequestsRemaining > 0 && now >= s_nextBurstRequestTime)
                {
                    s_burstRequestsRemaining--;
                    s_nextBurstRequestTime = now + BurstIntervalSeconds;
                    shouldSend = true;
                }
            }

            if (shouldSend)
            {
                SendSyncRequest();
            }
        }

        /// <summary>
        /// Starts a fresh synchronization burst. Measurements remain invalid until
        /// enough low-RTT samples have produced a stable estimate.
        /// </summary>
        public static void RequestImmediateSyncBurst(bool resetEstimate = true)
        {
            lock (s_syncLock)
            {
                if (!s_isServer)
                {
                    return;
                }

                if (resetEstimate)
                {
                    ResetEstimateLocked();
                }

                s_burstRequestsRemaining = InitialBurstCount;
                s_nextBurstRequestTime = 0f;
                s_nextPeriodicBurstTime = Time.realtimeSinceStartup + ResyncIntervalSeconds;
            }

            Debug.Log("[UDPClockSync] Immediate synchronization burst requested.");
        }

        public static void SetClientAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip))
            {
                return;
            }

            var udp = MetaQuestProEyeGazeUDP.UDPManager.Instance;
            if (udp != null && udp.udp != null)
            {
                udp.udp.UDPClientAddRess = ip;
                udp.udp.UDPClientPort = s_clientPort;
                Debug.Log($"[UDPClockSync] UDP target updated: {ip}:{s_clientPort}");
            }

            RequestImmediateSyncBurst();
        }

        private static void SendSyncRequest()
        {
            try
            {
                ClockSyncSnapshot snapshot = GetSnapshot();
                int sequence;
                lock (s_syncLock)
                {
                    sequence = ++s_requestSequence;
                }

                double t1 = GetLocalUnixTimeMsD();
                lock (s_syncLock)
                {
                    s_pendingRequests[sequence] = t1;
                    if (s_pendingRequests.Count > InitialBurstCount * 2)
                    {
                        // A new burst supersedes unanswered requests from an old
                        // network state. Keeping the newest sequence prevents a
                        // delayed packet from contaminating a fresh estimate.
                        int oldestSequence = int.MaxValue;
                        foreach (int pendingSequence in s_pendingRequests.Keys)
                        {
                            oldestSequence = Math.Min(oldestSequence, pendingSequence);
                        }
                        s_pendingRequests.Remove(oldestSequence);
                    }
                }
                string msg = JsonUtility.ToJson(new ClockSyncRequest
                {
                    cmd = "clocksync",
                    sequence = sequence,
                    t1 = t1,
                    clockDiff = snapshot.ClockDiff,
                    syncValid = snapshot.IsReady,
                    syncRttMs = snapshot.RoundTripTimeMs,
                    offsetUncertaintyMs = snapshot.OffsetUncertaintyMs,
                    sampleCount = snapshot.SampleCount
                });
                SendUDP(msg);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UDPClockSync] SendSyncRequest error: {exception}");
            }
        }

        /// <summary>
        /// Handles synchronization messages on the UDP processing thread.
        /// arrivalTimeMs must be captured immediately after Socket.ReceiveFrom.
        /// </summary>
        public static bool HandleMessage(string msg, double arrivalTimeMs = 0d)
        {
            try
            {
                if (msg.Contains("\"clocksync_resp\""))
                {
                    ClockSyncResponse response = JsonUtility.FromJson<ClockSyncResponse>(msg);
                    if (response.cmd != "clocksync_resp")
                    {
                        return false;
                    }

                    lock (s_syncLock)
                    {
                        if (!s_pendingRequests.TryGetValue(response.sequence, out double expectedT1) ||
                            Math.Abs(expectedT1 - response.t1) > 0.01d)
                        {
                            Debug.LogWarning($"[UDPClockSync] Ignored unmatched response sequence {response.sequence}.");
                            return true;
                        }

                        s_pendingRequests.Remove(response.sequence);
                    }

                    double t4 = arrivalTimeMs > 0d ? arrivalTimeMs : GetLocalUnixTimeMsD();
                    ProcessSyncSample(response.t1, response.t2, response.t3, t4);
                    return true;
                }

                if (msg.Contains("\"clocksync\""))
                {
                    ClockSyncRequest request = JsonUtility.FromJson<ClockSyncRequest>(msg);
                    if (request.cmd != "clocksync")
                    {
                        return false;
                    }

                    double t2 = arrivalTimeMs > 0d ? arrivalTimeMs : GetLocalUnixTimeMsD();
                    lock (s_syncLock)
                    {
                        if (request.syncValid)
                        {
                            s_clockDiff = request.clockDiff;
                            s_lastRtt = request.syncRttMs;
                            s_offsetUncertaintyMs = request.offsetUncertaintyMs;
                            s_hasStableEstimate = true;
                            s_acceptedSampleCount = Math.Max(MinimumReadySamples, request.sampleCount);
                            s_lastSyncLocalTimeMs = t2;
                        }
                        else
                        {
                            s_hasStableEstimate = false;
                        }
                    }

                    double t3 = GetLocalUnixTimeMsD();
                    string responseJson = JsonUtility.ToJson(new ClockSyncResponse
                    {
                        cmd = "clocksync_resp",
                        sequence = request.sequence,
                        t1 = request.t1,
                        t2 = t2,
                        t3 = t3
                    });
                    SendUDP(responseJson);
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UDPClockSync] HandleMessage error: {exception}");
            }

            return false;
        }

        private static bool ProcessSyncSample(double t1, double t2, double t3, double t4)
        {
            double remoteProcessingMs = t3 - t2;
            double networkRttMs = (t4 - t1) - remoteProcessingMs;
            if (!IsFinite(t1) || !IsFinite(t2) || !IsFinite(t3) || !IsFinite(t4) ||
                t4 < t1 || remoteProcessingMs < 0d ||
                remoteProcessingMs > MaximumRemoteProcessingMs || networkRttMs < 0d ||
                networkRttMs > MaximumAcceptedNetworkRttMs)
            {
                Debug.LogWarning(
                    $"[UDPClockSync] Rejected sample: networkRTT={networkRttMs:F2}ms, " +
                    $"remoteProcessing={remoteProcessingMs:F2}ms");
                return false;
            }

            double offset = ((t2 - t1) + (t3 - t4)) / 2d;
            bool becameReady = false;
            lock (s_syncLock)
            {
                s_samples.Add(new SyncSample(offset, networkRttMs));
                if (s_samples.Count > SampleWindowSize)
                {
                    s_samples.RemoveAt(0);
                }

                s_acceptedSampleCount++;
                bool wasReady = s_hasStableEstimate;
                RecalculateEstimateLocked();
                s_lastSyncLocalTimeMs = t4;
                becameReady = !wasReady && s_hasStableEstimate;
            }

            if (becameReady)
            {
                ClockSyncSnapshot snapshot = GetSnapshot();
                Debug.Log(
                    $"[UDPClockSync] Measurement ready: samples={snapshot.SampleCount}, " +
                    $"RTT={snapshot.RoundTripTimeMs:F2}ms, offset={snapshot.ClockDiff:F2}ms, " +
                    $"uncertainty<={snapshot.OffsetUncertaintyMs:F2}ms");
            }

            return true;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void RecalculateEstimateLocked()
        {
            if (s_samples.Count == 0)
            {
                return;
            }

            var ordered = new List<SyncSample>(s_samples);
            ordered.Sort((left, right) => left.NetworkRtt.CompareTo(right.NetworkRtt));
            int selectedCount = Math.Min(SelectedLowRttSamples, ordered.Count);
            var offsets = new List<double>(selectedCount);
            var roundTrips = new List<double>(selectedCount);
            for (int index = 0; index < selectedCount; index++)
            {
                offsets.Add(ordered[index].Offset);
                roundTrips.Add(ordered[index].NetworkRtt);
            }

            offsets.Sort();
            roundTrips.Sort();
            s_clockDiff = Median(offsets);
            s_lastRtt = Median(roundTrips);
            s_offsetUncertaintyMs = roundTrips[roundTrips.Count - 1] / 2d;
            s_hasStableEstimate = s_samples.Count >= MinimumReadySamples;
        }

        private static double Median(List<double> values)
        {
            int middle = values.Count / 2;
            return values.Count % 2 == 0
                ? (values[middle - 1] + values[middle]) / 2d
                : values[middle];
        }

        private static void ResetEstimateLocked()
        {
            s_samples.Clear();
            s_clockDiff = 0d;
            s_lastRtt = -1d;
            s_offsetUncertaintyMs = -1d;
            s_lastSyncLocalTimeMs = 0d;
            s_acceptedSampleCount = 0;
            s_hasStableEstimate = false;
            s_pendingRequests.Clear();
        }

        private static void SendUDP(string msg)
        {
            var udp = MetaQuestProEyeGazeUDP.UDPManager.Instance;
            if (udp != null && udp.udp != null && udp.udp.ConnectState())
            {
                udp.udp.SocketSend(msg);
            }
            else
            {
                Debug.LogWarning("[UDPClockSync] UDPManager is not initialized or connected.");
            }
        }

        [Serializable]
        private class ClockSyncRequest
        {
            public string cmd;
            public int sequence;
            public double t1;
            public double clockDiff;
            public bool syncValid;
            public double syncRttMs;
            public double offsetUncertaintyMs;
            public int sampleCount;
        }

        [Serializable]
        private class ClockSyncResponse
        {
            public string cmd;
            public int sequence;
            public double t1;
            public double t2;
            public double t3;
        }

        #region Test support

        public static void SetClockDiffForTest(double diff, bool synced = true)
        {
            lock (s_syncLock)
            {
                ResetEstimateLocked();
                s_clockDiff = diff;
                s_lastRtt = synced ? 10d : -1d;
                s_offsetUncertaintyMs = synced ? 5d : -1d;
                s_acceptedSampleCount = synced ? MinimumReadySamples : 0;
                s_hasStableEstimate = synced;
                s_lastSyncLocalTimeMs = synced ? GetLocalUnixTimeMsD() : 0d;
            }
        }

        public static bool AddFourTimestampSampleForTest(double t1, double t2, double t3, double t4)
        {
            return ProcessSyncSample(t1, t2, t3, t4);
        }

        public static void ResetForTest()
        {
            lock (s_syncLock)
            {
                ResetEstimateLocked();
            }
        }

        public static double TestGetLocalTimeMsD()
        {
            return GetLocalUnixTimeMsD();
        }

        #endregion
    }
}
