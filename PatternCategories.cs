using System;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HarmonyLib;

[BepInPlugin("local.dressmaker.patterncategories", "纸样部位分类", "1.0.5")]
public class PatternCategories : BaseUnityPlugin
{
    // ponytail: classify the game's English identifiers; update rules for new named parts.
    class Entry
    {
        public PatternPanel panel;
        public CanvasGroup buttonGroup, iconGroup;
        public LayoutElement[] layouts;
        public bool[] originalIgnore;
        public bool ownsLayout;
        public TextMeshProUGUI label;
        public string original;
        public TMP_FontAsset originalFont;
        public int category;
        public int directionMask;
        public bool? hidden;
    }
    readonly Dictionary<PatternPanel, Entry> entries = new Dictionary<PatternPanel, Entry>();
    GameObject root;
    readonly Button[] tabs = new Button[8];
    readonly Button[] directionTabs = new Button[5];
    GameObject directionMenu;
    Button directionToggle;
    int direction;
    bool directionExpanded;
    Dress current;
    int selected;
    TMP_FontAsset font;
    GameObject menu;
    Button toggle;
    bool expanded;
    bool ownsFont;
    Font systemFont;
    bool resetScroll;

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        var inventory = SidebarInventory.Instance;
        bool visible = gm && !gm.IsPaused && !gm.isInIntro && gm.activeDress && inventory && inventory.gameObject.activeInHierarchy
            && inventory.CurrentTab == SidebarInventory.Tab.Pattern
            && (gm.CurrentScene == GameManager.Scene.CuttingRoom || gm.CurrentScene == GameManager.Scene.Mannequin);
        if (!visible) { Restore(); expanded = directionExpanded = false; if (root) root.SetActive(false); return; }
        if (current != gm.activeDress) { ClearEntries(); current = gm.activeDress; selected = direction = 0; }
        foreach (var panel in current.PanelPieces)
        {
            if (!panel || !panel.InventoryButton || entries.ContainsKey(panel)) continue;
            var label = (TextMeshProUGUI)AccessTools.Field(typeof(PatternPanel), "iconText").GetValue(panel);
            if (!label) continue;
            var entry = new Entry { panel = panel, label = label, original = label.text, originalFont = label.font,
                category = PatternClassifier.Classify(panel.definition.prettyName, panel.garmentComponent.type.ToString()),
                directionMask = PatternClassifier.DirectionMask(panel.definition.prettyName, (int)panel.Zones) };
            // Dedicated child groups avoid replacing existing UI settings.
            entry.buttonGroup = panel.InventoryButton.gameObject.AddComponent<CanvasGroup>();
            if (panel.icon) entry.iconGroup = panel.icon.gameObject.AddComponent<CanvasGroup>();
            entry.layouts = panel.InventoryButton.GetComponents<LayoutElement>();
            if (entry.layouts.Length == 0) { entry.layouts = new[]{panel.InventoryButton.gameObject.AddComponent<LayoutElement>()}; entry.ownsLayout = true; }
            entry.originalIgnore = new bool[entry.layouts.Length];
            for (int i = 0; i < entry.layouts.Length; i++) entry.originalIgnore[i] = entry.layouts[i].ignoreLayout;
            entries.Add(panel, entry);
        }
        if (!font) font = FindChineseFont();
        if (!font) return;
        if (!root) Build();
        root.SetActive(true);
        Position(inventory);
        if (Input.GetMouseButtonDown(0) && (expanded || directionExpanded))
        {
            var openMenu = expanded ? menu : directionMenu;
            bool insideMenu = RectTransformUtility.RectangleContainsScreenPoint(openMenu.transform as RectTransform, Input.mousePosition, null);
            bool insideToggle = RectTransformUtility.RectangleContainsScreenPoint(toggle.transform as RectTransform, Input.mousePosition, null)
                || RectTransformUtility.RectangleContainsScreenPoint(directionToggle.transform as RectTransform, Input.mousePosition, null);
            if (PatternClassifier.ShouldCloseMenu(insideMenu, insideToggle)) expanded = directionExpanded = false;
        }
        menu.SetActive(expanded);
        directionMenu.SetActive(directionExpanded);
        int[] counts = new int[8];
        foreach (var e in entries.Values)
        {
            if (!e.panel || !e.panel.InventoryButton) continue;
            bool available = e.panel.InventoryButton.gameObject.activeInHierarchy;
            if (available && PatternClassifier.Matches(e.category, e.directionMask, 0, direction)) { counts[0]++; counts[e.category]++; }
        }
        int[] directionCounts = new int[5];
        foreach (var e in entries.Values)
        {
            if (!e.panel || !e.panel.InventoryButton || !e.panel.InventoryButton.gameObject.activeInHierarchy) continue;
            for (int i = 0; i < directionTabs.Length; i++)
                if (PatternClassifier.Matches(e.category, e.directionMask, selected, i)) directionCounts[i]++;
        }
        for (int i = 0; i < directionTabs.Length; i++)
        {
            directionTabs[i].gameObject.SetActive(i == 0 || i == direction || directionCounts[i] > 0);
            SetCaption(directionTabs[i], PatternClassifier.Directions[i] + " (" + directionCounts[i] + ")");
            directionTabs[i].image.color = i == direction ? new Color(.23f,.45f,.53f,.98f) : new Color(.15f,.18f,.23f,.98f);
        }
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].gameObject.SetActive(i == 0 || i == selected || counts[i] > 0);
            SetCaption(tabs[i], PatternClassifier.Categories[i] + " (" + counts[i] + ")");
            tabs[i].image.color = i == selected ? new Color(.23f,.45f,.53f,.98f) : new Color(.15f,.18f,.23f,.98f);
        }
        foreach (var e in entries.Values)
        {
            if (!e.panel) continue;
            bool hide = !PatternClassifier.Matches(e.category, e.directionMask, selected, direction);
            SetHidden(e, hide);
            if (e.label.font != font) e.label.font = font;
            string caption = PatternClassifier.Categories[e.category] + " · " + e.panel.DisplayName + "\n" + (e.panel.HasBeenCut ? "已裁剪" : "未裁剪");
            if (e.label.text != caption) e.label.text = caption;
        }
        if (resetScroll)
        {
            var parent = (Transform)AccessTools.Field(typeof(SidebarInventory), "patternParent").GetValue(inventory);
            var content = parent as RectTransform;
            if (content) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            var scroll = parent.GetComponentInParent<ScrollRect>();
            if (scroll) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; }
            resetScroll = false;
        }
    }
    static void SetHidden(Entry e, bool hide)
    {
        if (e.hidden == hide) return;
        e.hidden = hide;
        if (e.buttonGroup) { e.buttonGroup.alpha = hide ? 0 : 1; e.buttonGroup.blocksRaycasts = !hide; e.buttonGroup.interactable = !hide; }
        if (e.iconGroup) { e.iconGroup.alpha = hide ? 0 : 1; e.iconGroup.blocksRaycasts = !hide; }
        for (int i = 0; i < e.layouts.Length; i++) if (e.layouts[i]) e.layouts[i].ignoreLayout = hide || e.originalIgnore[i];
    }
    static void SetCaption(Button button, string caption)
    {
        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label.text != caption) label.text = caption;
    }
    void Restore()
    {
        foreach (var e in entries.Values) { SetHidden(e, false); if (e.label) { e.label.text = e.original; e.label.font = e.originalFont; } }
    }
    void Build()
    {
        root = new GameObject("Pattern category bar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
        toggle = MakeButton(root.transform, "分类", 44, 34);
        toggle.onClick.AddListener(delegate { expanded = !expanded; directionExpanded = false; });
        var bar = new GameObject("Categories", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        menu = bar;
        bar.transform.SetParent(root.transform, false);
        var rect = bar.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.pivot = new Vector2(0,1);
        bar.GetComponent<Image>().color = new Color(.1f,.12f,.15f,.96f);
        var group = bar.GetComponent<VerticalLayoutGroup>(); group.padding = new RectOffset(6,6,6,6); group.spacing = 4; group.childControlWidth = group.childControlHeight = true; group.childForceExpandWidth = group.childForceExpandHeight = false;
        var fit = bar.GetComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (int i in new[]{0,4,1,2,3,5,6,7})
        {
            int category = i;
            tabs[i] = MakeButton(bar.transform, PatternClassifier.Categories[i], 210, 34);
            tabs[i].onClick.AddListener(delegate { selected = category; resetScroll = true; });
        }
        menu.SetActive(false);
        directionToggle = MakeButton(root.transform, "方位", 44, 34);
        directionToggle.onClick.AddListener(delegate { directionExpanded = !directionExpanded; expanded = false; });
        directionMenu = Instantiate(menu, root.transform, false);
        directionMenu.name = "Directions";
        foreach (Transform child in directionMenu.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        for (int i = 0; i < directionTabs.Length; i++)
        {
            int choice = i;
            directionTabs[i] = MakeButton(directionMenu.transform, PatternClassifier.Directions[i],210,34);
            directionTabs[i].onClick.AddListener(delegate { direction = choice; resetScroll = true; });
        }
    }
    Button MakeButton(Transform parent, string caption, float width, float height)
    {
        var obj = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(width,height);
        var layout = obj.GetComponent<LayoutElement>(); layout.preferredWidth = width; layout.preferredHeight = height;
        var button = obj.GetComponent<Button>(); button.targetGraphic = obj.GetComponent<Image>(); button.image.color = new Color(.15f,.18f,.23f,.98f);
        var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); textObj.transform.SetParent(obj.transform,false);
        var tr = textObj.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        var text = textObj.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = caption; text.fontSize = 18; text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        return button;
    }
    TMP_FontAsset FindChineseFont()
    {
        const string required = "分类方位前后左右侧全部上身裙袖子口领腰带头装饰辅片其他";
        foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            bool supported = true;
            foreach (char c in required) if (!asset.HasCharacter(c, true, true)) { supported = false; break; }
            if (supported) { Logger.LogInfo("分类中文字体：" + asset.name); return asset; }
        }
        systemFont = Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei", "SimHei", "SimSun"},24);
        var result = TMP_FontAsset.CreateFontAsset(systemFont);
        ownsFont = true;
        Logger.LogInfo("分类使用系统中文字体：" + systemFont.name);
        return result;
    }
    void Position(SidebarInventory inventory)
    {
        var tab = (Component)AccessTools.Field(typeof(SidebarInventory), "patternTab").GetValue(inventory);
        var tabRect = tab.transform as RectTransform;
        var sidebarRect = inventory.transform as RectTransform;
        if (!tabRect || !sidebarRect) return;
        var canvas = inventory.GetComponentInParent<Canvas>();
        Camera camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector3[] corners = new Vector3[4]; sidebarRect.GetWorldCorners(corners);
        var left = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        var centre = RectTransformUtility.WorldToScreenPoint(camera, tabRect.TransformPoint(tabRect.rect.center));
        var rootRect = root.transform as RectTransform;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, new Vector2(left.x,centre.y),null,out local);
        var tr = toggle.transform as RectTransform; tr.anchorMin = tr.anchorMax = new Vector2(.5f,.5f); tr.anchoredPosition = local + new Vector2(28,0);
        var mr = menu.transform as RectTransform; mr.anchoredPosition = local + new Vector2(6,-24);
        var dt = directionToggle.transform as RectTransform; dt.anchorMin = dt.anchorMax = new Vector2(.5f,.5f); dt.anchoredPosition = local + new Vector2(78,0);
        (directionMenu.transform as RectTransform).anchoredPosition = local + new Vector2(6,-24);
    }
    void OnDestroy()
    {
        ClearEntries();
        if (root) Destroy(root);
        if (ownsFont && font) { if(font.material) Destroy(font.material); if(font.atlasTexture) Destroy(font.atlasTexture); Destroy(font); }
        if (systemFont) Destroy(systemFont);
    }
    void ClearEntries()
    {
        Restore();
        foreach (var e in entries.Values) { if(e.buttonGroup) Destroy(e.buttonGroup); if(e.iconGroup) Destroy(e.iconGroup); if(e.ownsLayout && e.layouts[0]) Destroy(e.layouts[0]); }
        entries.Clear();
    }
}
