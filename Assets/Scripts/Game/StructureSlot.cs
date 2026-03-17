using UnityEngine;

/// <summary>
/// 기물이 설치될 수 있는 슬롯.
/// 씬에 빈 오브젝트로 배치하고 이 컴포넌트를 붙여두세요.
/// </summary>
public class StructureSlot : MonoBehaviour
{
    [Header("비어있을 때 표시할 스프라이트 (선택)")]
    [SerializeField] private SpriteRenderer indicator;
    [SerializeField] private Color emptyColor    = new(1f, 1f, 0f, 0.4f);
    [SerializeField] private Color occupiedColor = new(0.3f, 0.3f, 0.3f, 0.2f);

    public bool IsOccupied { get; private set; }

    private void Awake()
    {
        if (indicator == null)
            indicator = GetComponent<SpriteRenderer>();
        RefreshVisual();
    }

    /// <summary>가장 가까운 빈 슬롯을 반환합니다. 없으면 null.</summary>
    public static StructureSlot FindNearest(Vector3 from)
    {
        StructureSlot best = null;
        float bestDist = float.MaxValue;
        foreach (var slot in FindObjectsByType<StructureSlot>(FindObjectsSortMode.None))
        {
            if (slot.IsOccupied) continue;
            float d = Vector3.Distance(from, slot.transform.position);
            if (d < bestDist) { bestDist = d; best = slot; }
        }
        return best;
    }

    /// <summary>슬롯에 기물을 설치합니다.</summary>
    public void Occupy(GameObject structurePrefab)
    {
        if (IsOccupied) return;
        IsOccupied = true;
        RefreshVisual();
        var go = Instantiate(structurePrefab, transform.position, Quaternion.identity);
        // 기물이 파괴되면 슬롯을 다시 비웁니다
        go.AddComponent<SlotReleaser>().Slot = this;
    }

    public void Release()
    {
        IsOccupied = false;
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (indicator == null) return;
        indicator.color = IsOccupied ? occupiedColor : emptyColor;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsOccupied ? new Color(0.3f, 0.3f, 0.3f, 0.5f) : new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.6f);
    }
}

/// <summary>기물 오브젝트에 자동 부착 — 파괴 시 슬롯 해제.</summary>
public class SlotReleaser : MonoBehaviour
{
    public StructureSlot Slot;
    private void OnDestroy() => Slot?.Release();
}
