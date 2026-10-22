using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class RequiredGauntletEnemy : MonoBehaviour
{
    [SerializeField] private string enemyType;
    [SerializeField] private Sprite portrait;
    public string EnemyType => NormalizeEnemyType(enemyType);
    public Sprite Portrait => portrait;
    [field: SerializeField] public int VisitId { get; private set; }
    [SerializeField] private GauntletRequirements requirements;

    private void Reset()
    {
        enemyType = NormalizeEnemyType(gameObject.name);
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) portrait = renderer.sprite;
    }

    public void Bind(GauntletRequirements owner, int visitId)
    {
        requirements = owner;
        VisitId = visitId;
    }

    private static string NormalizeEnemyType(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        value = value.Trim();
        while (value.EndsWith(")", System.StringComparison.Ordinal))
        {
            int start = value.LastIndexOf('(');
            if (start < 0) break;
            string suffix = value.Substring(start + 1, value.Length - start - 2);
            bool instanceSuffix = suffix == "Clone";
            if (!instanceSuffix && suffix.Length > 0)
            {
                instanceSuffix = true;
                foreach (char character in suffix)
                    if (character < '0' || character > '9') instanceSuffix = false;
            }
            if (!instanceSuffix) break;
            value = value.Substring(0, start).TrimEnd();
        }
        return value;
    }
    public void RecordDefeat()
    {
        if (requirements != null) requirements.RecordDefeat(this);
    }
}
