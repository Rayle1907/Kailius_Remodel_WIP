using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;


public class Chest : MonoBehaviour {
    public event System.Action<Chest> Opened;
    public bool IsOpened => !open;
    public GameObject coins;
    public GameObject gems;
    public GameObject hearts;
    public GameObject sword;
    public GameObject shield;
    public Sprite openChestSprite;

    public int maxCoins = 11;
    public int maxGems = 6;
    public int maxHearts = 3;
    public int maxSwords = 2;
    public int maxShields = 2;
    [SerializeField] private string rewardSourceId;
    [SerializeField] private bool isSectionRecoveryChest;

    private bool range = false;
    private bool open = true;

    private void Awake() {
        if (!OpenWorldChestProgress.IsOpenWorldScene(gameObject.scene.name)) return;
        if (string.IsNullOrWhiteSpace(rewardSourceId)) {
            open = false;
            Debug.LogError("Open-world chest is missing a stable reward ID. Assign chest IDs in the editor.", this);
            return;
        }
        if (OpenWorldChestProgress.IsOpened(gameObject.scene.path, rewardSourceId)) {
            open = false;
            gameObject.GetComponent<SpriteRenderer>().sprite = openChestSprite;
        }
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.E) && range && open) {
            openChest();
        }
    }

    public void generar() {
        openChest();
    }

    private void SpawnRewards() {
        int numCoins = Random.Range(1, maxCoins);
        int numGems = Random.Range(1, maxGems);
        int numHearts = GauntletHealthDropRules.IsGauntletScene(gameObject.scene.name)
            ? GauntletHealthDropRules.GetChestHeartCount(isSectionRecoveryChest)
            : OpenWorldChestProgress.IsOpenWorldScene(gameObject.scene.name) ? 1 : Random.Range(1, maxHearts);
        bool canDropUpgrades = GauntletProgress.TrySpendUpgrade(gameObject.scene.name, rewardSourceId);
        int numSwords = canDropUpgrades ? Random.Range(0, maxSwords) : 0;
        int numShields = canDropUpgrades ? Random.Range(0, maxShields) : 0;

        for (int i = 0; i < numCoins; i++) {
            SpawnReward(coins, new Vector3(gameObject.transform.position.x -1.0f, gameObject.transform.position.y + 3.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; i < numGems; i++) {
            SpawnReward(gems, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 3.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; hearts != null && i < numHearts; i++) {
            SpawnReward(hearts, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 3.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; i < numSwords; i++) {
            SpawnReward(sword, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 3.0f, gameObject.transform.position.z), Quaternion.identity);
        }

        for (int i = 0; i < numShields; i++) {
            SpawnReward(shield, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 3.0f, gameObject.transform.position.z), Quaternion.identity);
        }

    }

    private void SpawnReward(GameObject prefab, Vector3 position, Quaternion rotation) {
        GameObject reward = Instantiate(prefab, position, rotation);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(reward, gameObject.scene);
        OptionalLootProgress progress = OptionalLootProgress.ForScene(gameObject.scene);
        Coin coin = reward.GetComponent<Coin>();
        Gem gem = reward.GetComponent<Gem>();
        if (coin != null) coin.SetChestLootOwner(progress);
        if (gem != null) gem.SetChestLootOwner(progress);
        RewardMagnet.Attach(reward);
    }

    void OnTriggerEnter2D(Collider2D collision) {
        if (collision.gameObject.CompareTag("Player")) {
            range = true;
        }
    }

    public void openChest() {
        if (!open) return;
        if (OpenWorldChestProgress.IsOpenWorldScene(gameObject.scene.name)
            && !OpenWorldChestProgress.TryOpen(gameObject.scene.path, rewardSourceId)) {
            open = false;
            gameObject.GetComponent<SpriteRenderer>().sprite = openChestSprite;
            return;
        }
        open = false;
        gameObject.GetComponent<SpriteRenderer>().sprite = openChestSprite;
        SpawnRewards();
        Opened?.Invoke(this);
    }
}
