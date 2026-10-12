#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helpers for regenerating designer loading / start-race UI templates.
/// Prefer editing Assets/Resources/LoadingScreen.prefab and the Start Race Screen
/// under Assets/Prefab/UI.prefab directly.
/// </summary>
public static class UiDesignBootstrap
{
    const string LoadingPrefabPath = "Assets/Resources/LoadingScreen.prefab";

    [MenuItem("Racing/UI/Select Loading Screen Prefab")]
    static void SelectLoadingScreen()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingPrefabPath);
        if (asset == null)
        {
            EditorUtility.DisplayDialog(
                "Loading Screen",
                "Missing Assets/Resources/LoadingScreen.prefab.\nUse Racing/UI/Recreate Loading Screen Prefab.",
                "OK");
            return;
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    [MenuItem("Racing/UI/Recreate Loading Screen Prefab")]
    static void RecreateLoadingScreen()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var root = new GameObject("LoadingScreen", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(LoadingScreenView));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var panel = CreateUiChild(root.transform, "Panel");
        Stretch(panel);
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.05f, 1f);

        var status = CreateTmp(panel, "Status", new Vector2(0.5f, 0.55f), 36, "Loading...");
        var percent = CreateTmp(panel, "Percent", new Vector2(0.5f, 0.45f), 28, "0%");
        var slider = CreateProgressSlider(panel);

        var view = root.GetComponent<LoadingScreenView>();
        var so = new SerializedObject(view);
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.FindProperty("progressBar").objectReferenceValue = slider;
        so.FindProperty("progressText").objectReferenceValue = percent;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, LoadingPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(LoadingPrefabPath));
        Debug.Log($"[UiDesignBootstrap] Wrote {LoadingPrefabPath}. Edit this prefab for loading screen art.");
    }

    [MenuItem("Racing/UI/Add Start Race Screen To Selected UI")]
    static void AddStartRaceToSelectedUi()
    {
        var ui = Selection.activeGameObject;
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Start Race", "Select the UI root (with UIManager) in the prefab or scene.", "OK");
            return;
        }

        var uiManager = ui.GetComponent<UIManager>();
        if (uiManager == null)
        {
            EditorUtility.DisplayDialog("Start Race", "Selected object needs a UIManager.", "OK");
            return;
        }

        Transform existing = ui.transform.Find("Start Race Screen");
        GameObject panelGo;
        if (existing != null)
        {
            panelGo = existing.gameObject;
        }
        else
        {
            panelGo = CreateUiChild(ui.transform, "Start Race Screen").gameObject;
            Stretch(panelGo.GetComponent<RectTransform>());
            var dim = panelGo.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.45f);

            var buttonRt = CreateUiChild(panelGo.transform, "StartRaceButton");
            buttonRt.anchorMin = buttonRt.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRt.sizeDelta = new Vector2(320f, 70f);
            buttonRt.anchoredPosition = Vector2.zero;
            var buttonImage = buttonRt.gameObject.AddComponent<Image>();
            buttonImage.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            buttonRt.gameObject.AddComponent<Button>().targetGraphic = buttonImage;

            var label = CreateTmp(buttonRt, "Label", new Vector2(0.5f, 0.5f), 36, "START RACE");
            Stretch(label.rectTransform);
        }

        panelGo.SetActive(false);
        var button = panelGo.GetComponentInChildren<Button>(true);

        Undo.RecordObject(uiManager, "Wire Start Race Panel");
        uiManager.startRacePanel = panelGo;
        uiManager.startRaceButton = button;
        EditorUtility.SetDirty(uiManager);
        if (PrefabUtility.IsPartOfPrefabInstance(ui))
            PrefabUtility.RecordPrefabInstancePropertyModifications(uiManager);

        Selection.activeGameObject = panelGo;
        Debug.Log("[UiDesignBootstrap] Start Race Screen ready — design it under the UI prefab.");
    }

    static RectTransform CreateUiChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, Vector2 anchor, float size, string text)
    {
        var rt = CreateUiChild(parent, name);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 60f);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    static Slider CreateProgressSlider(Transform parent)
    {
        var rt = CreateUiChild(parent, "Progress");
        rt.anchorMin = new Vector2(0.25f, 0.35f);
        rt.anchorMax = new Vector2(0.75f, 0.38f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var fillArea = CreateUiChild(rt, "Fill Area");
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(4, 4);
        fillArea.offsetMax = new Vector2(-4, -4);

        var fill = CreateUiChild(fillArea, "Fill");
        Stretch(fill);
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        var slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.targetGraphic = fillImage;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;
        return slider;
    }
}
#endif
