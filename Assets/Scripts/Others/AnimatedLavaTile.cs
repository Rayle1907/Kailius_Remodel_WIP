using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "AnimatedLavaTile", menuName = "Tiles/Animated Lava Tile")]
public sealed class AnimatedLavaTile : TileBase
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float animationSpeed = 1f;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.gameObject = null;
        tileData.flags = TileFlags.LockColor | TileFlags.LockTransform;
        tileData.colliderType = Tile.ColliderType.Sprite;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (frames == null || frames.Length < 2)
        {
            return false;
        }

        tileAnimationData.animatedSprites = frames;
        tileAnimationData.animationSpeed = animationSpeed;
        tileAnimationData.animationStartTime = 0f;
        return true;
    }
}
