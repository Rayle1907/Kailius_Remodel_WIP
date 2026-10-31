using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnalyticsSegmentDefinition : MonoBehaviour
{
    [SerializeField] private string segmentId;
    [SerializeField] private string revision = "1";
    [SerializeField] private bool sceneEntry;
    [SerializeField] private Transform startCheckpoint;
    [SerializeField] private Transform targetCheckpoint;
    [SerializeField] private Portal targetExit;
    [SerializeField] private Collider2D[] routePlatforms = Array.Empty<Collider2D>();
    [SerializeField] private Collider2D[] hazards = Array.Empty<Collider2D>();
    [SerializeField] private DeathWall deathWall;
    [SerializeField] private bool hasDifficultyTarget;
    [SerializeField, Range(0, 1)] private float targetFailureMin;
    [SerializeField, Range(0, 1)] private float targetFailureMax = 1;
    [SerializeField, HideInInspector] private SegmentSnapshot bakedSnapshot;

    public string SegmentId => segmentId;
    public bool IsSceneEntry => sceneEntry;
    public Transform StartCheckpoint => startCheckpoint;
    public Transform TargetCheckpoint => targetCheckpoint;
    public Portal TargetExit => targetExit;
    public DeathWall Wall => deathWall;
    public SegmentSnapshot BakedSnapshot => bakedSnapshot;

    public void BakeSnapshot()
    {
        bakedSnapshot = Capture();
    }

    public void BakeSnapshot(SegmentSnapshot snapshot)
    {
        if (snapshot == null || snapshot.segment_id != segmentId)
            throw new InvalidOperationException("The baked snapshot must belong to this segment definition.");
        bakedSnapshot = snapshot;
    }

    private void Reset()
    {
        segmentId = Guid.NewGuid().ToString("N");
    }

    public SegmentSnapshot Capture()
    {
        ValidateConfiguration();
        Transform target = targetCheckpoint != null ? targetCheckpoint : targetExit.transform;
        if (target.gameObject.scene != gameObject.scene || startCheckpoint.gameObject.scene != gameObject.scene)
            throw new InvalidOperationException("Analytics route boundaries must belong to the same scene.");
        var geometry = new SegmentGeometry
        {
            start = startCheckpoint.position,
            target = target.position,
            platforms = routePlatforms.Select(CaptureCollider).ToArray(),
            hazards = hazards.Select(CaptureCollider).ToArray(),
            death_wall_speed = deathWall == null ? 0 : deathWall.AnalyticsSpeed,
            death_wall_head_start = deathWall == null ? 0 : deathWall.AnalyticsHeadStart
        };
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(geometry)))).Replace("-", "").ToLowerInvariant();
        return new SegmentSnapshot
        {
            segment_id = segmentId,
            segment_version = revision + "-" + hash.Substring(0, 12),
            geometry_hash = hash,
            scene_name = gameObject.scene.name,
            start_checkpoint_id = HierarchyPath(startCheckpoint),
            target_checkpoint_id = HierarchyPath(target),
            target_kind = targetExit != null ? "exit" : "checkpoint",
            scene_entry = sceneEntry,
            has_difficulty_target = hasDifficultyTarget,
            target_failure_min = targetFailureMin,
            target_failure_max = targetFailureMax,
            geometry = geometry
        };
    }

    public void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(segmentId)) throw new InvalidOperationException("Segment ID is missing: " + name);
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidOperationException("Segment revision is missing: " + name);
        if (startCheckpoint == null) throw new InvalidOperationException("Start checkpoint is missing: " + name);
        if ((targetCheckpoint == null) == (targetExit == null))
            throw new InvalidOperationException("Set exactly one target checkpoint or exit: " + name);
        if (routePlatforms == null || routePlatforms.Length == 0)
            throw new InvalidOperationException("At least one ordered route platform is required: " + name);
        if (routePlatforms.Any(item => item == null)) throw new InvalidOperationException("A route platform is unassigned: " + name);
        if (hazards == null || hazards.Any(item => item == null)) throw new InvalidOperationException("A hazard reference is unassigned: " + name);
        if (targetFailureMin > targetFailureMax || targetFailureMin < 0 || targetFailureMax > 1)
            throw new InvalidOperationException("The intended failure interval is invalid: " + name);
        if (deathWall != null && deathWall.gameObject.scene != gameObject.scene)
            throw new InvalidOperationException("The death wall must belong to the segment scene: " + name);
        if (routePlatforms.Any(item => item.gameObject.scene != gameObject.scene)
            || hazards.Any(item => item.gameObject.scene != gameObject.scene))
            throw new InvalidOperationException("Route geometry must belong to the segment scene: " + name);
    }

    private static SurfaceSnapshot CaptureCollider(Collider2D collider)
    {
        Bounds bounds = collider.bounds;
        return new SurfaceSnapshot
        {
            object_id = HierarchyPath(collider.transform),
            kind = collider.GetType().Name,
            x = bounds.center.x, y = bounds.max.y,
            width = bounds.size.x, height = bounds.size.y,
            shape_hash = collider.GetShapeHash().ToString()
        };
    }

    public static string HierarchyPath(Transform item)
    {
        string path = item.name + "[" + item.GetSiblingIndex() + "]";
        while (item.parent != null)
        {
            item = item.parent;
            path = item.name + "[" + item.GetSiblingIndex() + "]/" + path;
        }
        return path;
    }
}

[Serializable]
public sealed class SegmentSnapshot
{
    public string segment_id, segment_version, geometry_hash, scene_name, start_checkpoint_id, target_checkpoint_id, target_kind;
    public string target_cohort = "first_attempt_per_player_version";
    public bool scene_entry, has_difficulty_target;
    public float target_failure_min, target_failure_max;
    public SegmentGeometry geometry;
}

[Serializable]
public sealed class SegmentGeometry
{
    public Vector3 start, target;
    public SurfaceSnapshot[] platforms, hazards;
    public float death_wall_speed, death_wall_head_start;
}

[Serializable]
public sealed class SurfaceSnapshot
{
    public string object_id, kind, shape_hash;
    public float x, y, width, height;
}
