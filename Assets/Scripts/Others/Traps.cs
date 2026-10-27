using UnityEngine;
using UnityEngine.Tilemaps;

public class Traps : MonoBehaviour {
    public enum RecoveryMode { Checkpoint, Local }

    [System.Serializable]
    public struct SpikeRecoveryPoint {
        public Vector3Int cell;
        public Transform safePosition;
    }

    public int damage = 50;
    [SerializeField] private RecoveryMode recoveryMode;
    [SerializeField] private TileBase[] localRecoveryTiles;
    [SerializeField] private Transform safePosition;
    [SerializeField] private SpikeRecoveryPoint[] recoveryPoints;
    [SerializeField, Min(1)] private int spikeDamage = 50;

    private Tilemap recoveryTilemap;

    private void Awake() {
        recoveryTilemap = GetComponent<Tilemap>();
    }

    private void OnCollisionEnter2D(Collision2D collision) {
        HandleCollision(collision, false);
    }

    private void OnCollisionStay2D(Collision2D collision) {
        HandleCollision(collision, true);
    }

    private void HandleCollision(Collision2D collision, bool staying) {
        for (int i = 0; i < collision.contactCount; i++) {
            ContactPoint2D contact = collision.GetContact(i);
            if (TryGetLocalContact(contact.point, out Vector3Int cell)) {
                HandleLocalContact(collision.gameObject, cell, contact.point);
                return;
            }
        }
        if (!staying) HandleHazardContact(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (!TryHandleLocalTrigger(other)) HandleHazardContact(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other) {
        TryHandleLocalTrigger(other);
    }

    private bool TryHandleLocalTrigger(Collider2D other) {
        if (recoveryMode != RecoveryMode.Local) return false;
        if (localRecoveryTiles == null || localRecoveryTiles.Length == 0) {
            HandleLocalContact(other.gameObject, default, other.ClosestPoint(transform.position));
            return true;
        }
        if (recoveryTilemap == null) return false;
        Bounds bounds = other.bounds;
        Vector3Int min = recoveryTilemap.WorldToCell(new Vector3(bounds.min.x - 0.02f, bounds.min.y - 0.02f, transform.position.z));
        Vector3Int max = recoveryTilemap.WorldToCell(new Vector3(bounds.max.x + 0.02f, bounds.max.y + 0.02f, transform.position.z));
        for (int y = min.y; y <= max.y; y++) {
            for (int x = min.x; x <= max.x; x++) {
                Vector3Int cell = new Vector3Int(x, y, min.z);
                if (!IsLocalTile(cell)) continue;
                HandleLocalContact(other.gameObject, cell, recoveryTilemap.GetCellCenterWorld(cell));
                return true;
            }
        }
        return false;
    }

    private bool TryGetLocalContact(Vector2 point, out Vector3Int cell) {
        cell = default;
        if (recoveryMode != RecoveryMode.Local) return false;
        if (localRecoveryTiles == null || localRecoveryTiles.Length == 0) return true;
        if (recoveryTilemap == null) return false;
        for (int y = -1; y <= 1; y++) {
            for (int x = -1; x <= 1; x++) {
                cell = recoveryTilemap.WorldToCell(point + new Vector2(x, y) * 0.02f);
                if (IsLocalTile(cell)) return true;
            }
        }
        return false;
    }

    private bool IsLocalTile(Vector3Int cell) {
        TileBase tile = recoveryTilemap.GetTile(cell);
        if (tile == null) return false;
        foreach (TileBase localTile in localRecoveryTiles) {
            if (tile == localTile) return true;
        }
        return false;
    }

    private void HandleLocalContact(GameObject contactedObject, Vector3Int cell, Vector2 contact) {
        PlayerController controller = contactedObject.GetComponentInParent<PlayerController>();
        if (controller == null) return;
        Transform destination = safePosition;
        if (recoveryPoints != null) {
            foreach (SpikeRecoveryPoint recoveryPoint in recoveryPoints) {
                if (recoveryPoint.cell == cell && recoveryPoint.safePosition != null) {
                    destination = recoveryPoint.safePosition;
                    break;
                }
            }
        }
        Vector2 source = recoveryTilemap != null && localRecoveryTiles != null && localRecoveryTiles.Length > 0
            ? (Vector2)recoveryTilemap.GetCellCenterWorld(cell) : contact;
        controller.SpikeRecovery.TryHit(spikeDamage, source, destination);
    }

    protected void HandleHazardContact(GameObject contactedObject) {
        Stats playerStats = contactedObject.GetComponentInParent<Stats>();
        if (playerStats == null) return;
        int healthBefore = playerStats.health;
        playerStats.ApplyDamage(damage, "trap", true);
        if (playerStats.health > 0) {
            if (GauntletRunTracker.Instance != null) {
                GauntletRunTracker.Instance.RecordPlayerFailure("hazard", healthBefore, playerStats.health);
            }
            PlayerController controller = contactedObject.GetComponentInParent<PlayerController>();
            if (controller != null) controller.reSpawn();
        }
    }
}
