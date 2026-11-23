using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class RequiredGauntletEnemy : MonoBehaviour
{
    [SerializeField] private Sprite portrait;
    private Enemy enemy;
    private GauntletRequirements requirements;
    private int visitId = -1;

    public string EnemyType
    {
        get
        {
            if (enemy == null) enemy = GetComponent<Enemy>();
            if (enemy == null) return string.Empty;
            return enemy.EnemyTypeId;
        }
    }
    public Sprite Portrait => portrait;
    public int VisitId => visitId;

    private void Reset()
    {
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) portrait = renderer.sprite;
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    public void Bind(GauntletRequirements owner, int visitId)
    {
        requirements = owner;
        this.visitId = visitId;
        if (enemy == null) enemy = GetComponent<Enemy>();
    }

    public void RecordDefeat()
    {
        if (requirements != null && visitId >= 0) requirements.RecordDefeat(visitId);
    }
}
