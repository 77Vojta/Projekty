using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Ovládání z klávesnice. Funguje s novým Input Systemem i se starým Input Managerem.
/// </summary>
public static class Keys
{
    public static bool Left => Pressed(KeyCode.LeftArrow) || Pressed(KeyCode.A);
    public static bool Right => Pressed(KeyCode.RightArrow) || Pressed(KeyCode.D);
    public static bool Jump => Pressed(KeyCode.UpArrow) || Pressed(KeyCode.W) || Pressed(KeyCode.Space);
    public static bool Slide => Pressed(KeyCode.DownArrow) || Pressed(KeyCode.S);
    public static bool Start => Jump || Pressed(KeyCode.Return);
    public static bool Restart => Start || Pressed(KeyCode.R);
    public static bool Quit => Pressed(KeyCode.Escape);

    static bool Pressed(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        switch (key)
        {
            case KeyCode.LeftArrow: return kb.leftArrowKey.wasPressedThisFrame;
            case KeyCode.RightArrow: return kb.rightArrowKey.wasPressedThisFrame;
            case KeyCode.UpArrow: return kb.upArrowKey.wasPressedThisFrame;
            case KeyCode.DownArrow: return kb.downArrowKey.wasPressedThisFrame;
            case KeyCode.A: return kb.aKey.wasPressedThisFrame;
            case KeyCode.D: return kb.dKey.wasPressedThisFrame;
            case KeyCode.W: return kb.wKey.wasPressedThisFrame;
            case KeyCode.S: return kb.sKey.wasPressedThisFrame;
            case KeyCode.R: return kb.rKey.wasPressedThisFrame;
            case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
            case KeyCode.Return: return kb.enterKey.wasPressedThisFrame;
            case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
            default: return false;
        }
#else
        return Input.GetKeyDown(key);
#endif
    }
}
