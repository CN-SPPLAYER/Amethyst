namespace Amethyst;

internal static class AmethystSecurityNotify
{
    private static bool On => AmethystConfig.SecurityNotify.Value;

    internal static void Suspect(string who, AmethystNotifyKind kind = AmethystNotifyKind.Warning)
    {
        Fire("AbnormalRPC", kind, who);
    }

    internal static void Fire(string key, AmethystNotifyKind kind = AmethystNotifyKind.Danger, params object[] args)
    {
        string msg = AmethystText.T(key, args);
        try { AmethystEventLog.Add(msg, kind); } catch { }
        if (!On) return;
        try { AmethystToast.Push(AmethystText.T("Guard_2"), msg, 5.0f, kind); } catch { }
    }
}
