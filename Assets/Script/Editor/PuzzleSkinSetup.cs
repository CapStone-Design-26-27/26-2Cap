using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [MenuItem("Tools/Puzzle/Apply UI Skin")]
    public static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ImportSprites();
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            EditPrefab("BallQueueUI", root =>
            {
                var next = root.transform.Find("NextButton").GetComponent<Button>();
                ApplyButton(next, "btn_blue_normal", "btn_blue_pressed");
                var inventory = root.transform.Find("Inventory").GetComponent<Image>();
                ApplyImage(inventory, "panel_small");
                var quantity = root.transform.Find("Quantity").GetComponent<RectTransform>();
                var nextRect = (RectTransform)next.transform;
                var countOrigin = nextRect.anchoredPosition - new Vector2(0, nextRect.sizeDelta.y + 6);
                quantity.anchoredPosition = countOrigin - new Vector2(0, 4);
                quantity.sizeDelta = new Vector2(nextRect.sizeDelta.x, 54);
                var backdrop = ChildImage(root.transform, "QuantityBackdrop");
                if (backdrop.transform.GetSiblingIndex() > quantity.GetSiblingIndex())
                    backdrop.transform.SetSiblingIndex(quantity.GetSiblingIndex());
                backdrop.rectTransform.anchorMin = backdrop.rectTransform.anchorMax = quantity.anchorMin;
                backdrop.rectTransform.pivot = quantity.pivot;
                backdrop.rectTransform.anchoredPosition = countOrigin;
                backdrop.rectTransform.sizeDelta = new Vector2(nextRect.sizeDelta.x, 100);
                ApplyImage(backdrop, "quantity_pill");
                backdrop.color = new Color32(20, 38, 74, 166);
                backdrop.raycastTarget = false;
                var count = quantity.GetComponent<TMP_Text>();
                count.font = next.GetComponentInChildren<TMP_Text>(true).font;
                count.fontSharedMaterial = QuantityOutline(count.font);
                count.fontSize = 30;
                count.enableAutoSizing = false;
                count.enableWordWrapping = false;
                count.color = Color.white;
                var total = Text(root.transform, "TotalCount", count.font, 26);
                total.fontSharedMaterial = count.fontSharedMaterial;
                Place(total.rectTransform, quantity.anchorMin, countOrigin - new Vector2(0, 58), new Vector2(nextRect.sizeDelta.x, 34));
                total.rectTransform.pivot = quantity.pivot;
                total.enableWordWrapping = false;
                var fields = new SerializedObject(root.GetComponent<BallQueueUI>());
                fields.FindProperty("totalText").objectReferenceValue = total;
                fields.ApplyModifiedPropertiesWithoutUndo();
                inventory.rectTransform.anchoredPosition = countOrigin - new Vector2(0, 110);
            });
            EditPrefab("GameStateUI", root =>
            {
                var panel = root.transform.Find("StatePanel").GetComponent<Image>();
                panel.enabled = false;
                Place(panel.rectTransform, new Vector2(.5f, 1), new Vector2(0, -103), new Vector2(144, 132));
                var digit = Text(panel.transform, "StateText", panel.GetComponentInChildren<TMP_Text>(true).font, 100);
                digit.fontStyle = FontStyles.Bold;
                digit.color = new Color(1, 1, 1, .84f);
                digit.enableAutoSizing = false;
            });

            var play = EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity");
            var queue = UnityEngine.Object.FindObjectOfType<BallQueueUI>(true);
            var hud = queue.GetComponentInParent<Canvas>();
            hud.name = "FixedCanvas";
            hud.renderMode = RenderMode.ScreenSpaceOverlay;
            hud.worldCamera = null;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            var canvas = RectChild(hud.transform, "SafeArea");
            Stretch(canvas);
            foreach (var child in hud.transform.Cast<Transform>().Where(child => child != canvas).ToArray())
                child.SetParent(canvas, false);
            var bottom = RectChild(canvas, "BottomControls");
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = Vector2.right;
            bottom.pivot = new Vector2(.5f, 0);
            bottom.anchoredPosition = Vector2.zero;
            bottom.sizeDelta = new Vector2(0, 128);
            var safeArea = canvas.GetComponent<PlaySafeArea>() ?? canvas.gameObject.AddComponent<PlaySafeArea>();
            var safeFields = new SerializedObject(safeArea);
            safeFields.FindProperty("bottomControls").objectReferenceValue = bottom;
            safeFields.FindProperty("bottomReservedHeight").floatValue = 196f;
            safeFields.ApplyModifiedPropertiesWithoutUndo();
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("TestScene needs a MainCamera.");
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            var world = play.GetRootGameObjects().FirstOrDefault(root => root.name == "WorldCanvas");
            if (world == null) world = new GameObject("WorldCanvas", typeof(RectTransform), typeof(Canvas));
            var worldCanvas = world.GetComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = camera;
            var guide = UnityEngine.Object.FindObjectOfType<GuideLineController>(true);
            if (guide != null) guide.transform.SetParent(world.transform, true);
            var bg = camera.transform.Find("PlaySkinBackground");
            var renderer = bg != null ? bg.GetComponent<SpriteRenderer>() :
                new GameObject("PlaySkinBackground", typeof(SpriteRenderer), typeof(PlaySkinBackground)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(camera.transform, false);
            renderer.transform.localPosition = new Vector3(0, 0, 50);
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "bg_play.png");
            renderer.sortingOrder = short.MinValue;
            var ground = canvas.Find("GroundSkinStrip");
            if (ground != null) UnityEngine.Object.DestroyImmediate(ground.gameObject);
            foreach (string name in new[] { "Btnpanel", "UndoButton", "RetryButton" })
            {
                var child = canvas.Find(name);
                if (child != null) child.SetParent(bottom, false);
            }
            var oldPanel = bottom.Find("Btnpanel");
            if (oldPanel != null && oldPanel.TryGetComponent(out Image oldPanelImage))
                oldPanelImage.enabled = false;
            var font = queue.GetComponentInChildren<TMP_Text>(true).font;
            var main = oldPanel != null ? oldPanel.GetComponentInChildren<Button>(true) : null;
            if (main == null) throw new InvalidOperationException("TestScene needs its center button.");
            Place((RectTransform)main.transform, new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(184, 96));
            main.onClick = new Button.ButtonClickedEvent();
            var cameraFocus = camera.GetComponent<CameraFocusController>();
            if (cameraFocus == null) throw new InvalidOperationException("TestScene needs camera focus controls.");
            UnityEventTools.AddPersistentListener(main.onClick, cameraFocus.ShowOverview);
            var cameraFields = new SerializedObject(cameraFocus);
            cameraFields.FindProperty("playSafeArea").objectReferenceValue = safeArea;
            cameraFields.ApplyModifiedPropertiesWithoutUndo();
            ApplyButton(main, "btn_blue_normal", "btn_blue_pressed");
            var label = main.GetComponentInChildren<TMP_Text>(true);
            label.text = "MAIN";
            label.font = font;
            label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "PlayText.mat");
            label.color = Color.white;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 34;
            label.enableWordWrapping = false;
            ApplyStageDisplay(canvas, bottom, font);
            EditorSceneManager.SaveScene(play);
            AssetDatabase.SaveAssets();
            Debug.Log("Puzzle UI skin applied.");
        }
        finally
        {
            if (!string.IsNullOrEmpty(originalScene)) EditorSceneManager.OpenScene(originalScene);
        }
    }

    private static void ApplyStageDisplay(Transform canvas, Transform bottom, TMP_FontAsset font)
    {
        var header = ChildImage(canvas, "StageSkinHeader");
        Place(header.rectTransform, new Vector2(1, 1), new Vector2(-190, -114), new Vector2(340, 196));
        ApplyImage(header, "title_plate");
        header.raycastTarget = false;
        var title = Text(header.transform, "StageTitle", font, 40);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(.5f, 1), new Vector2(0, -46), new Vector2(282, 54));
        var status = Text(header.transform, "Status", font, 25);
        Place(status.rectTransform, new Vector2(.5f, 1), new Vector2(0, -96), new Vector2(282, 36));
        var meter = ChildImage(header.transform, "ClearMeter");
        Place(meter.rectTransform, new Vector2(.5f, 1), new Vector2(0, -134), new Vector2(264, 33));
        meter.rectTransform.pivot = new Vector2(.5f, 1);
        ApplyImage(meter, "progress_bar_bg");
        meter.raycastTarget = false;
        var fill = ChildImage(meter.transform, "Fill");
        ApplyImage(fill, "progress_bar_fill");
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        var star = meter.transform.Find("Star");
        if (star != null) UnityEngine.Object.DestroyImmediate(star.gameObject);
        meter.gameObject.SetActive(true);
        fill.rectTransform.anchorMax = new Vector2(0, 1);
        fill.gameObject.SetActive(false);
        var oldPause = canvas.Find("PauseButton");
        if (oldPause != null) UnityEngine.Object.DestroyImmediate(oldPause.gameObject);
        var undo = IconButton(bottom, "UndoButton", false, "undo");
        ApplyButton(undo, "circle_undo", "circle_undo_pressed");
        var undoSprites = undo.spriteState;
        undoSprites.disabledSprite = Sprite("circle_disabled");
        undo.spriteState = undoSprites;
        undo.interactable = false;
        var undoIcon = undo.transform.Find("Icon").GetComponent<Image>();
        undoIcon.color = new Color32(224, 230, 238, 209);
        var retry = IconButton(bottom, "RetryButton", true, "retry");
        var badge = ChildImage(undo.transform, "FreeCountBadge");
        Place(badge.rectTransform, Vector2.one, new Vector2(8, 8), new Vector2(82, 82));
        ApplyImage(badge, "badge_disc");
        badge.color = new Color32(20, 38, 74, 255);
        badge.raycastTarget = false;
        var face = ChildImage(badge.transform, "Face");
        Stretch(face.rectTransform);
        face.rectTransform.offsetMin = new Vector2(5, 5);
        face.rectTransform.offsetMax = new Vector2(-5, -5);
        ApplyImage(face, "badge_disc");
        face.color = new Color32(164, 174, 187, 255);
        face.raycastTarget = false;
        var oldCount = badge.transform.Find("Count");
        if (oldCount != null) oldCount.SetParent(face.transform, false);
        var undoCount = Text(face.transform, "Count", font, 35);
        undoCount.fontStyle = FontStyles.Bold;
        undoCount.color = new Color32(20, 38, 74, 255);
        undoCount.rectTransform.offsetMin = undoCount.rectTransform.offsetMax = Vector2.zero;
        undoCount.text = "1";
        var result = ChildImage(canvas, "StageSkinResult");
        result.transform.SetAsLastSibling();
        Place(result.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(460, 310));
        ApplyImage(result, "panel_large");
        var caption = Text(result.transform, "Caption", font, 39);
        Place(caption.rectTransform, new Vector2(.5f, 1), new Vector2(0, -56), new Vector2(430, 76));
        var action = ResultButton(result.transform, "Action", font, -22);
        var home = ResultButton(result.transform, "Home", font, -103);
        home.GetComponentInChildren<TMP_Text>(true).text = "HOME";
        var back = canvas.Find("BackToBaskets");
        if (back != null) UnityEngine.Object.DestroyImmediate(back.gameObject);
        var view = header.GetComponent<StagePresentationUI>() ?? header.gameObject.AddComponent<StagePresentationUI>();
        var fields = new SerializedObject(view);
        fields.FindProperty("status").objectReferenceValue = status;
        fields.FindProperty("stageTitle").objectReferenceValue = title;
        fields.FindProperty("progressFill").objectReferenceValue = fill.rectTransform;
        fields.FindProperty("undo").objectReferenceValue = undo;
        fields.FindProperty("undoCount").objectReferenceValue = undoCount;
        fields.FindProperty("undoBadge").objectReferenceValue = face;
        fields.FindProperty("undoIcon").objectReferenceValue = undoIcon;
        fields.FindProperty("retry").objectReferenceValue = retry;
        fields.FindProperty("result").objectReferenceValue = result.gameObject;
        fields.FindProperty("caption").objectReferenceValue = caption;
        fields.FindProperty("action").objectReferenceValue = action;
        fields.FindProperty("actionLabel").objectReferenceValue = action.GetComponentInChildren<TMP_Text>(true);
        fields.FindProperty("home").objectReferenceValue = home;
        fields.ApplyModifiedPropertiesWithoutUndo();
        result.gameObject.SetActive(false);
    }

    private static Button IconButton(Transform canvas, string name, bool right, string iconName)
    {
        var image = ChildImage(canvas, name);
        Place(image.rectTransform, right ? Vector2.right : Vector2.zero,
            new Vector2(right ? -82 : 82, 80), new Vector2(96, 96));
        var button = image.GetComponent<Button>() ?? image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ApplyButton(button, "circle_normal", "circle_pressed");
        var icon = ChildImage(image.transform, "Icon");
        Place(icon.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 4), new Vector2(68, 68));
        ApplyImage(icon, iconName);
        icon.raycastTarget = false;
        return button;
    }

    private static Button ResultButton(Transform parent, string name, TMP_FontAsset font, int y)
    {
        var image = ChildImage(parent, name);
        Place(image.rectTransform, new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(270, 70));
        var button = image.GetComponent<Button>() ?? image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ApplyButton(button, "btn_blue_normal", "btn_blue_pressed");
        Text(image.transform, "Label", font, 28);
        return button;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, int size)
    {
        var child = parent.Find(name);
        var text = child != null ? child.GetComponent<TextMeshProUGUI>() :
            new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(12, 8);
        text.rectTransform.offsetMax = new Vector2(-12, -8);
        text.font = font;
        text.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "PlayText.mat");
        text.color = Color.white;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static Image ChildImage(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) return child.GetComponent<Image>();
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        return image;
    }

    private static RectTransform RectChild(Transform parent, string name)
    {
        if (parent.Find(name) is RectTransform existing) return existing;
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Material QuantityOutline(TMP_FontAsset font)
    {
        const string path = Root + "QuantityText.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(font.material) { name = "QuantityText" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_OutlineColor", new Color32(20, 38, 74, 255));
        material.SetFloat("_OutlineWidth", .18f);
        material.EnableKeyword("OUTLINE_ON");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EditPrefab(string name, Action<GameObject> edit)
    {
        var path = "Assets/Aset/UI/" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Sprite Sprite(string name) =>
        AssetDatabase.LoadAllAssetsAtPath(Root + "ui_sprites.png").OfType<Sprite>().Single(sprite => sprite.name == name);

    private static void ApplyImage(Image image, string name)
    {
        image.sprite = Sprite(name);
        image.color = Color.white;
        image.type = image.sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
    }

    private static void ApplyButton(Button button, string normal, string pressed)
    {
        ApplyImage((Image)button.targetGraphic, normal);
        button.transition = Selectable.Transition.SpriteSwap;
        var state = button.spriteState;
        state.pressedSprite = Sprite(pressed);
        button.spriteState = state;
    }
}
