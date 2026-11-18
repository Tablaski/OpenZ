using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ.Multiplayer
{
    public sealed partial class DuelMenus
    {
        internal void ShowDiagnosticsProof() { page = Page.Diagnostics; dirty = true; }
        static readonly int[] SnapshotRates = { 8, 10, 12, 15, 18, 20 };
        static readonly int[] ReceiveBudgets = { 1, 2, 3, 5, 8 };
        static readonly int[] InterpolationDelays = { 70, 100, 130, 170, 220 };
        readonly DuelDiagnosticGraph[] diagnosticGraphs = new DuelDiagnosticGraph[4];
        OzText diagnosticsSummary, diagnosticsStatus, snapshotValue, budgetValue, interpolationValue, delayValue;
        OzText binaryEventsValue, compressionValue;
        int diagnosticRevision;
        string diagnosticAction = "";

        static bool DiagnosticsGesture()
        {
            var keys = Keyboard.current;
            if (!Application.isFocused || Mouse.current == null || !keys.ctrlKey.isPressed ||
                !keys.altKey.isPressed || !keys.upArrowKey.wasPressedThisFrame) return false;
            Vector2 pointer = Mouse.current.position.ReadValue();
            return pointer.x >= 0 && pointer.x < Screen.width * .1f &&
                pointer.y <= Screen.height && pointer.y > Screen.height * .88f;
        }
        void BuildDiagnosticsPanel()
        {
            snapshotValue = budgetValue = interpolationValue = delayValue = binaryEventsValue = compressionValue = null;
            var dim = Hud.Img(root, "Diagnostics backdrop", new Color32(12, 9, 16, 112));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            var panel = Panel(OzForkOptions.DuelDiagnosticsLabel, 0, 0, 1392, 912);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            Rect canvasRect = ((RectTransform)canvas.transform).rect;
            float scale = Mathf.Min(1, (canvasRect.width - 16) / 1392f, (canvasRect.height - 16) / 912f);
            if (scale > 0) panel.localScale = new Vector3(scale, scale, 1);
            Button(panel, "CLOSE  [ESC]", 1196, 14, 172, CloseOverlay, height: 37);
            diagnosticsSummary = Label(panel, "", 24, 58, 1328, 19, OzUi.Ink);
            string allowance = R.RelayRemainingBytes is long bytes ? $"{bytes / 1e9:0.###} GB" : "Unavailable";
            Label(panel, "TURN REMAINING AT CONNECTION · " + allowance, 704, 546, 500, 16, OzUi.Muted);

            AddDiagnosticGraph(panel, 0, 24, 98, "FRAME TIME", "Frame peak", "Main thread", "GPU", "ms");
            AddDiagnosticGraph(panel, 1, 704, 98, "NETWORK TIMING", "Ping", R.IsHost ? "Send gap" : "Arrival gap", R.IsHost ? null : "Queue wait", "ms");
            AddDiagnosticGraph(panel, 2, 24, 318, "STATE PROCESSING", R.IsHost ? "Send CPU peak" : "Receive CPU peak", null, null, "ms");
            AddDiagnosticGraph(panel, 3, 704, 318, "STATE CADENCE", R.IsHost ? "Sent per second" : "Received per second", R.IsHost ? "Target" : null, null, "Hz");

            Label(panel, "LIVE TUNING", 24, 546, 500, 18, OzForkOptions.Accent);
            Label(panel, "Applies immediately · resets at the next match", 24, 575, 958, 16, OzUi.Muted);
            if (R.IsHost)
            {
                snapshotValue = DiagnosticControl(panel, OzForkOptions.DuelSnapshotRateLabel, 24, () =>
                    R.SetDiagnosticTuning(Next(SnapshotRates, R.SnapshotHz), R.ReceiveBudgetMs, 0));
                Label(panel, "Host setting · affects both sides", 24, 694, 325, 15, OzUi.Muted);
                binaryEventsValue = DiagnosticControl(panel, OzForkOptions.DuelBinaryEventsLabel, 704,
                    () => R.SetStatePacking(!R.BinaryStateEvents, R.CompressStateUpdates), "TOGGLE");
                compressionValue = DiagnosticControl(panel, OzForkOptions.DuelCompressionLabel, 980,
                    () => R.SetStatePacking(R.BinaryStateEvents, !R.CompressStateUpdates), "TOGGLE");
                Label(panel, "Applied to both players' game updates", 704, 694, 445, 15, OzUi.Muted);
            }
            else
            {
                interpolationValue = DiagnosticControl(panel, OzForkOptions.DuelInterpolationLabel, 24, () =>
                    R.SetDiagnosticTuning(R.SnapshotHz, R.ReceiveBudgetMs, R.InterpolationMs == 0 ? R.InterpolationDelayMs : 0), "TOGGLE");
                delayValue = DiagnosticControl(panel, OzForkOptions.DuelInterpolationDelayLabel, 704,
                    () => R.SetDiagnosticDelay(Next(InterpolationDelays, R.InterpolationDelayMs)));
                Label(panel, "Local unit positions", 24, 694, 325, 15, OzUi.Muted);
                Label(panel, "Higher values tolerate longer arrival gaps", 704, 694, 468, 15, OzUi.Muted);
            }
            budgetValue = DiagnosticControl(panel, OzForkOptions.DuelReceiveBudgetLabel, 364, () =>
                R.SetDiagnosticTuning(R.SnapshotHz, Next(ReceiveBudgets, R.ReceiveBudgetMs), R.InterpolationMs));
            Label(panel, "Local processing limit between packets", 364, 694, 336, 15, OzUi.Muted);
            Button(panel, "RESET TUNING", 1196, 700, 172, R.ResetDiagnosticTuning, height: 37);

            Label(panel, "DUEL REPORTS", 24, 750, 500, 18, OzForkOptions.Accent);
            Button(panel, "COPY MINE", 24, 784, 202, () =>
            {
                GUIUtility.systemCopyBuffer = R.BuildDiagnosticReport();
                diagnosticAction = "Local report copied.";
            });
            Button(panel, "SEND MINE TO PEER", 236, 784, 248, () => diagnosticAction = R.SendDiagnosticReport());
            Button(panel, "SAVE PEER REPORT", 494, 784, 232, () => diagnosticAction = R.SavePeerDiagnosticReport());
            diagnosticsStatus = Label(panel, "", 24, 850, 1330, 16, OzUi.Ink);
            diagnosticAction = "";
            diagnosticRevision = -1;
        }
        OzText DiagnosticControl(RectTransform panel, string caption, int x, Action change, string actionLabel = "CHANGE")
        {
            Label(panel, caption, x, 604, 315, 17, OzForkOptions.Accent);
            Button(panel, actionLabel, x + 116, 632, 130, change, height: 42);
            return Label(panel, "", x, 639, 110, 20, OzUi.Ink);
        }
        static int Next(int[] values, int current) => values[(Array.IndexOf(values, current) + 1) % values.Length];

        void AddDiagnosticGraph(RectTransform panel, int index, int x, int y, string title,
            string first, string second, string third, string unit)
        {
            var card = Hud.Img(panel, title, new Color32(21, 17, 27, 245));
            card.rectTransform.TL(x, y, 664, 204);
            card.raycastTarget = false;
            Label(card.transform, title, 14, 12, 384, 16, OzUi.Ink);
            var axis = Label(card.transform, "", 506, 12, 142, 14, OzUi.Muted);
            Label(card.transform, first, 14, 178, 150, 14, DuelDiagnosticGraph.First);
            if (second != null) Label(card.transform, second, 170, 178, 150, 14, DuelDiagnosticGraph.Second);
            if (third != null) Label(card.transform, third, 324, 178, 154, 14, DuelDiagnosticGraph.Third);
            var marker = Label(card.transform, "", 492, 178, 152, 14, OzUi.Muted);
            var chart = Hud.Node("Plot", card.transform).TL(14, 41, 636, 122).gameObject.AddComponent<DuelDiagnosticGraph>();
            chart.Configure(R, index, axis, marker, unit);
            diagnosticGraphs[index] = chart;
        }
        void UpdateDiagnosticsPanel()
        {
            if (diagnosticsSummary == null || !diagnosticsSummary.gameObject || diagnosticRevision == R.DiagnosticRevision) return;
            diagnosticRevision = R.DiagnosticRevision;
            var sample = R.DiagnosticCount == 0 ? default : R.GetDiagnostic(R.DiagnosticCount - 1);
            foreach (var graph in diagnosticGraphs) graph.Redraw();
            diagnosticsSummary.Set($"{(R.IsHost ? "HOST" : "GUEST")}  ·  {R.TransportLabel}  ·  {sample.Fps:0} FPS  ·  {R.PingMs:0} ms PING  ·  STATE DATA {sample.PayloadKiBPerSecond:0.0} KiB/s · {R.MatchWireStateBytes / 1024.0:0.0} KiB TOTAL");
            snapshotValue?.Set($"{R.SnapshotHz} Hz");
            budgetValue?.Set($"{R.ReceiveBudgetMs} ms");
            interpolationValue?.Set(R.InterpolationMs == 0 ? "OFF" : "ON");
            delayValue?.Set($"{R.InterpolationDelayMs} ms");
            binaryEventsValue?.Set(R.BinaryStateEvents ? "ON" : "OFF");
            compressionValue?.Set(R.CompressStateUpdates ? "ON" : "OFF");
            string peerStatus = R.HasPeerDiagnosticReport ? "Peer report ready to save." : "";
            string status = diagnosticAction.Length > 0 ? diagnosticAction : "Send shares this duel's performance counters with your opponent.";
            diagnosticsStatus.Set(peerStatus.Length == 0 ? status : status + "  ·  " + peerStatus);
        }
    }

    internal sealed class DuelDiagnosticGraph : MaskableGraphic
    {
        internal static readonly Color32 First = new Color32(213, 112, 255, 255);
        internal static readonly Color32 Second = new Color32(103, 220, 200, 255);
        internal static readonly Color32 Third = new Color32(240, 178, 109, 255);
        static readonly Color32 Grid = new Color32(72, 60, 87, 150);
        DuelRuntime runtime;
        int kind, series;
        float peak;
        OzText axis, latest;
        string unit;

        internal void Configure(DuelRuntime source, int index, OzText axisLabel, OzText latestLabel, string units)
        {
            runtime = source; kind = index; axis = axisLabel; latest = latestLabel; unit = units;
            series = kind == 0 || (kind == 1 && !runtime.IsHost) ? 3 :
                kind == 1 || (kind == 3 && runtime.IsHost) ? 2 : 1;
            raycastTarget = false;
        }
        float Value(DuelRuntime.DiagnosticSample s, int line)
        {
            switch (kind)
            {
                case 0: return line == 0 ? s.Frame : line == 1 ? s.Main : s.Gpu;
                case 1: return line == 0 ? s.Ping : line == 1 ? s.Gap : s.Wait;
                case 2: return s.StateCost;
                default: return line == 0 ? s.Rate : s.TargetHz;
            }
        }
        internal void Redraw()
        {
            float largest = 0;
            int count = runtime.DiagnosticCount;
            for (int i = 0; i < count; i++)
                for (int line = 0; line < series; line++)
                    largest = Mathf.Max(largest, Value(runtime.GetDiagnostic(i), line));
            float step = kind == 1 ? 50 : kind == 0 ? 16.7f : 5;
            peak = Mathf.Max(step, Mathf.Ceil(largest / step) * step);
            axis.Set($"0–{peak:0.#} {unit}");
            latest.Set(count == 0 ? "WAITING…" : $"{Value(runtime.GetDiagnostic(count - 1), 0):0.0} {unit} NOW");
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!runtime || peak <= 0) return;
            var rect = rectTransform.rect;
            for (int i = 0; i <= 3; i++)
            {
                float y = rect.yMin + rect.height * i / 3;
                Line(mesh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), 1, Grid);
            }
            int count = runtime.DiagnosticCount;
            if (count == 0) return;
            float endTime = runtime.GetDiagnostic(count - 1).MatchTime;
            for (int line = 0; line < series; line++)
            {
                var tint = line == 0 ? First : line == 1 ? Second : Third;
                Vector2 previous = default;
                for (int i = 0; i < count; i++)
                {
                    var sample = runtime.GetDiagnostic(i);
                    float x = rect.xMax - rect.width * Mathf.Min(30, endTime - sample.MatchTime) / 30;
                    float y = rect.yMin + rect.height * Value(sample, line) / peak;
                    var point = new Vector2(x, y);
                    if (i > 0) Line(mesh, previous, point, 2, tint);
                    previous = point;
                }
            }
        }
        static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color32 tint)
        {
            Vector2 delta = b - a;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
            int i = mesh.currentVertCount;
            mesh.AddVert(a - normal, tint, Vector2.zero);
            mesh.AddVert(a + normal, tint, Vector2.zero);
            mesh.AddVert(b + normal, tint, Vector2.zero);
            mesh.AddVert(b - normal, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
            mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
