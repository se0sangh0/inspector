using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>원본 그림을 임포트하고 카드·스택 프리팹 연결을 검증한다.</summary>
public static class WaveACardUiInstaller
{
    private const string SourceRoot = "art/production/graphics-remake/wave-a/card-ui/2026-09-09/";
    private const string OutputRoot = "Assets/Resources/RemakeV1/CardUI/";
    private const string PrefabPath = "Assets/Prefab/UI/GamePlayScene_RightMainArea.prefab";
    private const string ScenePath = "Assets/Scenes/GamePlayScene.unity";
    private const string ReportRoot = "art/review/2026-09-16-card-ui-integration/";
    private static readonly string[] BackgroundNames =
    {
        "Card_Attack_Background_v1", "Card_Defense_Background_v1", "Card_Support_Background_v1"
    };
    private static readonly string[] BadgeNames =
    {
        "Badge_Attack_Sword_v1", "Badge_Defense_Shield_v1", "Badge_Support_Heart_v1"
    };
    private static readonly string[] StackPaths =
    {
        "StatusArea/MyStatus/StackBar/Row_3_Boxes/Box_Attack/job/RoleIcon",
        "StatusArea/MyStatus/StackBar/Row_3_Boxes/Box_Tank/job/RoleIcon",
        "StatusArea/MyStatus/StackBar/Row_3_Boxes/Box_Support/job/RoleIcon"
    };

