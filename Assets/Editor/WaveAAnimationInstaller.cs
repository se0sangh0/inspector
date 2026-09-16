using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Imports the approved Wave A sheets and replaces only sprite curves.</summary>
public static class WaveAAnimationInstaller
{
    private const string SourceRoot = "art/production/graphics-remake/wave-a/";
    private const string OutputRoot = "Assets/Resources/RemakeV1/";
    private const string ReportRoot = "art/review/2026-09-16-wave-a-integration/";
    private const int FrameWidth = 384;
    private const int FrameHeight = 512;

    private sealed class Entry
    {
        public string Name, Group, IdleSource, AttackSource, LegacyFolder;
        public Entry(string name, string group, string idle, string attack, string legacy)
        {
            Name = name; Group = group; IdleSource = idle;
            AttackSource = attack; LegacyFolder = legacy;
        }
        public string ControllerPath => $"Assets/Resources/Animators/{Group}/{Name}/{Name}.controller";
    }

    private static readonly Entry[] Entries =
    {
        new Entry("Caster", "Fellows", "character-sheets/caster/Caster_Filtered_Idle_384x512x4_v2.png", "character-sheets/caster/Caster_Filtered_ElementalBolt_384x512x4_v2.png", "Assets/Resources/Animators/Fellows/Caster"),
        new Entry("Offender", "Fellows", "character-sheets/offender/Offender_Filtered_Idle_384x512x4_v2.png", "character-sheets/offender/Offender_Filtered_TargetPierce_384x512x4_v2.png", "Assets/Resources/Animators/Fellows/Offender"),
        new Entry("Defender", "Fellows", "character-sheets/defender/Defender_Filtered_Idle_384x512x4_v1.png", "character-sheets/defender/Defender_Filtered_DeployBarrier_384x512x4_v2.png", "Assets/Resources/Animators/Fellows/Defender"),
        new Entry("Attacker", "Fellows", "character-sheets/attacker/Attacker_Filtered_Idle_384x512x4_v1.png", "character-sheets/attacker/Attacker_Filtered_ClearTheWay_384x512x4_v1.png", "Assets/Resources/Animators/Fellows/Attacker"),
        new Entry("Priest", "Fellows", "character-sheets/priest/Priest_Filtered_Idle_384x512x4_v1.png", "character-sheets/priest/Priest_Filtered_EmergencyTreatment_384x512x4_v1.png", "Assets/Resources/Animators/Fellows/Priest"),
        new Entry("Goblin", "Enemies", "enemy-sheets/goblin/Goblin_Filtered_Idle_384x512x4_v1.png", "enemy-sheets/goblin/Goblin_Filtered_DaggerSwing_384x512x4_v1.png", "Assets/Resources/Animators/Enemies/Goblin"),
        new Entry("Raider", "Enemies", "enemy-sheets/raider/Raider_Filtered_Idle_384x512x4_v1.png", "enemy-sheets/raider/Raider_Filtered_AxeSwing_384x512x4_v1.png", "Assets/Resources/Animators/Enemies/Wolf")
    };

    [Serializable] private sealed class Report
    {
        public string unityVersion;
        public string completedUtc;
        public bool passed;
        public List<CharacterReport> characters = new List<CharacterReport>();
        public List<string> errors = new List<string>();
    }

    [Serializable] private sealed class CharacterReport
    {
        public string name;
        public string controller;
        public float visibleHeight;
        public float baseline;
        public float pixelsPerUnit;
        public int spriteCount;
        public List<ClipReport> clips = new List<ClipReport>();
    }

    [Serializable] private sealed class ClipReport
    {
        public string path;
        public float durationBefore, durationAfter;
        public bool loop;
        public int events;
        public string[] sprites;
        public bool sampled;
        public bool triggerEntered;
        public bool returnedToIdle;
        public int observedAttackFrames;
    }

