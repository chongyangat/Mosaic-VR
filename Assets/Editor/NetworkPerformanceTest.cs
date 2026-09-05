using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using GameMain;
using Mirror;

/// <summary>
/// Editor tests for the network measurement chain. Directional latency is only
/// valid after a stable four-timestamp clock estimate; RTT does not require
/// clock synchronization.
/// </summary>
public class NetworkPerformanceTest
{
    [SetUp]
    public void SetUp()
    {
        UDPClockSync.ResetForTest();
    }

    [Test]
    [Description("Four timestamps exclude Quest response-processing time from network RTT")]
    public void FourTimestamp_RemovesRemoteProcessingTime()
    {
        double t1 = UDPClockSync.TestGetLocalTimeMsD();
        const double offsetMs = 5000d;
        const double forwardMs = 5d;
        const double questProcessingMs = 25d;
        const double reverseMs = 5d;

        bool accepted = UDPClockSync.AddFourTimestampSampleForTest(
            t1,
            t1 + forwardMs + offsetMs,
            t1 + forwardMs + offsetMs + questProcessingMs,
            t1 + forwardMs + questProcessingMs + reverseMs);

        Assert.IsTrue(accepted);
        UDPClockSync.ClockSyncSnapshot snapshot = UDPClockSync.GetSnapshot();
        Assert.AreEqual(offsetMs, snapshot.ClockDiff, 0.01d);
        Assert.AreEqual(forwardMs + reverseMs, snapshot.RoundTripTimeMs, 0.01d);
        Assert.IsFalse(snapshot.IsReady, "One sample must not make directional latency valid.");
        Assert.AreEqual("Collecting", snapshot.State);
    }

    [Test]
    [Description("A stable estimate needs five samples and selects the lowest-RTT observations")]
    public void FourTimestamp_LowRttBatchProducesStableEstimate()
    {
        double baseTime = UDPClockSync.TestGetLocalTimeMsD();

        for (int index = 0; index < 5; index++)
        {
            AddSample(baseTime + index * 50d, 6000d + index * 0.1d, 4d, 2d, 4d);
        }

        UDPClockSync.ClockSyncSnapshot ready = UDPClockSync.GetSnapshot();
        Assert.IsTrue(ready.IsReady);
        Assert.AreEqual("Ready", ready.State);
        Assert.AreEqual(6000.2d, ready.ClockDiff, 0.11d);
        Assert.AreEqual(8d, ready.RoundTripTimeMs, 0.01d);
        Assert.AreEqual(4d, ready.OffsetUncertaintyMs, 0.01d);
        Assert.AreEqual(5, ready.SampleCount);

        // Large asymmetric paths create biased offset estimates, but their high
        // RTT keeps them out of the selected low-RTT set.
        AddSample(baseTime + 500d, 7200d, 100d, 5d, 500d);
        AddSample(baseTime + 600d, 4200d, 450d, 5d, 150d);

        UDPClockSync.ClockSyncSnapshot filtered = UDPClockSync.GetSnapshot();
        Assert.IsTrue(filtered.IsReady);
        Assert.AreEqual(6000.2d, filtered.ClockDiff, 0.11d);
        Assert.AreEqual(8d, filtered.RoundTripTimeMs, 0.01d);
        Assert.AreEqual(7, filtered.SampleCount);
    }

    [Test]
    [Description("Impossible timestamp ordering is rejected and cannot unlock measurements")]
    public void FourTimestamp_InvalidSampleIsRejected()
    {
        double now = UDPClockSync.TestGetLocalTimeMsD();
        Assert.IsFalse(UDPClockSync.AddFourTimestampSampleForTest(now, now + 10d, now + 9d, now + 20d));
        Assert.IsFalse(UDPClockSync.AddFourTimestampSampleForTest(now, now + 10d, now + 11d, now - 1d));

        UDPClockSync.ClockSyncSnapshot snapshot = UDPClockSync.GetSnapshot();
        Assert.IsFalse(snapshot.IsReady);
        Assert.AreEqual(0, snapshot.SampleCount);
        Assert.AreEqual("Unsynced", snapshot.State);
    }

    [Test]
    [Description("UDP receive, clock sync and gaze sender use the same monotonic Unix clock")]
    public void SharedUdpClock_HasNoIndependentAnchorBias()
    {
        double udpTime = MetaQuestProEyeGazeUDP.UDP.GetHighPrecisionUnixTimeMsD();
        double syncTime = UDPClockSync.TestGetLocalTimeMsD();
        Assert.Less(Math.Abs(syncTime - udpTime), 2d);
    }

