using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>검토한 콘텐츠를 디버그 도구 없는 플레이어로 빌드한다. 자동 배포는 하지 않는다.</summary>
public static class InspectorPlayerBuild
{
    private static readonly string[] Scenes =
    {
        "Assets/Scenes/GameStartScene.unity",
        "Assets/Scenes/GamePlayScene.unity"
    };

    [Serializable] private sealed class BuildRecord
    {
        public string result, target, architecture, unityVersion, product, version, bundleId, output, completedUtc;
        public bool development;
        public int errors, warnings;
        public ulong bytes;
        public string[] scenes;
    }

    [MenuItem("Tools/INSPECTOR/Build Mac Release")]
    public static void BuildMacRelease() => Run(BuildTarget.StandaloneOSX, "macOS/INSPECTOR.app");

    [MenuItem("Tools/INSPECTOR/Build Windows Release")]
    public static void BuildWindowsRelease() => Run(BuildTarget.StandaloneWindows64, "Windows/INSPECTOR.exe");

    private static void Run(BuildTarget target, string defaultOutput)
    {
        int exitCode = 1;
        string originalProduct = PlayerSettings.productName;
        PropertyInfo architectureProperty = null;
        object originalArchitecture = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Unity build support is not installed for " + target);
            foreach (string scene in Scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                    throw new InvalidOperationException("Missing build scene: " + scene);

            ValidateRouteContent();
            string architecture = target == BuildTarget.StandaloneOSX ? "Universal" : "x86_64";
            if (target == BuildTarget.StandaloneOSX)
            {
                var settings = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings"))
                    .FirstOrDefault(t => t != null);
                architectureProperty = settings?.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
                if (architectureProperty == null)
                    throw new InvalidOperationException("Mac architecture settings unavailable.");
                originalArchitecture = architectureProperty.GetValue(null);
                string universal = Enum.GetNames(architectureProperty.PropertyType)
                    .FirstOrDefault(n => n.Equals("x64ARM64", StringComparison.OrdinalIgnoreCase)
                        || n.Equals("Universal", StringComparison.OrdinalIgnoreCase));
                if (universal == null)
                    throw new InvalidOperationException("Universal Mac architecture unavailable: "
                        + string.Join(", ", Enum.GetNames(architectureProperty.PropertyType)));
                architectureProperty.SetValue(null, Enum.Parse(architectureProperty.PropertyType, universal));
                architecture = universal;
            }

            // 회사명·앱 식별자·버전 등 저장 계약은 프로젝트 설정을 그대로 사용한다.
            PlayerSettings.productName = "INSPECTOR";
            string output = Environment.GetEnvironmentVariable("INSPECTOR_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(output)) output = Path.GetFullPath("Builds/" + defaultOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            });
            var record = new BuildRecord
            {
                result = report.summary.result.ToString(),
                target = target.ToString(),
                architecture = architecture,
                unityVersion = Application.unityVersion,
                product = PlayerSettings.productName,
                version = PlayerSettings.bundleVersion,
                bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone),
                output = output,
                development = (report.summary.options & BuildOptions.Development) != 0,
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                bytes = report.summary.totalSize,
                scenes = Scenes,
                completedUtc = DateTime.UtcNow.ToString("O")
            };
            string reportPath = Environment.GetEnvironmentVariable("INSPECTOR_BUILD_REPORT");
            if (string.IsNullOrWhiteSpace(reportPath)) reportPath = Path.Combine(Path.GetDirectoryName(output), "build-report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(record, true));
            exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            Debug.Log("[InspectorPlayerBuild] " + record.result + " — " + target + ", development=" + record.development);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            PlayerSettings.productName = originalProduct;
            if (architectureProperty != null && originalArchitecture != null)
                architectureProperty.SetValue(null, originalArchitecture);
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }
    }

    private static void ValidateRouteContent()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Routes/RouteCatalog.json");
        if (catalog == null) throw new InvalidOperationException("Route catalog is missing.");
        for (int floor = 2; floor <= 8; floor++)
            for (int column = 0; column < 3; column++)
            {
                var route = RouteCatalog.Create(floor, column, 0, false);
                if (string.IsNullOrWhiteSpace(route.title) || string.IsNullOrWhiteSpace(route.environment)
                    || string.IsNullOrWhiteSpace(route.omen))
                    throw new InvalidOperationException("Route text is incomplete: " + route.id);
                if (!string.IsNullOrEmpty(route.imagePath) && Resources.Load<Sprite>(route.imagePath) == null)
                    throw new InvalidOperationException("Route illustration is missing: " + route.imagePath);
            }
        if (LocalizationTable.Ko2En["성소로 향한다"] != "Head to the Sanctum")
            throw new InvalidOperationException("Sanctum button translation mismatch.");
    }
}