    [MenuItem("Tools/Graphics Remake/Apply Wave A Animations %&w")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before replacing animation assets.");
        var report = new Report { unityVersion = Application.unityVersion };
        Directory.CreateDirectory(ReportRoot);
        try
        {
            foreach (var entry in Entries)
            {
                Require(File.Exists(SourceRoot + entry.IdleSource), "Missing idle source: " + entry.Name);
                Require(File.Exists(SourceRoot + entry.AttackSource), "Missing attack source: " + entry.Name);
                Require(AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.LegacyFolder + "/Idle.anim") != null, "Missing legacy idle: " + entry.Name);
            }
            foreach (var entry in Entries)
                ApplyEntry(entry, report);
            string dataPath = "Assets/Resources/Data/enemies.json";
            string data = File.ReadAllText(dataPath);
            string updated = data.Replace("\"animatorPath\": \"Animators/Enemies/Wolf/Wolf\"", "\"animatorPath\": \"Animators/Enemies/Raider/Raider\"");
            Require(updated.Contains("\"animatorPath\": \"Animators/Enemies/Raider/Raider\""), "Raider data mapping missing.");
            if (updated != data) File.WriteAllText(dataPath, updated);
            AssetDatabase.ImportAsset(dataPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            report.passed = true;
            Debug.Log("[WaveA] PASS: 7 characters, 14 sheets, 18 clips; all attack triggers return to Idle.");
        }
        catch (Exception exception)
        {
            report.errors.Add(exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            report.completedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(ReportRoot + "integration-report.json", JsonUtility.ToJson(report, true));
        }
    }

    private static void ApplyEntry(Entry entry, Report report)
    {
        var oldIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.LegacyFolder + "/Idle.anim");
        var oldBinding = SpriteBinding(oldIdle);
        var oldSprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(oldIdle, oldBinding)[0].value;
        Require(oldSprite != null, "Null previous idle: " + entry.Name);
        var oldTexture = ReadTexture(AssetDatabase.GetAssetPath(oldSprite));
        RectInt oldArea = PixelBounds(oldTexture, new RectInt(Mathf.RoundToInt(oldSprite.rect.x), Mathf.RoundToInt(oldSprite.rect.y), Mathf.RoundToInt(oldSprite.rect.width), Mathf.RoundToInt(oldSprite.rect.height)));
        Object.DestroyImmediate(oldTexture);
        Vector2 oldVisibleMin = (new Vector2(oldArea.xMin, oldArea.yMin) - oldSprite.rect.position - oldSprite.pivot) / oldSprite.pixelsPerUnit;
        float oldHeight = oldArea.height / oldSprite.pixelsPerUnit;
        float oldCenterX = oldVisibleMin.x + oldArea.width / oldSprite.pixelsPerUnit * 0.5f;

        var sourceTexture = ReadTexture(SourceRoot + entry.IdleSource);
        RectInt newArea = PixelBounds(sourceTexture, new RectInt(0, 0, FrameWidth, FrameHeight));
        Object.DestroyImmediate(sourceTexture);
        float pixelsPerUnit = newArea.height / oldHeight;
        float anchorX = newArea.center.x - oldCenterX * pixelsPerUnit;
        float baseline = oldVisibleMin.y;
        string outputFolder = OutputRoot + (entry.Group == "Fellows" ? "Characters/" : "Enemies/") + entry.Name;
        var idleSprites = ImportSheet(SourceRoot + entry.IdleSource, outputFolder + "/Idle.png", entry.Name + "_Idle", pixelsPerUnit, anchorX, baseline);
        var attackSprites = ImportSheet(SourceRoot + entry.AttackSource, outputFolder + "/Attack.png", entry.Name + "_Attack", pixelsPerUnit, anchorX, baseline);
        var character = new CharacterReport { name = entry.Name, controller = entry.ControllerPath, visibleHeight = oldHeight, baseline = baseline, pixelsPerUnit = pixelsPerUnit, spriteCount = idleSprites.Length + attackSprites.Length };
        report.characters.Add(character);

        AnimatorController controller;
        if (entry.Name == "Raider")
            controller = PrepareRaiderController(entry);
        else
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(entry.ControllerPath);
        Require(controller != null, "Missing controller: " + entry.Name);
        var states = States(controller.layers[0].stateMachine).ToArray();
        foreach (var clip in controller.animationClips.Distinct())
        {
            bool isIdle = clip.name == "Idle";
            Require(isIdle || clip.name.StartsWith("Attack", StringComparison.Ordinal), "Unexpected clip: " + clip.name);
            var binding = SpriteBinding(clip);
            var oldKeys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            float length = clip.length;
            float lastTime = oldKeys[oldKeys.Length - 1].time;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            var events = AnimationUtility.GetAnimationEvents(clip);
            float exitFraction = 1f;
            foreach (var state in states.Where(state => state.motion == clip))
                foreach (var transition in state.transitions.Where(transition => transition.hasExitTime))
                    exitFraction = Mathf.Min(exitFraction, transition.exitTime);
            float motionDuration = Mathf.Min(length * exitFraction, lastTime * 4f / 3f);
            float firstChange = motionDuration / 4f;
            if (!isIdle)
            {
                // Sprite references switch at the midpoint of the incoming blend.
                // Hold the first pose until it is visible without changing state timings.
                foreach (var target in states.Where(state => state.motion == clip))
                    foreach (var source in states)
                        foreach (var transition in source.transitions.Where(transition => transition.destinationState == target))
                        {
                            float blendSeconds = transition.hasFixedDuration ? transition.duration
                                : transition.duration * source.motion.averageDuration / Mathf.Max(source.speed, 0.001f);
                            firstChange = Mathf.Max(firstChange, blendSeconds * target.speed * 0.5f + 1f / clip.frameRate);
                        }
                Require(firstChange < motionDuration, "Incoming blend hides attack: " + entry.Name + "/" + clip.name);
            }
            Sprite[] sprites = isIdle ? idleSprites : attackSprites;
            var keys = new ObjectReferenceKeyframe[5];
            for (int index = 0; index < 4; index++)
            {
                float time = index == 0 ? 0f : firstChange + (motionDuration - firstChange) * (index - 1) / 3f;
                keys[index] = new ObjectReferenceKeyframe { time = time, value = sprites[index] };
            }
            keys[4] = new ObjectReferenceKeyframe { time = lastTime, value = sprites[3] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            Require(Mathf.Abs(clip.length - length) < 0.0001f, "Duration changed: " + clip.name);
            Require(AnimationUtility.GetAnimationEvents(clip).Length == events.Length, "Events changed: " + clip.name);
            var clipReport = new ClipReport { path = AssetDatabase.GetAssetPath(clip), durationBefore = length, durationAfter = clip.length, loop = settings.loopTime, events = events.Length, sprites = sprites.Select(sprite => sprite.name).ToArray() };
            character.clips.Add(clipReport);
            ValidateSamples(clip, keys, clipReport);
        }
        AssetDatabase.SaveAssets();
        ValidateController(controller, character);
        Require(character.clips.Count == (entry.Name == "Offender" || entry.Name == "Defender" || entry.Name == "Priest" ? 2 : 3), "Clip count mismatch.");
    }

    private static Sprite[] ImportSheet(string source, string destination, string prefix, float pixelsPerUnit, float anchorX, float baseline)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(source, destination, true);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects();
        var texture = ReadTexture(source);
        Require(texture.width == 1536 && texture.height == 512, "Unexpected source dimensions: " + source);
        var rects = new SpriteRect[4];
        for (int index = 0; index < 4; index++)
        {
            RectInt area = PixelBounds(texture, new RectInt(index * FrameWidth, 0, FrameWidth, FrameHeight));
            string name = prefix + "_" + index;
            var prior = existing.FirstOrDefault(rect => rect.name == name);
            float localLeft = area.x - index * FrameWidth;
            // Tight rects retain every alpha pixel; pivots retain the shared cell origin and feet baseline.
            rects[index] = new SpriteRect
            {
                name = name,
                rect = new Rect(area.x, area.y, area.width, area.height),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2((anchorX - localLeft) / area.width, -baseline * pixelsPerUnit / area.height),
                spriteID = prior != null ? prior.spriteID : GUID.Generate()
            };
        }
        Object.DestroyImmediate(texture);
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(destination).OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        Require(sprites.Length == 4, "Expected four sprites: " + destination);
        for (int index = 0; index < 4; index++)
        {
            Require(Mathf.Abs(sprites[index].bounds.min.y - baseline) < 0.01f, "Foot baseline mismatch: " + sprites[index].name);
            Require(sprites[index].rect == rects[index].rect, "Imported rectangle mismatch.");
        }
        return sprites;
    }

