using Dalamud.Game.ClientState.Keys;

namespace AetherBags.Configuration;

public class KeybindSettings
{
    public bool SearchFocusEnabled { get; set; } = true;
    public Keybind SearchFocusKeybind { get; set; } = new Keybind
    {
        Key = VirtualKey.F,
        Modifiers = [VirtualKey.CONTROL],
    };

    public bool OpenBagsEnabled { get; set; }
    public Keybind OpenBagsKeybind { get; set; } = new Keybind
    {
        Key = VirtualKey.I,
        Modifiers = [],
    };

    public bool OpenSaddlebagsEnabled { get; set; }
    public Keybind OpenSaddlebagsKeybind { get; set; } = new Keybind
    {
        Key = VirtualKey.I,
        Modifiers = [VirtualKey.CONTROL, VirtualKey.SHIFT],
    };
}

