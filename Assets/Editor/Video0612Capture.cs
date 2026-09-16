using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>0612 영상 교체용 실제 플레이를 캡처한다. 씬과 저장값은 종료 후 복원한다.</summary>
[InitializeOnLoad]
public static class Video0612Capture
{
    public const string Output = "Temp/video-0612/capture";
    private const string ActiveKey = "Video0612Capture.Active";
    private static readonly string[] PreferenceKeys =
    {
        "SoulStone", "ManaStone", "language", "tutorial_completed",
        "SoulStone_preTutorial", "run_completed_count"
    };
    [Serializable] private sealed class Backup
    {
        public string scene;
        public bool[] present;
        public int[] values;
    }

    static Video0612Capture() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

    [MenuItem("Tools/Video 0612/Capture Updated Gameplay %&#F10")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before capture.");
        Directory.CreateDirectory(Output);
        var backup = new Backup
        {
            scene = SceneManager.GetActiveScene().path,
            present = PreferenceKeys.Select(PlayerPrefs.HasKey).ToArray(),
            values = PreferenceKeys.Select(key => PlayerPrefs.GetInt(key)).ToArray()
        };
        File.WriteAllText(Output + "/session-backup.json", JsonUtility.ToJson(backup, true));
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/GamePlayScene.unity", OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            new GameObject("0612 capture driver").AddComponent<Video0612CaptureDriver>();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            var backup = JsonUtility.FromJson<Backup>(File.ReadAllText(Output + "/session-backup.json"));
            for (int i = 0; i < PreferenceKeys.Length; i++)
            {
                if (backup.present[i]) PlayerPrefs.SetInt(PreferenceKeys[i], backup.values[i]);
                else PlayerPrefs.DeleteKey(PreferenceKeys[i]);
            }
            PlayerPrefs.Save();
            SessionState.SetBool(ActiveKey, false);
            if (!string.IsNullOrEmpty(backup.scene)) EditorSceneManager.OpenScene(backup.scene, OpenSceneMode.Single);
            Debug.Log("[Video0612] Original scene and saved preferences restored.");
        }
    }
}

public sealed class Video0612CaptureDriver : MonoBehaviour
{
    private RenderTexture _target;
    private Texture2D _pixels;
    private Camera _camera;
    private Process _encoder;
    private readonly StringBuilder _encoderLog = new StringBuilder();
    private readonly List<string> _events = new List<string>();
    private bool _recording;
    private int _frames;
    private string _clipName = "battle";
    private float VideoTime => _frames / 30f;

