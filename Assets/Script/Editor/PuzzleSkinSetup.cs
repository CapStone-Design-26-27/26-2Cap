using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class PuzzleSkinSetup
{
    private const string Root = "Assets/Aset/UI/Skin/";
    [Serializable] private class Sheet { public int width, height; public Entry[] sprites; }
    [Serializable] private class Entry { public string name; public int x, y, w, h; public Border border; }
    [Serializable] private class Border { public int left, bottom, right, top; }

    [MenuItem("Tools/Puzzle/Import UI Sprites")]
    public static void ImportSprites()
    {
        var sheet = JsonUtility.FromJson<Sheet>(System.IO.File.ReadAllText(Root + "sprites.json"));
        var atlas = (TextureImporter)AssetImporter.GetAtPath(Root + "ui_sprites.png");
        Configure(atlas, SpriteImportMode.Multiple);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(atlas);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects().ToDictionary(rect => rect.name);
        var rects = sheet.sprites.Select(s => new SpriteRect
        {
            name = s.name,
            rect = new Rect(s.x, sheet.height - s.y - s.h, s.w, s.h),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(.5f, .5f),
            border = new Vector4(s.border.left, s.border.bottom, s.border.right, s.border.top),
            spriteID = existing.TryGetValue(s.name, out var old) &&
                !old.spriteID.ToString().StartsWith("0000000000000000", StringComparison.Ordinal)
                ? old.spriteID : GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        atlas.SaveAndReimport();

        var bg = (TextureImporter)AssetImporter.GetAtPath(Root + "bg_play.png");
        Configure(bg, SpriteImportMode.Single);
        bg.SaveAndReimport();
    }

    private static void Configure(TextureImporter importer, SpriteImportMode mode)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = mode;
        importer.spritePixelsPerUnit = 100;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
    }
}
