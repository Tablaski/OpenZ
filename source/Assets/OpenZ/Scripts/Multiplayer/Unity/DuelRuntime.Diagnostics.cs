using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Stopwatch = System.Diagnostics.Stopwatch;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelRuntime
    {
        internal const int DefaultSnapshotHz = 15, DefaultReceiveBudgetMs = 3;
        internal const int DiagnosticHistorySize = 120;
        const string ReportHeader = "OpenZ-VK duel diagnostics v1";
        const int DefaultInterpolationDelayMs = 100, ReportMaxBytes = 96 * 1024, ReportLogLines = 36;
        static readonly UTF8Encoding ReportEncoding = new UTF8Encoding(false, true);
        internal struct DiagnosticSample
        {
            public float MatchTime, Frame, Main, Gpu, Ping, StateCost, Rate, Gap, Wait, QueueKiB, Fps;
            public int TargetHz;
        }
        internal int SnapshotHz { get; private set; } = DefaultSnapshotHz;
        internal int ReceiveBudgetMs { get; private set; } = DefaultReceiveBudgetMs;
        internal int InterpolationMs { get; private set; }
        internal int InterpolationDelayMs { get; private set; } = DefaultInterpolationDelayMs;
        internal string TransportLabel => OnlinePeer != null ? (OnlinePeer.UsesRelay ? "TURN" : "DIRECT") : "TCP";
        internal int DiagnosticCount { get; private set; }
        internal int DiagnosticRevision { get; private set; }
        internal bool HasPeerDiagnosticReport => peerDiagnosticReport != null;
        readonly DiagnosticSample[] diagnosticHistory = new DiagnosticSample[DiagnosticHistorySize];
        DiagnosticSample diagnosticPending;
        int diagnosticNext, diagnosticStates, diagnosticFrames;
        double diagnosticStart;
        long diagnosticLastState;
        readonly DuelMotionBuffer motionHistory = new DuelMotionBuffer();
        int motionSelectedFrame = -1;
        readonly Queue<string> diagnosticSummaries = new Queue<string>(ReportLogLines);
        string peerDiagnosticReport;
        double nextReportSend, nextReportReceive;

        internal DiagnosticSample GetDiagnostic(int index)
            => diagnosticHistory[(diagnosticNext + DiagnosticHistorySize - DiagnosticCount + index) % DiagnosticHistorySize];

        void ResetDiagnostics()
        {
            InterpolationMs = 0;
            InterpolationDelayMs = DefaultInterpolationDelayMs;
            SnapshotHz = DefaultSnapshotHz;
            ReceiveBudgetMs = DefaultReceiveBudgetMs;
            motionHistory.Reset();
            motionSelectedFrame = -1;
            DiagnosticCount = diagnosticNext = diagnosticStates = diagnosticFrames = 0;
            DiagnosticRevision++;
            diagnosticPending = default;
            diagnosticLastState = 0;
            diagnosticStart = Now;
            diagnosticSummaries.Clear();
            peerDiagnosticReport = null;
            nextReportSend = nextReportReceive = 0;
        }
        internal void SetDiagnosticDelay(int delayMs)
        {
            if (!Playing || IsHost) return;
            InterpolationDelayMs = Mathf.Clamp(delayMs, 70, 220);
            if (InterpolationMs != 0) SetDiagnosticTuning(SnapshotHz, ReceiveBudgetMs, InterpolationDelayMs);
        }
        internal void ResetDiagnosticTuning()
        {
            InterpolationDelayMs = DefaultInterpolationDelayMs;
            SetDiagnosticTuning(DefaultSnapshotHz, DefaultReceiveBudgetMs, 0);
        }
        internal void SetDiagnosticTuning(int snapshotHz, int receiveBudgetMs, int interpolationMs)
        {
            if (!Playing) return;
            if (IsHost) SnapshotHz = Mathf.Clamp(snapshotHz, 8, 20);
            else
            {
                interpolationMs = Mathf.Clamp(interpolationMs, 0, 220);
                if (InterpolationMs == 0 || interpolationMs == 0) motionHistory.Reset();
                InterpolationMs = interpolationMs;
            }
            ReceiveBudgetMs = Mathf.Clamp(receiveBudgetMs, 1, 8);
            string line = FormattableString.Invariant(
                $"[OpenZ Multiplayer Perf] tuning matchS={Now - perfMatchStart:F2} role={(IsHost ? "host" : "guest")} snapshotTargetHz={SnapshotHz} receiveBudgetMs={ReceiveBudgetMs} interpolationMs={InterpolationMs}");
            Debug.Log(line);
            RememberPerformanceLine(line);
        }
        void RecordDiagnosticFrame(float frameMs)
        {
            diagnosticFrames++;
            diagnosticPending.Frame = Math.Max(diagnosticPending.Frame, frameMs);
            diagnosticPending.Main = Math.Max(diagnosticPending.Main, (float)OzFrameTimes.Main);
            diagnosticPending.Gpu = Math.Max(diagnosticPending.Gpu, (float)OzFrameTimes.Gpu);
            double now = Now, elapsed = now - diagnosticStart;
            if (elapsed < .25) return;
            diagnosticPending.MatchTime = (float)(now - perfMatchStart);
            diagnosticPending.Ping = PingMs;
            diagnosticPending.Rate = (float)(diagnosticStates / elapsed);
            diagnosticPending.Fps = (float)(diagnosticFrames / elapsed);
            diagnosticPending.TargetHz = IsHost ? SnapshotHz : 0;
            diagnosticHistory[diagnosticNext] = diagnosticPending;
            diagnosticNext = (diagnosticNext + 1) % DiagnosticHistorySize;
            DiagnosticCount = Math.Min(DiagnosticCount + 1, DiagnosticHistorySize);
            DiagnosticRevision++;
            diagnosticPending = default;
            diagnosticStates = diagnosticFrames = 0;
            diagnosticStart = now;
        }
        void RecordDiagnosticState(long stamp, long elapsed, long queueWait)
        {
            diagnosticStates++;
            diagnosticPending.StateCost = Math.Max(diagnosticPending.StateCost, (float)(elapsed * PerfTickMs));
            diagnosticPending.Wait = Math.Max(diagnosticPending.Wait, (float)(queueWait * PerfTickMs));
            if (diagnosticLastState != 0)
                diagnosticPending.Gap = Math.Max(diagnosticPending.Gap, (float)((stamp - diagnosticLastState) * PerfTickMs));
            diagnosticLastState = stamp;
        }
        void RecordDiagnosticQueue(int queuedBytes)
            => diagnosticPending.QueueKiB = Math.Max(diagnosticPending.QueueKiB, queuedBytes / 1024f);

        void RememberPerformanceLine(string line)
        {
            if (diagnosticSummaries.Count == ReportLogLines) diagnosticSummaries.Dequeue();
            diagnosticSummaries.Enqueue(line);
        }
        internal string BuildDiagnosticReport()
        {
            var report = new StringBuilder();
            report.Append(ReportHeader).Append('\n');
            report.AppendFormat(CultureInfo.InvariantCulture,
                "utc={0:O} role={1} map={2} transport={3} matchS={4:F1} build={5}\n",
                DateTime.UtcNow, IsHost ? "host" : "guest", View?.Map ?? "unknown", TransportLabel, Now - perfMatchStart, LocalRelease);
            foreach (var line in diagnosticSummaries) report.Append(line).Append('\n');
            string partial = BuildPerformanceLine("report");
            if (partial != null) report.Append(partial).Append('\n');
            report.Append("matchS\tfps\tframePeakMs\tmainMs\tgpuMs\tpingMs\tstateCpuMs\tstateHz\tgapMs\tqueueWaitMs\tqueueKiB\ttargetHz\n");
            for (int i = 0; i < DiagnosticCount; i++)
            {
                var s = GetDiagnostic(i);
                report.AppendFormat(CultureInfo.InvariantCulture, "{0:F2}\t{1:F1}\t{2:F1}\t{3:F1}\t{4:F1}\t{5:F0}\t{6:F2}\t{7:F1}\t{8:F1}\t{9:F1}\t{10:F1}\t{11}\n",
                    s.MatchTime, s.Fps, s.Frame, s.Main, s.Gpu, s.Ping, s.StateCost, s.Rate, s.Gap, s.Wait, s.QueueKiB, s.TargetHz);
            }
            return report.ToString();
        }
        internal string SendDiagnosticReport()
        {
            if (!Playing || !Connected) return "The peer is no longer connected.";
            if (Now < nextReportSend) return "Wait a few seconds before sending another report.";
            if (QueuedSendBytes >= 256 * 1024) return "The connection is busy. Try again when the send queue clears.";
            var bytes = ReportEncoding.GetBytes(BuildDiagnosticReport());
            if (bytes.Length > ReportMaxBytes) return "The report is too large to send. Use Copy Mine.";
            if (!peer.Send(new DuelPacket(DuelMessage.DiagnosticsReport, bytes))) return "The report could not be queued.";
            nextReportSend = Now + 5;
            return "Local report queued for your opponent.";
        }
        void ReceiveDiagnosticReport(byte[] bytes)
        {
            if (!Playing || Now < nextReportReceive || bytes.Length > ReportMaxBytes) return;
            nextReportReceive = Now + 4;
            string text;
            try { text = ReportEncoding.GetString(bytes); }
            catch (DecoderFallbackException) { return; }
            if (!text.StartsWith(ReportHeader + "\n", StringComparison.Ordinal)) return;
            foreach (char c in text)
                if (char.IsControl(c) && c != '\n' && c != '\t') return;
            peerDiagnosticReport = text;
        }
        internal string SavePeerDiagnosticReport()
        {
            if (peerDiagnosticReport == null) return "Ask your opponent to select Send Mine to Peer.";
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "OpenZ-VK", "diagnostics");
                Directory.CreateDirectory(folder);
                string file = "peer-duel-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture) + ".txt";
                File.WriteAllText(Path.Combine(folder, file), peerDiagnosticReport, ReportEncoding);
                return "Saved in OpenZ-VK/diagnostics/" + file;
            }
            catch (IOException) { return "The report could not be written to the diagnostics folder."; }
            catch (UnauthorizedAccessException) { return "The diagnostics folder is not writable."; }
        }

        static bool CanInterpolate(ZObject unit)
            => !unit.Destroyed && (unit.Kind == ZKind.Robot || unit.Kind == ZKind.Vehicle);
        static int PresentationIdentity(ZObject unit) => (int)unit.Kind + 4 * (int)unit.Owner;

        void CaptureMotion(long arrival)
        {
            if (InterpolationMs == 0 || !Game) return;
            long began = Stopwatch.GetTimestamp();
            var world = Game.World;
            double localArrival = (arrival > 0 ? arrival : began) / (double)Stopwatch.Frequency;
            if (motionHistory.BeginSnapshot(world.Time, localArrival))
                foreach (var unit in world.Objects)
                    if (CanInterpolate(unit))
                        motionHistory.AddUnit(unit.Id, PresentationIdentity(unit), (float)unit.PosX, (float)unit.PosY, (float)unit.MoveSpeed);
            perf.MotionCaptureMaxTicks = Math.Max(perf.MotionCaptureMaxTicks, Stopwatch.GetTimestamp() - began);
        }
        internal static bool TryVisualPosition(ZGame game, ZObject unit, out Vector3 position)
        {
            position = default;
            var runtime = I;
            if (!runtime || runtime.IsHost || runtime.InterpolationMs == 0 || !runtime.Playing ||
                runtime.Game != game || !CanInterpolate(unit)) return false;
            if (runtime.motionSelectedFrame != Time.frameCount)
            {
                runtime.motionSelectedFrame = Time.frameCount;
                runtime.motionHistory.Select(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, runtime.InterpolationMs / 1000d);
                if (runtime.motionHistory.Starved) runtime.perf.MotionHoldFrames++;
            }
            if (!runtime.motionHistory.TryPosition(unit.Id, PresentationIdentity(unit), (float)unit.PosX, (float)unit.PosY, out float x, out float y))
                return false;
            position = ZGame.PxToWorld(x + unit.WidthPix * .5f, y + unit.HeightPix * .5f);
            return true;
        }
    }
}
