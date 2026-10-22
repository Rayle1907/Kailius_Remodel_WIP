using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class GauntletEarnings : MonoBehaviour
{
    [SerializeField] private int coins;
    [SerializeField] private int gems;

    public int Coins => coins;
    public int Gems => gems;
    public event Action Changed;

    public static GauntletEarnings ForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || !GauntletHealthDropRules.IsGauntletScene(scene.name)) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GauntletEarnings earnings = root.GetComponentInChildren<GauntletEarnings>(true);
            if (earnings != null) return earnings;
        }
        return null;
    }

    public void RecordCollection(int coinValue, int gemValue)
    {
        if (coinValue <= 0 && gemValue <= 0) return;
        coins += Mathf.Max(0, coinValue);
        gems += Mathf.Max(0, gemValue);
        Changed?.Invoke();
    }
}