    private void OnEnable() { Application.logMessageReceived += OnLog; }
    private void OnLog(string message, string trace, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
        File.AppendAllText(Video0612Capture.Output + "/errors.txt", message + "\n" + trace + "\n");
        EditorApplication.isPlaying = false;
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        Time.captureFramerate = 0;
        UnityEngine.Random.InitState(161206);
        RunSessionManager.MarkOpeningCompleted();
        RunSessionManager.MarkCombatGuideCompleted();
        yield return new WaitForSeconds(1f);
        LocalizationManager.SetLanguage(Language.English);
        SoulstoneManager.Instance?.SetAmount(20);
        ManastoneManager.Instance?.SetAmount(0);
        var node = NodeSystem.Current;
        var roomAt = typeof(NodeSystem).GetMethod("GetRoomTypeAt", BindingFlags.Instance | BindingFlags.NonPublic);
        int combatColumn = Enumerable.Range(0, 3).First(column =>
            (RoomType)roomAt.Invoke(node, new object[] { node.CurrentFloor, column }) == RoomType.Combat);
        node.OnNodeClicked(node.CurrentFloor, combatColumn);
        yield return new WaitForSeconds(4f);
        foreach (var accordion in FindObjectsByType<AccordionController>(FindObjectsSortMode.None))
        {
            string label = string.Join(" ", accordion.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text));
            if (label.Contains("Currency") || label.Contains("Party")) accordion.SetOpen(true, true);
        }
        yield return null;
        BeginRecording();
        Note("ready");
        yield return new WaitForSeconds(4f);
        while (BattleManager.Instance.currentPhase != BattlePhase.BattleEnd && VideoTime < 150f)
        {
            var battle = BattleManager.Instance;
            if (battle.currentPhase != BattlePhase.PlayerCardPlay) { yield return null; continue; }
            Note("turn " + battle.CurrentTurn);
            var cards = GameManager.Instance.myCards.Where(c => c.gameObject.activeInHierarchy && !c.isUsed).ToArray();
            foreach (var card in cards.OrderByDescending(c => c.stackDelta))
            {
                if (card.stackDelta < 0 && PlayerRoleCost.Instance.GetAmount(card.stackType) + card.stackDelta < 3) continue;
                card.GetComponent<Button>().onClick.Invoke();
                Note("select " + card.stackType + " " + card.stackDelta);
                yield return new WaitForSeconds(battle.CurrentTurn == 1 ? 0.65f : 0.18f);
            }
            yield return new WaitForSeconds(battle.CurrentTurn == 1 ? 1.1f : 0.25f);
            Note("end turn " + battle.CurrentTurn);
            GameManager.Instance.EndMyTurn();
            yield return null;
            while (battle.currentPhase == BattlePhase.PlayerCardPlay) yield return null;
            while (battle.currentPhase != BattlePhase.PlayerCardPlay && battle.currentPhase != BattlePhase.BattleEnd) yield return null;
        }
        Note("battle ended");
        yield return new WaitForSeconds(2f);
        if (BattleManager.Instance.enemies.All(e => e.isDead))
        {
            Note("victory");
            yield return new WaitForSeconds(2f);
            var result = FindFirstObjectByType<BattleResultScreen>();
            if (result != null) typeof(BattleResultScreen).GetMethod("ProceedVictory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(result, null);
            yield return new WaitForSeconds(0.3f);
            Note("clue");
            yield return new WaitForSeconds(4f);
        }
        _recording = false;
        FinishEncoder();
        File.WriteAllText(Video0612Capture.Output + "/state.json", JsonUtility.ToJson(new CaptureState
        {
            phase = BattleManager.Instance.currentPhase.ToString(),
            turn = BattleManager.Instance.CurrentTurn,
            allies = BattleManager.Instance.allies.Select(f => f.jobClass.ToString()).ToArray(),
            cards = GameManager.Instance.myCards.Select(c => c.stackType + ":" + c.stackDelta).ToArray(),
            screen = Screen.width + "x" + Screen.height
        }, true));
        if (PostBattleObservationPanel.Instance != null)
        {
            PostBattleObservationPanel.Instance.GetComponentInChildren<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            ReleaseCapture();
            typeof(NodeSystem).GetProperty("CurrentRoomType").SetValue(node, RoomType.Elite);
            typeof(NodeSystem).GetMethod("DispatchByRoomType", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(node, new object[] { RoomType.Elite });
            yield return new WaitForSeconds(4f);
            _frames = 0;
            _events.Clear();
            _clipName = "elite";
            BeginRecording();
            Note("elite ready");
            yield return new WaitForSeconds(1f);
            foreach (var card in GameManager.Instance.myCards.Where(c => c.gameObject.activeInHierarchy && !c.isUsed && c.stackDelta > 0))
            {
                card.GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(0.4f);
            }
            GameManager.Instance.EndMyTurn();
            yield return new WaitForSeconds(8f);
            _recording = false;
            FinishEncoder();
        }
        EditorApplication.isPlaying = false;
    }

    private void Note(string label)
    {
        _events.Add(VideoTime.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "\t" + label);
        File.WriteAllLines(Video0612Capture.Output + "/" + _clipName + "-events.tsv", _events);
    }

    private void BeginRecording()
    {
        _camera = Camera.main;
        _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        _target.Create();
        _pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        _camera.targetTexture = _target;
        _camera.aspect = 1920f / 1080f;
        foreach (var background in FindObjectsByType<BattleBackground>(FindObjectsSortMode.None)) background.Apply(RoomType.Combat);
        _encoder = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Users\kshyeon\AppData\Local\CapCut\Apps\9.4.0.4015\ffmpeg.exe",
                Arguments = "-hide_banner -loglevel warning -y -f rawvideo -pix_fmt rgb24 -s 1920x1080 -r 30 -i pipe:0 -vf vflip -an -c:v h264_mf -b:v 16000000 -pix_fmt yuv420p -movflags +faststart \"" + Path.GetFullPath(Video0612Capture.Output + "/" + _clipName + "-recording.mp4") + "\"",
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true
            }
        };
        _encoder.ErrorDataReceived += (_, args) => { if (args.Data != null) lock (_encoderLog) _encoderLog.AppendLine(args.Data); };
        _encoder.Start();
        _encoder.BeginErrorReadLine();
        Time.captureFramerate = 30;
        _recording = true;
    }

    private void LateUpdate()
    {
        if (!_recording) return;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace ||
                (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == _camera)) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 5f;
            ResponsiveUi.Configure(canvas);
        }
        Canvas.ForceUpdateCanvases();
        _camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = _target;
        _pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
        RenderTexture.active = previous;
        byte[] bytes = _pixels.GetRawTextureData<byte>().ToArray();
        _encoder.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
        if (_frames == 30 || _frames % 300 == 0)
        {
            File.WriteAllBytes(Video0612Capture.Output + "/" + _clipName + "-preview-" + _frames.ToString("D5") + ".png", _pixels.EncodeToPNG());
            File.WriteAllText(Video0612Capture.Output + "/progress.txt", _clipName + " " + VideoTime.ToString("F2"));
        }
        _frames++;
        if (VideoTime > 160f) { Note("timeout"); EditorApplication.isPlaying = false; }
    }

    private void FinishEncoder()
    {
        if (_encoder == null) return;
        _encoder.StandardInput.Close();
        _encoder.WaitForExit(10000);
        lock (_encoderLog) File.WriteAllText(Video0612Capture.Output + "/" + _clipName + "-encoder.log", _encoderLog.ToString());
        _encoder.Dispose();
        _encoder = null;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
        _recording = false;
        FinishEncoder();
        Time.captureFramerate = 0;
        ReleaseCapture();
    }

    private void ReleaseCapture()
    {
        if (_camera != null) _camera.targetTexture = null;
        if (_target != null) { _target.Release(); Destroy(_target); }
        if (_pixels != null) Destroy(_pixels);
    }

    [Serializable] private sealed class CaptureState
    {
        public string phase;
        public int turn;
        public string[] allies;
        public string[] cards;
        public string screen;
    }

    private void CapturePng(string file)
    {
        var camera = Camera.main;
        _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        _target.Create();
        _pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        camera.targetTexture = _target;
        camera.aspect = 1920f / 1080f;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            ResponsiveUi.Configure(canvas);
        }
        foreach (var background in FindObjectsByType<BattleBackground>(FindObjectsSortMode.None)) background.Apply(RoomType.Combat);
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = _target;
        _pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        _pixels.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(Video0612Capture.Output + "/" + file, _pixels.EncodeToPNG());
        camera.targetTexture = null;
        _target.Release();
        Destroy(_target);
        Destroy(_pixels);
        Debug.Log("[Video0612] Captured " + file);
    }
}
