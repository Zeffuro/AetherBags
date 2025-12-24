using AetherBags.Interop;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using Lumina.Text.ReadOnly;
using Lumina.Text;

namespace AetherBags.Extensions;

// TODO: Remove this when CS is merged into Dalamud.
public static unsafe class DragDropPayloadExtensions
{
    public static DragDropPayload FromFixedInterface(AtkDragDropInterface* dragDropInterface)
    {
        // Cast to our manual fixed struct
        var fixedInterface = (AtkDragDropInterfaceFixed*)dragDropInterface;

        // Calls Index 12
        var payloadContainer = fixedInterface->GetPayloadContainer();

        return new DragDropPayload
        {
            Type = fixedInterface->DragDropType,
            ReferenceIndex = fixedInterface->DragDropReferenceIndex,
            Int1 = payloadContainer->Int1,
            Int2 = payloadContainer->Int2,
            Text = new ReadOnlySeString(payloadContainer->Text),
        };
    }

    public static void ToFixedInterface(this DragDropPayload payload, AtkDragDropInterface* dragDropInterface, bool writeToPayloadContainer = true)
    {
        var fixedInterface = (AtkDragDropInterfaceFixed*)dragDropInterface;

        fixedInterface->DragDropType = payload.Type;
        fixedInterface->DragDropReferenceIndex = payload.ReferenceIndex;

        if (writeToPayloadContainer)
        {
            // Calls Index 12
            var payloadContainer = fixedInterface->GetPayloadContainer();

            payloadContainer->Clear();
            payloadContainer->Int1 = payload.Int1;
            payloadContainer->Int2 = payload.Int2;

            if (payload.Text.IsEmpty)
            {
                payloadContainer->Text.Clear();
            }
            else
            {
                var stringBuilder = new SeStringBuilder().Append(payload.Text);
                payloadContainer->Text.SetString(stringBuilder.GetViewAsSpan());
            }
        }
    }
}