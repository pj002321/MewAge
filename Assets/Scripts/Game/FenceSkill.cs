using UnityEngine;

/// <summary>설치 시 플레이어 이동 방향 수직으로 3개 연속 배치됩니다.</summary>
public class FenceSkill : StructureSkillBase
{
    [SerializeField] private float fenceSpacing = 0.8f;

    private bool isFenceChild;

    public override void Activate()
    {
        if (!isFenceChild) SpawnFenceLine();
    }

    public void MarkAsFenceChild() => isFenceChild = true;

    private void SpawnFenceLine()
    {
        var player   = PlayerController.Instance;
        Vector2 move = player != null && player.MoveDirection.sqrMagnitude > 0.01f
            ? player.MoveDirection.normalized : Vector2.up;

        Vector2 perp = new Vector2(-move.y, move.x);

        foreach (int offset in new[] { -1, 1 })
        {
            Vector3 pos   = transform.position + (Vector3)(perp * fenceSpacing * offset);
            var     extra = Instantiate(gameObject, pos, transform.rotation);
            extra.GetComponent<FenceSkill>()?.MarkAsFenceChild();
        }
    }
}
