using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run only in an isolated validation project: this harness changes PlayerSettings.
[InitializeOnLoad]
public static class NodeVisitFallbackChecks
{
    private const string RunningKey = "NodeVisitFallbackChecks.Running";
    private const string ExitKey = "NodeVisitFallbackChecks.Exit";
    [Serializable] private sealed class Report
    {
        public int passed, failed;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
    }
    private static Report report;

    static NodeVisitFallbackChecks() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

    public static void RunBatch()
    {
        PlayerSettings.companyName = "CodexIsolatedValidation";
        PlayerSettings.productName = "InspectorNodeFallbackChecks";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(ExitKey, 2);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall += Start;
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(RunningKey, false);
            EditorApplication.Exit(SessionState.GetInt(ExitKey, 2));
        }
    }

    private static void Start()
    {
        var runner = new GameObject("NodeVisitFallbackChecksRunner").AddComponent<Runner>();
        UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(runner.Run());
    }

    private sealed class Runner : MonoBehaviour
    {
        public IEnumerator Run()
        {
            report = new Report();
            Application.logMessageReceived += Capture;
            var routine = Execute();
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception e) { report.errors.Add(e.ToString()); break; }
                if (!next) break;
                yield return routine.Current;
            }
            Application.logMessageReceived -= Capture;
            string path = Environment.GetEnvironmentVariable("INSPECTOR_NODE_FALLBACK_RESULT");
            if (string.IsNullOrEmpty(path)) path = Path.GetFullPath("node-fallback-result.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            SessionState.SetInt(ExitKey, report.failed == 0 && report.errors.Count == 0 ? 0 : 1);
            Debug.Log($"[NodeVisitFallbackChecks] {report.passed} passed, {report.failed} failed, {report.errors.Count} errors");
            EditorApplication.ExitPlaymode();
        }
    }

    private static void Capture(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.errors.Add(message);
    }
    private static void Check(string name, bool passed)
    {
        if (passed) report.passed++; else report.failed++;
        report.checks.Add((passed ? "PASS " : "FAIL ") + name);
    }
    private static FieldInfo Field(string name) => typeof(NodeSystem).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Set(NodeSystem node, string name, object value) => Field(name).SetValue(node, value);
    private static object Get(NodeSystem node, string name) => Field(name).GetValue(node);
    private static void Call(NodeSystem node, string name, params object[] args) =>
        typeof(NodeSystem).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(node, args);

    private static IEnumerator Execute()
    {
        // Without a Canvas, event creation must fail without locking travel.
        var emptyHost = new GameObject("MissingCanvasNode");
        emptyHost.SetActive(false);
        var emptyNode = emptyHost.AddComponent<NodeSystem>();
        Set(emptyNode, "nodeRows", new List<NodeSystem.NodeRow>());
        Set(emptyNode, "currentRowIndex", 1);
        Set(emptyNode, "_visitInProgress", true);
        Check("event test has no Canvas", UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include) == null);
        Call(emptyNode, "OpenEventFromNode");
        Check("event creation failure releases visit", !(bool)Get(emptyNode, "_visitInProgress"));
        UnityEngine.Object.DestroyImmediate(emptyHost);

        yield return SceneManager.LoadSceneAsync("GamePlayScene");
        for (int i = 0; i < 3; i++) yield return null;
        Check("new run starts", RunSessionManager.Instance != null && RunSessionManager.Instance.StartNewRun());
        var node = NodeSystem.Current;
        Check("actual gameplay node exists", node != null);
        foreach (var spec in new[] {
            new[] { "mercenaryOfficePanel", "OpenMercenaryFromNode" },
            new[] { "churchPanel", "OpenChurchFromNode" } })
        {
            object original = Get(node, spec[0]);
            Check(spec[0] + " is connected in production scene", original != null);
            Set(node, spec[0], null);
            Set(node, "currentRowIndex", 2);
            Set(node, "_visitInProgress", true);
            Call(node, spec[1]);
            Check(spec[0] + " missing releases route selection", node.CanSelectRoute);
            Set(node, spec[0], original);
        }

        Check("restPanel is connected in production scene", Get(node, "restPanel") != null);
        Set(node, "restPanel", null);
        Set(node, "currentRowIndex", MapGenerator.RestFloor);
        Set(node, "_visitInProgress", true);
        Call(node, "DispatchByRoomType", RoomType.Rest);
        Check("missing rest panel finishes visit", !(bool)Get(node, "_visitInProgress"));
        float deadline = Time.realtimeSinceStartup + 4f;
        while (node.CurrentRoomType != RoomType.Boss && Time.realtimeSinceStartup < deadline) yield return null;
        Check("missing rest panel still enters floor 10 boss", node.CurrentRoomType == RoomType.Boss
            && (int)Get(node, "currentRowIndex") == MapGenerator.BossFloor);
    }
}
