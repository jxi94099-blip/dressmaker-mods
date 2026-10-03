using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;
using HarmonyLib;

[BepInPlugin("local.dressmaker.onekeymannequin", "一键布片上人台", "1.3.0")]
public class OneKeyMannequin : BaseUnityPlugin
{
    ConfigEntry<KeyboardShortcut> shortcut;
    ConfigEntry<KeyboardShortcut> minus, recall, recycle;
    readonly Queue<PatternPanel> cutQueue = new Queue<PatternPanel>();
    readonly HashSet<PatternPanel> returning = new HashSet<PatternPanel>();
    Dress current;
    bool recallPending;
    bool recycledAtPointer;
    Vector2 lastRecyclePosition;
    static readonly System.Reflection.PropertyInfo busyProperty = AccessTools.Property(typeof(CuttingRoom), "BusyCutting");
    static readonly System.Reflection.FieldInfo animatingField = AccessTools.Field(typeof(PatternPanel), "animating");
    static bool BusyCutting(CuttingRoom room) { return (bool)busyProperty.GetValue(room, null); }
    static bool IsAnimating(PatternPanel panel) { return (bool)animatingField.GetValue(panel); }
    void Awake()
    {
        shortcut = Config.Bind("快捷键", "PlaceAll", new KeyboardShortcut(KeyCode.KeypadMultiply), "在人台界面将当前服装已裁剪、未放置的全部布片放到人台。");
        minus = Config.Bind("快捷键", "RecoverOrCut", new KeyboardShortcut(KeyCode.KeypadMinus), "人台长按回收鼠标碰到的布片；裁剪台按一下同时裁剪已摆好的纸样，每片保留剪刀动画。");
        recall = Config.Bind("快捷键", "RecallPatterns", new KeyboardShortcut(KeyCode.KeypadDivide), "回收裁剪台全部纸样；有裁剪动画时等当前布片剪完再回收。");
        recycle = Config.Bind("快捷键", "RecycleHovered", new KeyboardShortcut(KeyCode.KeypadPlus), "裁剪台长按，将鼠标碰到的台面或纸样栏里的已裁剪布片还原成纸样。");
        Logger.LogInfo("一键布片上人台 1.3.0：数字小键盘 * 放置，- 回收/多剪刀同时裁剪，/ 收回纸样，+ 只还原鼠标碰到的布片。");
    }
    public static bool ShouldPlace(bool cut, bool placed, bool stitched)
    {
        return cut && !placed && !stitched;
    }
    public static bool ShouldCut(bool world, bool cut, bool busy, bool animating, bool overlapping, bool onFabric)
    {
        return world && !cut && !busy && !animating && !overlapping && onFabric;
    }
    public static bool ShouldRecover(bool placed, bool busy, bool accessories)
    {
        return placed && !busy && !accessories;
    }
    public static bool ShouldRecall(bool world, bool busy, bool animating, bool placed)
    {
        return world && !busy && !animating && !placed;
    }
    public static bool ShouldRecycle(bool world, bool cut, bool busy, bool animating, bool attached, bool inventory)
    {
        return (world || inventory) && cut && !busy && !animating && !attached;
    }
    public static bool ShouldRecycleAgain(bool processed, float squaredMouseMovement)
    {
        return !processed || squaredMouseMovement >= 16f;
    }
    void LateUpdate()
    {
        var manager = GameManager.Instance;
        if (!manager) return;
        if (current != manager.activeDress || manager.CurrentScene != GameManager.Scene.CuttingRoom)
        { cutQueue.Clear(); recallPending = false; recycledAtPointer = false; current = manager.activeDress; }
        if (!recycle.Value.IsPressed()) recycledAtPointer = false;
        if (manager.IsPaused || manager.isInIntro) return;
        var dress = manager.activeDress;
        var inventory = SidebarInventory.Instance;
        if (!dress || dress.state == null || dress.state.finished || !inventory || !inventory.Interactable) return;
        try
        {
            if (manager.CurrentScene == GameManager.Scene.CuttingRoom) RunCutting(dress);
            else if (manager.CurrentScene == GameManager.Scene.Mannequin)
            {
                if (shortcut.Value.IsDown()) PlaceAll(manager, dress);
                else if (minus.Value.IsPressed() && !InputReader.PointerOverUI) RecoverHovered(manager, dress);
            }
        }
        catch (Exception ex) { cutQueue.Clear(); recallPending = false; Logger.LogError(ex); }
    }
    void RunCutting(Dress dress)
    {
        var room = CuttingRoom.Instance;
        var interactor = SharedPanelInteractor.Instance;
        if (!room || !interactor || interactor.IsDragging) return;
        bool recycling = recycle.Value.IsPressed();
        if (recycling) cutQueue.Clear();
        if (recall.Value.IsDown()) { cutQueue.Clear(); recallPending = true; }
        else if (!recycling && minus.Value.IsDown() && !recallPending && cutQueue.Count == 0 && !BusyCutting(room) && room.HasFabricPiece)
        {
            foreach (var panel in dress.PanelPieces)
                if (CanCut(panel, room)) cutQueue.Enqueue(panel);
            interactor.ClearSelection();
            if (room.FabricScissors) AccessTools.Method(typeof(FabricScissors), "ActivateScissors").Invoke(room.FabricScissors, new object[]{false});
            Logger.LogInfo("自动裁剪排队：" + cutQueue.Count);
        }
        if (BusyCutting(room)) return;
        if (recallPending)
        {
            recallPending = false;
            interactor.ClearSelection();
            var knob = PatternRotationKnob.Instance;
            if (knob) AccessTools.Method(typeof(PatternRotationKnob), "Hide").Invoke(knob, null);
            int count = 0;
            foreach (var panel in new List<PatternPanel>(dress.PanelPieces))
            {
                if (!panel || !ShouldRecall(panel.IsWorld3D, panel.beingCut, IsAnimating(panel), panel.PlacedOnMannequin)) continue;
                room.StartCoroutine((IEnumerator)AccessTools.Method(typeof(CuttingRoom), "ReturnToInventoryAnimation").Invoke(room, new object[]{panel}));
                count++;
            }
            Logger.LogInfo("收回裁剪台纸样：" + count);
            return;
        }
        if (recycling)
        {
            RecycleHovered(dress, interactor);
            return;
        }
        while (cutQueue.Count > 0)
        {
            var panel = cutQueue.Dequeue();
            if (!CanCut(panel, room)) continue;
            if (interactor.SelectedPanel) interactor.ClearSelection();
            room.CutOutFabric(panel);
        }
    }
    void RecycleHovered(Dress dress, SharedPanelInteractor interactor)
    {
        Vector2 pointer = Input.mousePosition;
        if (!ShouldRecycleAgain(recycledAtPointer, (pointer - lastRecyclePosition).sqrMagnitude)) return;
        PatternPanel panel;
        bool inInventory = InputReader.PointerOverUI;
        if (inInventory) panel = HoveredInventoryPanel(dress);
        else if (!interactor.TryGetHoveredPanel(out panel)) return;
        if (!panel || panel.dress != dress) return;
        if (!ShouldRecycle(panel.IsWorld3D, panel.HasBeenCut, panel.beingCut, IsAnimating(panel),
            panel.PlacedOnMannequin || panel.HasBeenStitched || dress.GetAccessoriesOnPanel(panel).Count > 0, inInventory)) return;
        if (interactor.SelectedPanel == panel) interactor.ClearSelection();
        var knob = PatternRotationKnob.Instance;
        if (knob) AccessTools.Method(typeof(PatternRotationKnob), "Hide").Invoke(knob, null);
        int index = inInventory ? panel.InventoryButton.transform.GetSiblingIndex() : -1;
        // Same state and representation changes as the game's DiscardBinUI.OnDrop.
        panel.UndoCut();
        // Preserve the hovered inventory slot, so another cloth cannot slide under the cursor.
        panel.ShowRepresentation2D(index);
        recycledAtPointer = true;
        lastRecyclePosition = pointer;
        Logger.LogInfo("布片还原成纸样：" + panel.DisplayName);
    }
    static PatternPanel HoveredInventoryPanel(Dress dress)
    {
        var inventory = SidebarInventory.Instance;
        var events = EventSystem.current;
        if (!inventory || inventory.CurrentTab != SidebarInventory.Tab.Pattern || !events) return null;
        var hits = new List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = Input.mousePosition }, hits);
        if (hits.Count == 0 || !hits[0].gameObject) return null;
        var target = hits[0].gameObject.transform;
        foreach (var panel in dress.PanelPieces)
        {
            if (!panel || !panel.InventoryButton || !panel.InventoryButton.gameObject.activeInHierarchy || !panel.InventoryButton.IsInteractable()) continue;
            // Icons are fake-parented separately from their inventory buttons.
            if (target.IsChildOf(panel.InventoryButton.transform) || (panel.icon && target.IsChildOf(panel.icon.transform))) return panel;
        }
        return null;
    }
    static bool CanCut(PatternPanel panel, CuttingRoom room)
    {
        return panel && room.HasFabricPiece && ShouldCut(panel.IsWorld3D, panel.HasBeenCut, panel.beingCut,
            IsAnimating(panel), panel.IsOverlapping, panel.lastPlacedOn == room.GetCurrentDisplayedFabricPiece());
    }
    void RecoverHovered(GameManager manager, Dress dress)
    {
        var scene = UnityEngine.Object.FindAnyObjectByType<MannequinScene>();
        var interactor = SharedPanelInteractor.Instance;
        if (!scene || !interactor || interactor.IsDragging || interactor.SelectedPanel || !manager.mainCamera) return;
        var hits = Physics.RaycastAll(manager.mainCamera.ScreenPointToRay(Input.mousePosition));
        Array.Sort(hits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
        foreach (var hit in hits)
        {
            var panel = (PatternPanel)AccessTools.Method(typeof(MannequinScene), "GetPatternFromMannequin").Invoke(scene, new object[]{hit.collider.gameObject});
            if (!panel || !panel.VisibleOnMannequin || !panel.PlacedOnMannequin) continue;
            // Only the visible top cloth is handled; never reach through a blocked cloth.
            if (returning.Contains(panel) || !ShouldRecover(panel.PlacedOnMannequin, panel.beingCut || IsAnimating(panel),
                dress.GetAccessoriesOnPanel(panel).Count > 0)) return;
            if (panel.HasBeenStitched) dress.UnsewPanel(panel);
            returning.Add(panel);
            StartCoroutine(ReturnMannequin(panel, scene, dress, hit.point));
            scene.UpdateScores();
            scene.UpdateSewButtons(true);
            var prompt = (GameObject)AccessTools.Field(typeof(MannequinScene), "finishDressPrompt").GetValue(scene);
            if (prompt) prompt.SetActive(false);
            return;
        }
    }
    IEnumerator ReturnMannequin(PatternPanel panel, MannequinScene scene, Dress dress, Vector3 point)
    {
        try
        {
            point.z = -.5f;
            panel.RemoveFromMannequinAnimated(point);
            var morph = AccessTools.Field(typeof(PatternPanel), "_morphRoutine");
            while (panel && morph.GetValue(panel) != null) yield return null;
            var manager = GameManager.Instance;
            if (panel && manager && manager.activeDress == dress && manager.CurrentScene == GameManager.Scene.Mannequin && !panel.PlacedOnMannequin)
            {
                panel.ShowRepresentation2D(-1);
                if (scene) { scene.UpdateScores(); scene.UpdateSewButtons(true); }
            }
        }
        finally { returning.Remove(panel); }
    }
    void PlaceAll(GameManager manager, Dress dress)
    {
        var scene = UnityEngine.Object.FindAnyObjectByType<MannequinScene>();
        if (!dress || dress.state == null || dress.state.finished || !scene) return;
        var interactor = (SharedPanelInteractor)AccessTools.Property(typeof(MannequinScene), "Interactor").GetValue(scene, null);
        if (!interactor || interactor.IsDragging || interactor.SelectedPanel) return;
        int count = 0;
        try
        {
            foreach (var panel in dress.PanelPieces)
            {
                if (!panel || panel.state == null || returning.Contains(panel) || !ShouldPlace(panel.HasBeenCut, panel.PlacedOnMannequin, panel.HasBeenStitched)) continue;
                panel.ShowRepresentationMannequin(false);
                if (panel.InventoryButton) panel.InventoryButton.gameObject.SetActive(false);
                count++;
            }
        }
        catch (Exception ex) { Logger.LogError(ex); }
        finally
        {
            if (count > 0)
            {
                scene.UpdateScores();
                manager.CheckDressCompletion();
                AccessTools.Method(typeof(MannequinScene), "Show").Invoke(scene, null);
                scene.UpdateSewButtons(true);
            }
            Logger.LogInfo("已放置布片：" + count);
        }
    }
}


