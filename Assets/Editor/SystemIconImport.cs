using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>시스템 아이콘 원본을 보존하며 Unity의 표준 임포트 설정을 적용한다.</summary>
public static class SystemIconImport
{
    private const string Root = "Assets/Resources/RemakeV1/SystemIcons/";
    public static readonly string[] Names =
    {
        "icon_notebook", "icon_soulstone", "icon_magicstone", "icon_core_clue",
        "icon_settings", "icon_help", "icon_back", "icon_close"
    };

    [MenuItem("Tools/Graphics Remake/Import System Icons")]
    public static void Apply()
    {
        foreach (string name in Names)
        {
            string path = Root + name + ".png";
            if (!File.Exists(path)) throw new FileNotFoundException("Missing system icon", path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Not a texture: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 64;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
