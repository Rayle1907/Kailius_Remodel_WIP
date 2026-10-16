using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class RequiredGauntletEnemy : MonoBehaviour
{
    [SerializeField] private string enemyType;
    [SerializeField] private Sprite portrait;
    public string EnemyType => enemyType;
    public Sprite Portrait => portrait;
    public int VisitId { get; private set; }
    private GauntletRequirements requirements;

    private void Reset()
    {
        enemyType = gameObject.name;
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) portrait = renderer.sprite;
    }

    public void Bind(GauntletRequirements owner, int visitId)
    {
        requirements = owner;
        VisitId = visitId;
    }
    public void RecordDefeat()
    {
        if (requirements != null) requirements.RecordDefeat(this);
    }
}
