using System;

public static class GauntletHealthDropRules
{
    public const int ChestSecondHeartChancePercent = 25;
    public const int EnemyHeartChancePercent = 5;

    public static bool IsGauntletScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName)
            && sceneName.IndexOf("Gauntlet", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Rolls are integers in [0, 100), matching UnityEngine.Random.Range(0, 100).
    public static int GetChestHeartCount(int roll)
    {
        ValidateRoll(roll);
        return roll < ChestSecondHeartChancePercent ? 2 : 1;
    }

    public static int GetEnemyHeartCount(int roll)
    {
        ValidateRoll(roll);
        return roll < EnemyHeartChancePercent ? 1 : 0;
    }

    private static void ValidateRoll(int roll)
    {
        if (roll < 0 || roll >= 100)
        {
            throw new ArgumentOutOfRangeException(nameof(roll), roll, "Expected a roll from 0 through 99.");
        }
    }
}
