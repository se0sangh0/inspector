using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Wave B 배경·단서를 임포트하고 실제 UI 컴포넌트로 검증한다.</summary>
public static class WaveBEnvironmentInstaller
{
    private const string SourceRoot = "art/production/graphics-remake/wave-b/";
    private const string OutputRoot = "Assets/Resources/RemakeV1/";
    private const string BackgroundName = "Battle_Canyon_Floor4_v1";
    private const string BackgroundPath = "RemakeV1/Backgrounds/" + BackgroundName;
    private const string ScenePath = "Assets/Scenes/GamePlayScene.unity";
    private const string ReportRoot = "art/review/2026-09-16-wave-b-integration/";
    private static readonly string[] ClueNames =
    {
        "OBS_TORN_BASKET_v1", "OBS_STONE_EDGED_SLOPE_v1", "OBS_COVERED_ROCK_GAP_v1",
        "OBS_HORN_KNOT_v1", "OBS_LOW_PASSAGE_BARRIER_v1", "OBS_FOREST_BARRICADE_v1"
    };
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable] private sealed class Report
    {
        public string unityVersion;
        public string completedUtc;
        public bool passed;
        public int importedSprites;
        public int backgroundModes;
        public int observationLayouts;
        public bool bossPreserved;
        public bool legacyImageNames;
        public bool emptyImage;
        public bool continueOnce;
        public List<string> renders = new List<string>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("Tools/Graphics Remake/Apply Wave B Background and Clues %&#F9")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before importing Wave B art.");
        var report = new Report { unityVersion = Application.unityVersion };
        Directory.CreateDirectory(ReportRoot);
        Application.LogCallback captureError = (message, trace, type) =>
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                report.errors.Add(message);
        };
        Application.logMessageReceived += captureError;
        try
        {
            Import("backgrounds", "Backgrounds", BackgroundName, 1672, 941);
            foreach (string name in ClueNames) Import("clue-illustrations", "Clues", name, 1024, 1536);
            report.importedSprites = 7;
            UpdateScenePath();
            ValidateBackground(report);
            ValidateObservations(report);
            Require(report.errors.Count == 0, "Unity reported errors during validation.");
            report.passed = true;
            Debug.Log("[WaveB] PASS: seven sprites, normal/elite background, unchanged boss, two observation routes, portrait layouts and single continue callback.");
        }
        catch (Exception exception)
        {
            report.errors.Add(exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            Application.logMessageReceived -= captureError;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(ReportRoot + "integration-report.json", JsonUtility.ToJson(report, true));
        }
    }

    private static void Import(string sourceFolder, string destinationFolder, string name, int width, int height)
    {
        string destination = OutputRoot + destinationFolder + "/" + name + ".png";
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(SourceRoot + sourceFolder + "/2026-09-09/" + name + ".png", destination, true);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destination);
        Require(sprite != null && sprite.rect.width == width && sprite.rect.height == height, "Sprite import mismatch: " + name);
    }

    private static void UpdateScenePath()
    {
        // 문자열 경로 두 항목만 바꾸고 다른 씬 오브젝트의 재직렬화를 피한다.
        // 저장 뒤 Unity로 씬을 다시 열어 SerializedObject와 실제 표시를 확인한다.
        string yaml = File.ReadAllText(ScenePath);
        const string pattern = @"(?m)^--- !u!114 &[^\r\n]+\r?\n[\s\S]*?(?=^--- !u!|\z)";
        int found = 0;
        string updated = Regex.Replace(yaml, pattern, match =>
        {
            if (!match.Value.Contains("guid: b434568f6e2d84c7386f77677b300b76")) return match.Value;
            found++;
            string block = Regex.Replace(match.Value, @"(?m)^  normalSpritePath: [^\r\n]*", "  normalSpritePath: " + BackgroundPath);
            if (block.Contains("  normalBackgroundBrightness:"))
                block = Regex.Replace(block, @"(?m)^  normalBackgroundBrightness: [^\r\n]*", "  normalBackgroundBrightness: 0.85");
            else
                block += "  normalBackgroundBrightness: 0.85" + (yaml.Contains("\r\n") ? "\r\n" : "\n");
            return block;
        });
        Require(found == 1, "Expected one BattleBackground in GamePlayScene.");
        if (updated != yaml) File.WriteAllText(ScenePath, updated);
        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ValidateBackground(Report report)
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        Material previewMaterial = null;
        try
        {
            var background = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BattleBackground>(true)).Single();
            var serialized = new SerializedObject(background);
            Require(serialized.FindProperty("normalSpritePath").stringValue == BackgroundPath, "Scene still uses the old normal path.");
            Require(serialized.FindProperty("bossSpritePath").stringValue == "BackGround/Battle_Boss", "Boss path changed.");
            float bossBrightness = serialized.FindProperty("backgroundBrightness").floatValue;
            Require(Mathf.Approximately(bossBrightness, 0.28f), "Boss brightness changed.");
            var renderer = (SpriteRenderer)serialized.FindProperty("worldBackground").objectReferenceValue;
            Require(renderer != null, "Missing world background reference.");
            foreach (RoomType room in new[] { RoomType.Combat, RoomType.Elite, RoomType.Boss })
            {
                background.Apply(room);
                var expected = Resources.Load<Sprite>(room == RoomType.Boss ? "BackGround/Battle_Boss" : BackgroundPath);
                Require(renderer.sprite == expected, "Wrong background for " + room);
                Require(renderer.sortingOrder == -100, "Background must remain behind characters.");
                Require(Mathf.Approximately(renderer.color.r, room == RoomType.Boss ? bossBrightness : 0.85f), "Brightness mismatch.");
                report.backgroundModes++;
            }
            report.bossPreserved = true;
            background.Apply(RoomType.Combat);
            previewMaterial = renderer.sharedMaterial;
            // 그림과 명도만 검증하는 격리된 월드 카메라. 다른 씬 오브젝트는 그리지 않는다.
            var renderScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var copy = new GameObject("Wave B background", typeof(SpriteRenderer));
                SceneManager.MoveGameObjectToScene(copy, renderScene);
                var image = copy.GetComponent<SpriteRenderer>();
                image.sprite = renderer.sprite;
                image.sharedMaterial = renderer.sharedMaterial;
                image.color = renderer.color;
                image.sortingOrder = renderer.sortingOrder;
                var camera = CreateCamera(renderScene, 5.4f);
                float scale = Mathf.Max(19.2f / image.sprite.bounds.size.x, 10.8f / image.sprite.bounds.size.y);
                copy.transform.localScale = new Vector3(scale, scale, 1);
                foreach (var size in Resolutions()) Render(camera, size, "battle", report);
            }
            finally { EditorSceneManager.ClosePreviewScene(renderScene); }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (previewMaterial != null) Object.DestroyImmediate(previewMaterial);
        }
    }

    private static void ValidateObservations(Report report)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var holder = new GameObject("Observation verification");
            SceneManager.MoveGameObjectToScene(holder, scene);
            var panel = holder.AddComponent<PostBattleObservationPanel>();
            Invoke(panel, "Build"); // Awake의 DontDestroyOnLoad와 전역 Instance를 건드리지 않는다.
            var canvas = holder.GetComponentInChildren<Canvas>(true);
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
            var camera = CreateCamera(scene, 540f);
            canvas.worldCamera = camera;
            var image = Field<Image>(panel, "_image");
            var box = Field<RectTransform>(panel, "_box");
            var body = Field<TMP_Text>(panel, "_bodyText");
            var title = Field<TMP_Text>(panel, "_titleText");
            foreach (string name in ClueNames)
            {
                var sprite = Resources.Load<Sprite>("RemakeV1/Clues/" + name);
                Require(sprite != null && Mathf.Approximately(sprite.rect.width / sprite.rect.height, 2f / 3f), "Clue aspect mismatch: " + name);
            }
            foreach (string id in new[] { FieldObservationCatalog.TornBasketId, FieldObservationCatalog.ForestBarricadeId })
            {
                var observation = FieldObservationCatalog.GetById(id);
                Require(observation.imageName == "RemakeV1/Clues/" + id + "_v1", "Observation route mismatch: " + id);
                foreach (var pixels in Resolutions())
                {
                    Vector2 logical = ResponsiveUi.LogicalSize(pixels);
                    canvasRect.sizeDelta = logical;
                    camera.aspect = pixels.x / pixels.y;
                    Invoke(panel, "_Show", observation.title, observation.screenText, observation.imageName, null);
                    var sprite = Resources.Load<Sprite>(observation.imageName);
                    typeof(PostBattleObservationPanel).GetMethod("LayoutBox", PrivateInstance, null,
                        new[] { typeof(Sprite), typeof(Vector2) }, null).Invoke(panel, new object[] { sprite, logical });
                    Canvas.ForceUpdateCanvases();
                    foreach (var text in holder.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();
                    Require(image.sprite == sprite && image.gameObject.activeSelf && image.preserveAspect, "Observation image missing.");
                    Require(box.sizeDelta.x <= logical.x - 80 && box.sizeDelta.y <= logical.y - 80, "Observation box exceeds viewport.");
                    var corners = new Vector3[4];
                    box.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        Vector3 point = camera.WorldToViewportPoint(corner);
                        Require(point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1, "Observation is outside the render viewport.");
                    }
                    Require(!body.isTextOverflowing && !title.isTextOverflowing, "Observation text overflow.");
                    Require(image.rectTransform.anchoredPosition.y - image.rectTransform.sizeDelta.y >= body.rectTransform.anchoredPosition.y,
                        "Image overlaps body text.");
                    Render(camera, pixels, id, report);
                    report.observationLayouts++;
                }
            }
            Invoke(panel, "_Show", "", "", "Torn_Basket", null);
            Require(image.sprite == Resources.Load<Sprite>("ResultImage/Torn_Basket"), "Legacy filename no longer loads.");
            report.legacyImageNames = true;
            Invoke(panel, "_Show", "", "", null, null);
            Require(!image.gameObject.activeSelf, "Empty image should be hidden.");
            report.emptyImage = true;
            int calls = 0;
            Action onContinue = () => calls++;
            Invoke(panel, "_Show", "", "", null, onContinue);
            var button = holder.GetComponentsInChildren<Button>(true).Single();
            button.onClick.Invoke();
            button.onClick.Invoke();
            Require(calls == 1, "Continue callback must fire once.");
            report.continueOnce = true;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static Vector2[] Resolutions() => new[] { new Vector2(1280, 720), new Vector2(1920, 1080) };
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    private static void Invoke(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, arguments);

    private static Camera CreateCamera(Scene scene, float size)
    {
        var go = new GameObject("Wave B preview camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(go, scene);
        var camera = go.GetComponent<Camera>();
        camera.scene = scene;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = size;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.04f, 0.045f);
        return camera;
    }

    private static void Render(Camera camera, Vector2 size, string name, Report report)
    {
        int width = (int)size.x, height = (int)size.y;
        var target = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            string path = ReportRoot + name + "-" + width + "x" + height + ".png";
            File.WriteAllBytes(path, image.EncodeToPNG());
            report.renders.Add(path);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
