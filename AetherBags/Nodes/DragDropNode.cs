        using System;
        using System.Numerics;
        using AetherBags.Extensions;
        using AetherBags.Interop;
        using FFXIVClientStructs.FFXIV.Client.Enums;
        using FFXIVClientStructs.FFXIV.Client.UI;
        using FFXIVClientStructs.FFXIV.Client.UI.Agent;
        using FFXIVClientStructs.FFXIV.Component.GUI;
        using KamiToolKit.Classes;
        using KamiToolKit.Classes.Timelines;
        using KamiToolKit.Nodes;
        using Lumina.Text.ReadOnly;

        namespace AetherBags.Nodes;

        public unsafe class DragDropNode : ComponentNode<AtkComponentDragDrop, AtkUldComponentDataDragDrop> {

            // FIX: Manually expose the pointers that are 'internal' in KamiToolKit
            // We access the raw AtkComponentNode* via 'this.ResNode' and cast from there.
            private new AtkComponentDragDrop* Component => (AtkComponentDragDrop*)this.InternalComponentNode->Component;
            private new AtkUldComponentDataDragDrop* Data => (AtkUldComponentDataDragDrop*)Component->UldManager.ComponentData;

            public readonly ImageNode DragDropBackgroundNode;
            public readonly IconNode IconNode;

            public DragDropNode() {
                SetInternalComponentType(ComponentType.DragDrop);

                DragDropBackgroundNode = new SimpleImageNode {
                    NodeId = 3,
                    Size = new Vector2(44.0f, 44.0f),
                    TexturePath = "ui/uld/DragTargetA.tex",
                    TextureCoordinates = new Vector2(0.0f, 0.0f),
                    TextureSize = new Vector2(44.0f, 44.0f),
                    WrapMode = WrapMode.Tile,
                    NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents,
                };
                DragDropBackgroundNode.AttachNode(this);

                IconNode = new IconNode {
                    NodeId = 2,
                    Size = new Vector2(44.0f, 48.0f),
                    NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents,
                };
                IconNode.AttachNode(this);

                LoadTimelines();

                Data->Nodes[0] = IconNode.NodeId;

                AcceptedType = DragDropType.Everything;
                Payload = new DragDropPayload();

                // Use the fixed shadow struct for writing initial values if needed,
                // though direct field access on the struct usually works for simple fields.
                // However, to be safe with the VTable fix, we just set fields directly here
                // as they are standard offsets, or use the pointer.
                Component->AtkDragDropInterface.DragDropType = DragDropType.Everything;
                Component->AtkDragDropInterface.DragDropReferenceIndex = 0;

                InitializeComponentEvents();

                AddEvent(AtkEventType.DragDropBegin, DragDropBeginHandler);
                AddEvent(AtkEventType.DragDropInsert, DragDropInsertHandler);
                AddEvent(AtkEventType.DragDropDiscard, DragDropDiscardHandler);
                AddEvent(AtkEventType.DragDropClick, DragDropClickHandler);
                AddEvent(AtkEventType.DragDropRollOver, DragDropRollOverHandler);
                AddEvent(AtkEventType.DragDropRollOut, DragDropRollOutHandler);
            }

            private bool IsDragDropEndRegistered { get; set; }

            public Action<DragDropNode>? OnBegin { get; set; }
            public Action<DragDropNode>? OnEnd { get; set; }
            public Action<DragDropNode, DragDropPayload>? OnPayloadAccepted { get; set; }
            public Action<DragDropNode>? OnDiscard { get; set; }
            public Action<DragDropNode>? OnClicked { get; set; }
            public Action<DragDropNode>? OnRollOver { get; set; }
            public Action<DragDropNode>? OnRollOut { get; set; }

            public DragDropPayload Payload { get; set; }

            public uint IconId {
                get => IconNode.IconId;
                set {
                    IconNode.IconId = value;
                    IconNode.IsVisible = value != 0;
                }
            }

            public bool IsIconDisabled {
                get => IconNode.IsIconDisabled;
                set => IconNode.IsIconDisabled = value;
            }

            public int Quantity {
                get => int.Parse(Component->GetQuantityText().ToString());
                set => Component->SetQuantity(value);
            }

            public string QuantityString {
                get => Component->GetQuantityText().ToString();
                set => Component->SetQuantityText(value);
            }

            public DragDropType AcceptedType {
                get => Component->AcceptedType;
                set => Component->AcceptedType = value;
            }

            public AtkDragDropInterface.SoundEffectSuppression SoundEffectSuppression {
                get => Component->AtkDragDropInterface.DragDropSoundEffectSuppression;
                set => Component->AtkDragDropInterface.DragDropSoundEffectSuppression = value;
            }

            public bool IsDraggable {
                get => !Component->Flags.HasFlag(DragDropFlag.Locked);
                set {
                    if (value) {
                        Component->Flags &= ~DragDropFlag.Locked;
                    }
                    else {
                        Component->Flags |= DragDropFlag.Locked;
                    }
                }
            }

            public bool IsClickable {
                get => Component->Flags.HasFlag(DragDropFlag.Clickable);
                set {
                    if (value) {
                        Component->Flags |= DragDropFlag.Clickable;
                    }
                    else {
                        Component->Flags &= ~DragDropFlag.Clickable;
                    }
                }
            }

            private void DragDropBeginHandler(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData) {
                atkEvent->SetEventIsHandled();

                // FIX: Use extension method to write payload using fixed VTable
                Payload.ToFixedInterface(atkEventData->DragDropData.DragDropInterface);

                OnBegin?.Invoke(this);

                if (!IsDragDropEndRegistered) {
                    AddEvent(AtkEventType.DragDropEnd, DragDropEndHandler);
                    IsDragDropEndRegistered = true;
                }
            }

            public override ReadOnlySeString? Tooltip {
                get;
                set {
                    field = value;
                    switch (value) {
                        case { IsEmpty: false } when !TooltipRegistered:
                            AddEvent(AtkEventType.DragDropRollOver, ShowTooltip);
                            AddEvent(AtkEventType.DragDropRollOut, HideTooltip);

                            TooltipRegistered = true;
                            break;

                        case null when TooltipRegistered:
                            RemoveEvent(AtkEventType.DragDropRollOver, ShowTooltip);
                            RemoveEvent(AtkEventType.DragDropRollOut, HideTooltip);

                            TooltipRegistered = false;
                            break;
                    }
                }
            }

            private void DragDropInsertHandler(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData) {
                atkEvent->SetEventIsHandled();

                atkEvent->State.StateFlags |= AtkEventStateFlags.HasReturnFlags;
                atkEvent->State.ReturnFlags = 1;

                // FIX: Use extension method to read payload using fixed VTable
                var payload = DragDropPayloadExtensions.FromFixedInterface(atkEventData->DragDropData.DragDropInterface);

                Payload.Clear();
                IconId = 0;

                OnPayloadAccepted?.Invoke(this, payload);
            }

            private void DragDropDiscardHandler(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData) {
                atkEvent->SetEventIsHandled();

                atkEvent->State.StateFlags |= AtkEventStateFlags.HasReturnFlags;
                atkEvent->State.ReturnFlags = 1;

                OnDiscard?.Invoke(this);
            }

            private void DragDropEndHandler(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData) {
                atkEvent->SetEventIsHandled();

                // FIX: Cast to shadow struct to call the correct GetPayloadContainer (Index 12)
                var fixedInterface = (AtkDragDropInterfaceFixed*)atkEventData->DragDropData.DragDropInterface;
                fixedInterface->GetPayloadContainer()->Clear();

                OnEnd?.Invoke(this);

                if (IsDragDropEndRegistered) {
                    RemoveEvent(AtkEventType.DragDropEnd, DragDropEndHandler);
                    IsDragDropEndRegistered = false;
                }
            }

            private void DragDropClickHandler(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData) {
                atkEvent->SetEventIsHandled();

                atkEvent->State.StateFlags |= AtkEventStateFlags.HasReturnFlags;
                atkEvent->State.ReturnFlags = 1;

                OnClicked?.Invoke(this);
            }

            private void DragDropRollOverHandler()
                => OnRollOver?.Invoke(this);

            private void DragDropRollOutHandler()
                => OnRollOut?.Invoke(this);

            public void Clear() {
                Payload.Clear();
                IconId = 0;
            }

            public void ShowTooltip(AtkTooltipManager.AtkTooltipType type, ActionKind actionKind) {
                if (AtkStage.Instance()->DragDropManager.IsDragging) return;

                // FIX: Explicitly use 'this.ResNode' and cast to (AtkResNode*) to avoid ambiguity with the class name
                var addon = RaptureAtkUnitManager.Instance()->GetAddonByNode((AtkResNode*)this);
                if (addon is null) return;

                var tooltipArgs = new AtkTooltipManager.AtkTooltipArgs();
                tooltipArgs.Ctor();
                tooltipArgs.ActionArgs.Id = Payload.Int2;
                tooltipArgs.ActionArgs.Kind = (DetailKind)actionKind;

                AtkStage.Instance()->TooltipManager.ShowTooltip(
                    AtkTooltipManager.AtkTooltipType.Action,
                    addon->Id,
                    (AtkResNode*)this, // FIX: Explicit cast here as well
                    &tooltipArgs);
            }

            private void LoadTimelines() {
                AddTimeline(new TimelineBuilder()
                    .BeginFrameSet(1, 59)
                    .AddLabelPair(1, 10, 1)
                    .AddLabelPair(11, 19, 2)
                    .AddLabelPair(20, 29, 3)
                    .AddLabelPair(30, 39, 7)
                    .AddLabelPair(40, 49, 6)
                    .AddLabelPair(50, 59, 4)
                    .EndFrameSet()
                    .Build());
            }
        }