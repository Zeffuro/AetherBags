using AetherBags.Inventory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Inventory;

internal static unsafe class InventoryDragPreview
{
    private static CustomEventListener? _listener;
    private static InventoryDragDropNodeBase? _source;
    private static AtkEventListener* _sourceListener;
    private static AtkComponentNode* _previewNode;
    private static uint _eventParam;
    private static int _callbackDepth;
    private static bool _disposeRequested;

    public static void Initialize() => _disposeRequested = false;

    public static void Begin(InventoryDragDropNodeBase source, ReadOnlySeString quantity)
    {
        if (_disposeRequested || _source != null) return;

        var beginEvent = source.Node->AtkResNode.AtkEventManager.Event;
        while (beginEvent != null && beginEvent->State.EventType != AtkEventType.DragDropBegin)
            beginEvent = beginEvent->NextEvent;
        if (beginEvent == null || beginEvent->Listener == null) return;

        _listener ??= new CustomEventListener(OnPreviewEvent);
        var preview = Experimental.CreateDragDropPreview(&source.Component->AtkDragDropInterface);
        if (preview == null || preview->OwnerNode == null) return;

        _source = source;
        _sourceListener = beginEvent->Listener;
        _eventParam = beginEvent->Param;
        _previewNode = preview->OwnerNode;

        try
        {
            preview->LoadIcon(source.IconId);
            preview->SetQuantityText(quantity);
            if (preview->AtkComponentIcon != null)
                preview->AtkComponentIcon->SetFrameLabelId(source.IconNode.Component->GetFrameLabelId());

            ref var events = ref _previewNode->AtkResNode.AtkEventManager;
            var target = &_previewNode->AtkResNode.AtkEventTarget;
            events.RegisterEvent(AtkEventType.DragDropEnd, _eventParam, null, target, _listener, false);
            events.RegisterEvent(AtkEventType.DragDropDiscard, _eventParam, null, target, _listener, false);
        }
        catch
        {
            ClearBridge();
            var stage = AtkStage.Instance();
            if (stage != null && stage->DragDropManager.DragDrop2 == &source.Component->AtkDragDropInterface)
                stage->DragDropManager.CancelDragDrop(false, true);
            throw;
        }
    }

    public static void CancelForAddon(AtkUnitBase* addon)
    {
        var source = _source;
        if (addon == null || source == null || source.Node == null) return;
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null || manager->GetAddonByNode(source) != addon) return;

        var stage = AtkStage.Instance();
        if (stage != null && stage->DragDropManager.DragDrop2 == &source.Component->AtkDragDropInterface
            && stage->DragDropManager.DragDropS != null && stage->DragDropManager.IsDragging)
            stage->DragDropManager.CancelDragDrop(false, true);

        if (ReferenceEquals(_source, source)) ClearBridge();
    }

    public static void ReleaseSource(InventoryDragDropNodeBase source)
    {
        if (ReferenceEquals(_source, source)) ClearBridge();
        if (source.Node == null || source.Component == null) return;

        var stage = AtkStage.Instance();
        if (stage == null) return;
        ref var manager = ref stage->DragDropManager;
        var sourceInterface = &source.Component->AtkDragDropInterface;
        if (manager.DragDrop2 != sourceInterface) return;

        manager.DragDrop2 = null;
        if (manager.IsDragging && manager.DragDropS != null)
            manager.CancelDragDrop(false, true);
        if (manager.DragDrop1 == sourceInterface)
            manager.DragDrop1 = null;
    }

    public static void Dispose()
    {
        if (_disposeRequested && _listener == null) return;
        _disposeRequested = true;
        if (_callbackDepth != 0)
        {
            IFramework.Get().RunOnTick(() =>
            {
                if (_disposeRequested) Dispose();
            });
            return;
        }

        if (_source is { } source) ReleaseSource(source);
        ClearBridge();
        var listener = _listener;
        _listener = null;
        listener?.Dispose();
    }

    private static void OnPreviewEvent(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam,
        AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        if (_source == null || _sourceListener == null
            || eventType is not (AtkEventType.DragDropEnd or AtkEventType.DragDropDiscard)) return;

        var sourceListener = _sourceListener;
        int sourceParam = unchecked((int)_eventParam);
        if (eventType == AtkEventType.DragDropEnd) ClearBridge();

        _callbackDepth++;
        try
        {
            sourceListener->ReceiveEvent(eventType, sourceParam, atkEvent, atkEventData);
        }
        finally
        {
            _callbackDepth--;
        }
    }

    private static void ClearBridge()
    {
        var previewNode = _previewNode;
        uint eventParam = _eventParam;
        _source = null;
        _sourceListener = null;
        _previewNode = null;
        _eventParam = 0;
        if (previewNode == null || _listener == null) return;

        ref var events = ref previewNode->AtkResNode.AtkEventManager;
        events.UnregisterEvent(AtkEventType.DragDropEnd, eventParam, _listener, false);
        events.UnregisterEvent(AtkEventType.DragDropDiscard, eventParam, _listener, false);
    }
}