    [Test]
    [Description("Unsynchronized cross-device delay is invalid instead of guessed")]
    public void DirectionalDelay_UnsynchronizedReturnsInvalid()
    {
        GameObject monitorObject = new GameObject("NetworkPerformanceMonitor_UnsyncedTest");
        NetworkPerformanceMonitor monitor = monitorObject.AddComponent<NetworkPerformanceMonitor>();

        try
        {
            MethodInfo calculateMethod = typeof(NetworkPerformanceMonitor).GetMethod(
                "CalculateFrameArrivalDelay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(calculateMethod);

            double arrival = UDPClockSync.TestGetLocalTimeMsD();
            object result = calculateMethod.Invoke(
                monitor,
                new object[] { (ulong)(arrival - 5d), arrival, true, null });

            Assert.AreEqual(-1f, (float)result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(monitorObject);
        }
    }

    [Test]
    [Description("Recording flushes all rows and exposes measurement/synchronization quality columns")]
    public void Recording_StopFlushesPendingSamplesAndWritesQualityColumns()
    {
        string testDir = Path.Combine(Application.dataPath, "..", "Temp", "NetworkPerfRecordingTest");
        Directory.CreateDirectory(testDir);
        string csvPath = Path.Combine(testDir, "recording_flush.csv");

        GameObject monitorObject = new GameObject("NetworkPerformanceMonitor_RecordingTest");
        NetworkPerformanceMonitor monitor = monitorObject.AddComponent<NetworkPerformanceMonitor>();

        try
        {
            Assert.IsTrue(monitor.StartRecording(csvPath));
            MethodInfo writeMethod = typeof(NetworkPerformanceMonitor).GetMethod(
                "WriteToRecordingFile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(writeMethod);

            for (int index = 0; index < 32; index++)
            {
                var sample = new NetworkPerformanceMonitor.PerformanceData
                {
                    sampleTimestamp = "2026-08-14 12:00:00.000",
                    sentTimestamp = 0UL,
                    link = "EyeTracking→PC",
                    latency = -1f,
                    jitter = 1f,
                    packetLoss = 0f,
                    latencyMin = -1f,
                    latencyMax = -1f,
                    latencyMedian = -1f,
                    latency95th = -1f,
                    latency99th = -1f,
                    frameArrivalDelay = -1f,
                    frameInterval = 16f,
                    frameLossRate = 0f,
                    sequenceNumber = (uint)(index + 1),
                    measurementType = "Estimated VR->PC one-way delay",
                    syncValid = false,
                    syncState = "Collecting",
                    clockOffsetMs = 0d,
                    syncRttMs = -1d,
                    syncAgeMs = -1d,
                    offsetUncertaintyMs = -1d,
                    syncSampleCount = 3
                };

                writeMethod.Invoke(monitor, new object[] { sample });
            }

            Assert.AreEqual(csvPath, monitor.StopRecording());
            string[] lines = File.ReadAllLines(csvPath);
            Assert.AreEqual(33, lines.Length);

            string[] header = lines[0].Split(',');
            string[] firstRow = lines[1].Split(',');
            Assert.AreEqual(23, header.Length);
            Assert.AreEqual(header.Length, firstRow.Length);
            Assert.IsEmpty(firstRow[2], "An unsynchronized sender time must be blank.");
            Assert.AreEqual("Estimated VR->PC one-way delay", firstRow[15]);
            Assert.AreEqual("false", firstRow[16]);
            Assert.AreEqual("Collecting", firstRow[17]);
            Assert.AreEqual("3", firstRow[22]);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(monitorObject);
        }
    }

    [Test]
    [Description("PC/VR state-sync sampling follows Mirror's configured send rate")]
    public void StateSyncSampling_UsesMirrorSendInterval()
    {
        int originalTickRate = NetworkServer.tickRate;

        try
        {
            NetworkServer.tickRate = 60;
            Assert.AreEqual(1f / 60f, NetworkPerformanceMonitor.StateSyncSampleInterval, 0.000001f);

            NetworkServer.tickRate = 30;
            Assert.AreEqual(1f / 30f, NetworkPerformanceMonitor.StateSyncSampleInterval, 0.000001f);
        }
        finally
        {
            NetworkServer.tickRate = originalTickRate;
        }
    }

    [Test]
    [Description("Resetting statistics preserves an active RTT link and permits sampling")]
    public void Recording_ActiveRttLinkProducesCsvRowAfterStatisticsReset()
    {
        const string linkName = "PC<->VR 状态同步";
        string testDir = Path.Combine(Application.dataPath, "..", "Temp", "NetworkPerfRecordingTest");
        Directory.CreateDirectory(testDir);
        string csvPath = Path.Combine(testDir, "active_link.csv");

        GameObject monitorObject = new GameObject("NetworkPerformanceMonitor_ActiveLinkTest");
        NetworkPerformanceMonitor monitor = monitorObject.AddComponent<NetworkPerformanceMonitor>();

        try
        {
            monitor.UpdateLinkActivity(linkName);
            monitor.ResetStatistics();
            Assert.IsTrue(monitor.IsLinkActive(linkName));

            Assert.IsTrue(monitor.StartRecording(csvPath));
            monitor.SamplePerformanceData(linkName);
            monitor.StopRecording();

            string[] lines = File.ReadAllLines(csvPath);
            Assert.AreEqual(2, lines.Length);
            StringAssert.Contains(linkName, lines[1]);
            StringAssert.Contains("Mirror RTT", lines[1]);
            StringAssert.Contains("Not required", lines[1]);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(monitorObject);
        }
    }

    private static void AddSample(
        double t1,
        double offset,
        double forwardDelay,
        double remoteProcessing,
        double reverseDelay)
    {
        double t2 = t1 + forwardDelay + offset;
        double t3 = t2 + remoteProcessing;
        double t4 = t1 + forwardDelay + remoteProcessing + reverseDelay;
        Assert.IsTrue(UDPClockSync.AddFourTimestampSampleForTest(t1, t2, t3, t4));
    }

    [TearDown]
    public void TearDown()
    {
        UDPClockSync.ResetForTest();
    }
}