    private static AnimatorController PrepareRaiderController(Entry entry)
    {
        string folder = Path.GetDirectoryName(entry.ControllerPath).Replace('\\', '/');
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (!File.Exists(entry.ControllerPath))
            Require(AssetDatabase.CopyAsset(entry.LegacyFolder + "/Wolf.controller", entry.ControllerPath), "Could not copy Raider controller.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(entry.ControllerPath);
        controller.name = "Raider";
        foreach (var state in States(controller.layers[0].stateMachine))
        {
            var sourceClip = state.motion as AnimationClip;
            Require(sourceClip != null, "Unexpected Raider state motion.");
            string clipPath = folder + "/" + sourceClip.name + ".anim";
            if (!File.Exists(clipPath))
                Require(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceClip), clipPath), "Could not copy Raider clip.");
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void ValidateSamples(AnimationClip clip, ObjectReferenceKeyframe[] keys, ClipReport report)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var instance = new GameObject("WaveA clip verification", typeof(SpriteRenderer), typeof(Animator));
        SceneManager.MoveGameObjectToScene(instance, scene);
        var graph = PlayableGraph.Create("WaveA clip samples");
        try
        {
            var output = AnimationPlayableOutput.Create(graph, "Sprite", instance.GetComponent<Animator>());
            var playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
            graph.Play();
            for (int index = 0; index < 4; index++)
            {
                playable.SetTime(keys[index].time + 0.0001f);
                graph.Evaluate(0f);
                Require(instance.GetComponent<SpriteRenderer>().sprite == keys[index].value, "Sample failed: " + clip.name + "/" + index);
            }
            report.sampled = true;
        }
        finally { graph.Destroy(); Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void ValidateController(AnimatorController controller, CharacterReport report)
    {
        foreach (var clipReport in report.clips.Where(clip => Path.GetFileNameWithoutExtension(clip.path).StartsWith("Attack", StringComparison.Ordinal)))
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var instance = new GameObject("WaveA Animator verification", typeof(SpriteRenderer), typeof(Animator));
            SceneManager.MoveGameObjectToScene(instance, scene);
            var graph = PlayableGraph.Create("WaveA controller playback");
            try
            {
                var animator = instance.GetComponent<Animator>();
                var renderer = instance.GetComponent<SpriteRenderer>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var output = AnimationPlayableOutput.Create(graph, "Controller", animator);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                output.SetSourcePlayable(playable);
                graph.Play();
                graph.Evaluate(0f);
                Require(renderer.sprite != null && renderer.sprite.name.Contains("_Idle_"), "Default Idle failed: " + report.name);
                string trigger = Path.GetFileNameWithoutExtension(clipReport.path);
                Require(controller.parameters.Any(parameter => parameter.name == trigger && parameter.type == AnimatorControllerParameterType.Trigger), "Missing trigger: " + trigger);
                playable.SetTrigger(trigger);
                var observed = new HashSet<string>();
                for (int tick = 0; tick < 240; tick++)
                {
                    graph.Evaluate(1f / 120f);
                    if (renderer.sprite != null && clipReport.sprites.Contains(renderer.sprite.name))
                    {
                        observed.Add(renderer.sprite.name);
                        clipReport.triggerEntered = true;
                    }
                }
                clipReport.observedAttackFrames = observed.Count;
                clipReport.returnedToIdle = renderer.sprite != null && renderer.sprite.name.Contains("_Idle_");
                Require(clipReport.triggerEntered && clipReport.returnedToIdle, "Attack/Idle transition failed: " + report.name + "/" + trigger);
                Require(observed.Count == 4, "Not all attack frames played: " + report.name + "/" + trigger + " saw " + observed.Count + ": " + string.Join(", ", observed));
            }
            finally { graph.Destroy(); Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    private static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var state in machine.states) yield return state.state;
        foreach (var child in machine.stateMachines)
            foreach (var state in States(child.stateMachine)) yield return state;
    }

    private static EditorCurveBinding SpriteBinding(AnimationClip clip)
    {
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip).Where(binding => binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite").ToArray();
        Require(bindings.Length == 1 && bindings[0].path == "", "Expected root sprite curve: " + clip.name);
        return bindings[0];
    }

    private static Texture2D ReadTexture(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
        {
            Object.DestroyImmediate(texture);
            throw new InvalidDataException("Could not read PNG: " + path);
        }
        return texture;
    }

    private static RectInt PixelBounds(Texture2D texture, RectInt area)
    {
        var pixels = texture.GetPixels32();
        int minX = area.xMax, minY = area.yMax, maxX = -1, maxY = -1;
        for (int y = area.yMin; y < area.yMax; y++)
            for (int x = area.xMin; x < area.xMax; x++)
                if (pixels[y * texture.width + x].a > 0)
                {
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
        Require(maxX >= minX, "Empty sprite cell.");
        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

