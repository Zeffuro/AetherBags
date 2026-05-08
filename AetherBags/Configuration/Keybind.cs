using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Keys;

namespace AetherBags.Configuration;

public class Keybind
{
    private static readonly VirtualKey[] ModifierKeys = [VirtualKey.CONTROL, VirtualKey.SHIFT, VirtualKey.MENU];

    public VirtualKey Key { get; init; } = VirtualKey.NO_KEY;

    public HashSet<VirtualKey> Modifiers { get; init; } = [];

    public bool IsPressed(bool exactModifiers = false)
    {
        if (Key == VirtualKey.NO_KEY || !Services.KeyState.IsVirtualKeyValid(Key) || !Services.KeyState[(int)Key]) return false;

        foreach (var modifier in Modifiers)
        {
            if (!Services.KeyState.IsVirtualKeyValid(modifier) || !Services.KeyState[(int)modifier]) return false;
        }

        if (exactModifiers)
        {
            foreach (var modifier in ModifierKeys)
            {
                if (!Modifiers.Contains(modifier) && Services.KeyState.IsVirtualKeyValid(modifier) && Services.KeyState[(int)modifier]) return false;
            }
        }

        return true;
    }

    public void Reset()
    {
        if (Key == VirtualKey.NO_KEY || !Services.KeyState.IsVirtualKeyValid(Key)) return;

        Services.KeyState[(int)Key] = false;
    }

    public override string ToString()
    {
        if (Key == VirtualKey.NO_KEY) return "None";

        return string.Join(" + ", Modifiers.OrderByModifier().Append(Key).Select(key => key.GetDisplayName()));
    }
}

public static class VirtualKeyExtensions
{
    public static bool IsModifier(this VirtualKey key) => key is VirtualKey.MENU or VirtualKey.SHIFT or VirtualKey.CONTROL;

    public static bool IsMouseButton(this VirtualKey key) => (int)key is 0x01 or 0x02 or 0x04 or 0x05 or 0x06;

    public static bool IsKey(this VirtualKey key) => key != VirtualKey.NO_KEY && !key.IsModifier() && !key.IsMouseButton();

    public static bool IsBindable(this VirtualKey key) => key.IsModifier() || key.IsKey();

    public static string GetDisplayName(this VirtualKey key) => key switch
    {
        VirtualKey.CONTROL => "Ctrl",
        VirtualKey.SHIFT => "Shift",
        VirtualKey.MENU => "Alt",
        VirtualKey.NO_KEY => "None",
        _ => key.ToString()
    };

    public static IOrderedEnumerable<VirtualKey> OrderByModifier(this IEnumerable<VirtualKey> keys)
    {
        return keys.OrderBy(key => key switch
        {
            VirtualKey.CONTROL => 0,
            VirtualKey.SHIFT => 1,
            VirtualKey.MENU => 2,
            _ => 3
        });
    }
}