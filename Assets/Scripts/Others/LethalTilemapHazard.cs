using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[RequireComponent(typeof(Tilemap), typeof(TilemapCollider2D))]
public sealed class LethalTilemapHazard : Traps
{
    private Tilemap tilemap;
    private bool updatingColliders;

    private void OnEnable()
    {
        tilemap = GetComponent<Tilemap>();
        GetComponent<TilemapCollider2D>().isTrigger = true;
        Tilemap.tilemapTileChanged += HandleTilesChanged;
        RefreshColliders();
    }

    private void OnDisable()
    {
        Tilemap.tilemapTileChanged -= HandleTilesChanged;
    }

    private void HandleTilesChanged(Tilemap changedTilemap, Tilemap.SyncTile[] changes)
    {
        if (changedTilemap != tilemap || updatingColliders) return;
        updatingColliders = true;
        try
        {
            foreach (Tilemap.SyncTile change in changes)
            {
                UpdateCollider(change.position);
            }
        }
        finally
        {
            updatingColliders = false;
        }
    }

    private void RefreshColliders()
    {
        updatingColliders = true;
        try
        {
            foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
            {
                UpdateCollider(position);
            }
        }
        finally
        {
            updatingColliders = false;
        }
    }

    private void UpdateCollider(Vector3Int position)
    {
        if (tilemap.HasTile(position) && tilemap.GetColliderType(position) != Tile.ColliderType.Grid)
        {
            tilemap.SetColliderType(position, Tile.ColliderType.Grid);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Application.IsPlaying(gameObject)) return;
        HandleHazardContact(other.gameObject);
    }
}
