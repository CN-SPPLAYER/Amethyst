namespace Amethyst;

internal static class AmethystTextVisibility
{
    internal static bool For(AmethystPlayerCache cache)
    {
        if (cache == null) return false;
        if (cache.Id == AmethystPlayerCacheStore.LocalId) return !cache.InVent;
        if (!AmethystPlayerCacheStore.LocalDead && cache.Dead) return false;
        return true;
    }
}
