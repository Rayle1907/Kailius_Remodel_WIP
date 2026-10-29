using System;

public static class GauntletHealthDropRules
{
    public static bool IsGauntletScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName)
            && sceneName.IndexOf("Gauntlet", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static int GetChestHeartCount(bool isSectionRecoveryChest)
    {
        return isSectionRecoveryChest ? 1 : 0;
    }
}