    [Serializable] private sealed class Report
    {
        public string unityVersion;
        public string completedUtc;
        public bool passed;
        public int importedSprites;
        public int prefabCards;
        public int sceneStackIcons;
        public int cardSetups;
        public bool pendingDiscardAndReuse;
        public List<string> renders = new List<string>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("Tools/Graphics Remake/Apply Wave A Card UI %&#F8")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before importing card UI.");
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
            foreach (string name in BackgroundNames.Concat(BadgeNames))
                Require(File.Exists(SourceRoot + name + ".png"), "Missing source: " + name);
            foreach (string name in BackgroundNames) Import(name, false);
            foreach (string name in BadgeNames) Import(name, true);
            report.importedSprites = 6;
            UpdatePrefab(report);
            ValidateAndRender(report);
            Require(report.errors.Count == 0, "Unity reported an error during validation.");
            report.passed = true;
            Debug.Log("[WaveACardUI] PASS: six sprites, four card slots, three stack icons, signed values and reuse; two preview resolutions.");
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

    private static void Import(string name, bool badge)
    {
        Directory.CreateDirectory(OutputRoot);
        string destination = OutputRoot + name + ".png";
        File.Copy(SourceRoot + name + ".png", destination, true);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = badge ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = badge;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = badge ? 256 : 1024;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        if (badge)
        {
            // 원본 PNG는 보존한다. 정사각 슬라이스 안에 그림 75%와 여백 25%를 배치한다.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(texture.LoadImage(File.ReadAllBytes(destination)), "Unreadable image: " + name);
                Color32[] pixels = texture.GetPixels32();
                int left = texture.width, bottom = texture.height, right = 0, top = 0;
                for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < texture.width; x++)
                        if (pixels[y * texture.width + x].a >= 128)
                        {
                            left = Mathf.Min(left, x); right = Mathf.Max(right, x + 1);
                            bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y + 1);
                        }
                Require(right > left && top > bottom, "Empty badge: " + name);
                int side = Mathf.CeilToInt(Mathf.Max(right - left, top - bottom) / 0.75f);
                var area = new Rect(Mathf.Floor((left + right - side) * 0.5f), Mathf.Floor((bottom + top - side) * 0.5f), side, side);
                Require(area.xMin >= 0 && area.yMin >= 0 && area.xMax <= texture.width && area.yMax <= texture.height, "Badge crop exceeds source.");
                var factories = new SpriteDataProviderFactories();
                factories.Init();
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var prior = provider.GetSpriteRects().FirstOrDefault(rect => rect.name == name);
                var sprite = new SpriteRect
                {
                    name = name, rect = area, alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f),
                    spriteID = prior != null ? prior.spriteID : GUID.Generate()
                };
                provider.SetSpriteRects(new[] { sprite });
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(name, sprite.spriteID) });
                provider.Apply();
            }
            finally { Object.DestroyImmediate(texture); }
        }
        importer.SaveAndReimport();
        Require(AssetDatabase.LoadAllAssetsAtPath(destination).OfType<Sprite>().Count() == 1, "Expected one sprite: " + destination);
    }

    private static void UpdatePrefab(Report report)
    {
        string originalYaml = File.ReadAllText(PrefabPath);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            for (int i = 0; i < StackPaths.Length; i++)
            {
                var icon = root.transform.Find(StackPaths[i]).GetComponent<Image>();
                icon.sprite = StackCardArt.Badge((StackType)i);
                icon.color = Color.white;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            foreach (var card in root.GetComponentsInChildren<StackCardController>(true))
            {
                var background = card.GetComponent<Image>();
                background.sprite = StackCardArt.Background(card.stackType);
                background.color = Color.white;
                background.type = Image.Type.Simple;
                card.numberText.color = StackCardArt.NumberColor;
                report.prefabCards++;
            }
            Require(report.prefabCards == 4, "Expected four prefab card slots.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // Unity가 함께 저장한 전투 컴포넌트의 기본 필드는 이번 UI 변경에 넣지 않는다.
        const string blockPattern = @"(?m)^--- !u!\d+ &([^\r\n]+)\r?\n[\s\S]*?(?=^--- !u!|\z)";
        var originals = Regex.Matches(originalYaml, blockPattern).Cast<Match>()
            .ToDictionary(match => match.Groups[1].Value, match => match.Value);
        string serialized = File.ReadAllText(PrefabPath);
        string focused = Regex.Replace(serialized, blockPattern, match =>
        {
            bool uiComponent = match.Value.Contains("UnityEngine.UI::UnityEngine.UI.Image")
                || match.Value.Contains("TMPro.TextMeshProUGUI");
            return !uiComponent && originals.TryGetValue(match.Groups[1].Value, out string original)
                ? original : match.Value;
        });
        if (focused != serialized)
        {
            File.WriteAllText(PrefabPath, focused);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static void ValidateAndRender(Report report)
    {
        var sourceScene = EditorSceneManager.OpenPreviewScene(ScenePath);
        var preview = EditorSceneManager.NewPreviewScene();
        var owner = ScriptableObject.CreateInstance<FellowData>();
        try
        {
            var root = sourceScene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "GamePlayScene_RightMainArea");
            for (int i = 0; i < StackPaths.Length; i++)
            {
                Require(root.Find(StackPaths[i]).GetComponent<Image>().sprite == StackCardArt.Badge((StackType)i), "Scene stack icon mismatch.");
                report.sceneStackIcons++;
            }
            var template = root.GetComponentsInChildren<StackCardController>(true).First();
            var canvasObject = new GameObject("Card UI verification", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, preview);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
            for (int i = 0; i < 6; i++)
            {
                var card = Object.Instantiate(template, canvas.transform);
                card.gameObject.SetActive(true);
                var rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one;
                rect.anchoredPosition = new Vector2((i - 2.5f) * 230f, 0);
                // Preview Scene는 Awake를 실행하지 않는다. 실제 초기화와 동일한 기준 크기를 잡는다.
                typeof(StackCardController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(card, null);
                owner.role = (CompanionRole)(i % 3);
                foreach (int delta in new[] { -5, -1, 1, 5 })
                {
                    card.SetupCard(delta, owner);
                    Require(card.currentNumber == delta && card.stackDelta == delta && card.stackType == (StackType)(i % 3), "Card data changed.");
                    Require(card.numberText.text == (delta > 0 ? "+" + delta : delta.ToString()), "Signed value mismatch.");
                    Require(card.GetComponent<Image>().sprite == StackCardArt.Background(card.stackType), "Card background mismatch.");
                    Require(card.roleText.transform.Find("RoleIcon").GetComponent<Image>().sprite == StackCardArt.Badge(card.stackType), "Card badge mismatch.");
                    Require(card.numberText.color == StackCardArt.NumberColor, "Card contrast mismatch.");
                    report.cardSetups++;
                }
                card.SetPendingDiscard(true);
                Require(card.IsPendingDiscard && card.GetComponent<Image>().color != Color.white, "Discard highlight missing.");
                card.SetPendingDiscard(false);
                Require(!card.IsPendingDiscard && card.GetComponent<Image>().color == Color.white, "Discard reset missing.");
                card.MarkConsumed();
                Require(card.isUsed && !card.GetComponent<Button>().interactable, "Consumed state mismatch.");
                card.SetupCard(i < 3 ? 3 : -2, owner);
                Require(!card.isUsed && card.GetComponent<Button>().interactable, "Card reuse failed.");
                // MarkConsumed 원위치 복귀 후 검증 배치로 돌린다.
                rect.anchoredPosition = new Vector2((i - 2.5f) * 230f, 0);
                var symbol = new GameObject("Stack symbol", typeof(RectTransform), typeof(Image));
                symbol.transform.SetParent(canvas.transform, false);
                var image = symbol.GetComponent<Image>();
                image.sprite = StackCardArt.Badge(card.stackType);
                image.preserveAspect = true;
                image.rectTransform.sizeDelta = new Vector2(36, 36);
                image.rectTransform.anchoredPosition = new Vector2(rect.anchoredPosition.x, 220);
            }
            report.pendingDiscardAndReuse = true;
            var cameraObject = new GameObject("Card UI preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 540;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.045f);
            canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases();
            foreach (var label in canvas.GetComponentsInChildren<TextMeshProUGUI>()) label.ForceMeshUpdate();
            Render(camera, 1280, 720, report);
            Render(camera, 1920, 1080, report);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            EditorSceneManager.ClosePreviewScene(preview);
            EditorSceneManager.ClosePreviewScene(sourceScene);
        }
    }

    private static void Render(Camera camera, int width, int height, Report report)
    {
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
            string path = ReportRoot + "cards-" + width + "x" + height + ".png";
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
