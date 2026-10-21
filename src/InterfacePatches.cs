using System;
using DiceModders.Shared;

namespace DiceModders.Tweaks;

internal static class InterfacePatches
{
    private static object _logInterface;     // LogInterface instance, captured in OnEnable
    private static long _lastChatClear;
    private static long _lastLogClear;

    // ChatInterface.Awake fires once per fresh match/scene load, so it doubles as the
    // "new match" signal: drop anything captured from the previous match.
    internal static void ChatAwakePrefix()
    {
        _logInterface = null;
        History.StartSession();
    }

    internal static void LogEnablePostfix(object __instance) => _logInterface = __instance;

    internal static void LogDisablePostfix(object __instance)
    {
        if (ReferenceEquals(_logInterface, __instance)) _logInterface = null;
    }

    // Runs every frame while the match UI exists.
    internal static void ChatUpdatePostfix(object __instance)
    {
        bool chat = InterfaceTweaks.AutoClearChat.On, log = InterfaceTweaks.AutoClearLog.On;
        if (!chat && !log) return;

        long now = Environment.TickCount64;
        long interval = InterfaceTweaks.ClearIntervalMs.Value;
        try
        {
            if (chat && now - _lastChatClear >= interval)
            {
                _lastChatClear = now;
                Reflect.Call(__instance, "Clear");          // ChatInterface.Clear
            }
            if (log && _logInterface != null && now - _lastLogClear >= interval)
            {
                _lastLogClear = now;
                Reflect.Call(_logInterface, "Clear");       // LogInterface.Clear
            }
        }
        catch (Exception e)
        {
            ModPlugin.L.LogError("[auto-clear] " + e.Message);
            // switch both off so the error is not repeated every frame
            if (InterfaceTweaks.AutoClearChat.On) InterfaceTweaks.AutoClearChat.Flip();
            if (InterfaceTweaks.AutoClearLog.On) InterfaceTweaks.AutoClearLog.Flip();
        }
    }
}
