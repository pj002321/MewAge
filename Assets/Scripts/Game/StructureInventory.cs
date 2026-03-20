using UnityEngine;
using Mewage.DB;

/// <summary>
/// 기물 인벤토리 — 최대 4종류의 기물을 슬롯별로 관리합니다.
/// 같은 종류의 기물은 같은 슬롯에 누적됩니다.
/// Auto 모드 활성화 시 slotAutoInterval 주기로 보유 슬롯을 자동 사용합니다.
/// </summary>
public class StructureInventory : MonoBehaviourSingleton<StructureInventory>
{
    public const int SlotCount = 4;

    [Header("자동 사용 설정")]
    [Tooltip("Auto 모드에서 슬롯 하나를 사용하는 주기 (초)")]
    [SerializeField] private float slotAutoInterval = 5f;

    private readonly DB_기물[] slotData  = new DB_기물[SlotCount];
    private readonly int[]     slotCount = new int[SlotCount];
    private readonly float[]   autoTimer = new float[SlotCount];

    protected override bool DontDestroy => false;

    public event System.Action OnChanged;
    public event System.Action OnAutoChanged;

    public bool IsAutoMode { get; private set; }

    public void ToggleAuto()
    {
        IsAutoMode = !IsAutoMode;
        // 오토 켤 때 타이머 리셋
        for (int i = 0; i < SlotCount; i++) autoTimer[i] = 0f;
        OnAutoChanged?.Invoke();
    }

    private void Update()
    {
        if (!IsAutoMode) return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] == null || slotCount[i] <= 0) continue;

            autoTimer[i] += Time.deltaTime;
            if (autoTimer[i] >= slotAutoInterval)
            {
                autoTimer[i] = 0f;
                UseAt(i);
            }
        }
    }

    // ── 조회 ────────────────────────────────────────────────────────
    public DB_기물 GetData(int slot)  => slot >= 0 && slot < SlotCount ? slotData[slot]  : null;
    public int     GetCount(int slot) => slot >= 0 && slot < SlotCount ? slotCount[slot] : 0;

    // ── 획득 (기물 구슬 수집 시 호출) ───────────────────────────────
    public void Push(DB_기물 data)
    {
        if (data == null) return;

        // 같은 종류 슬롯이 있으면 누적
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] == data) { slotCount[i]++; OnChanged?.Invoke(); return; }
        }

        // 빈 슬롯에 새로 추가
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] == null) { slotData[i] = data; slotCount[i] = 1; OnChanged?.Invoke(); return; }
        }

        Debug.LogWarning("[StructureInventory] 슬롯이 가득 찼습니다.");
    }

    // ── 사용 (버튼 클릭 시 호출) ────────────────────────────────────
    public void UseAt(int slot)
    {
        if (slot < 0 || slot >= SlotCount) return;
        if (slotData[slot] == null || slotCount[slot] <= 0) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        var data = slotData[slot];
        slotCount[slot]--;
        if (slotCount[slot] <= 0) slotData[slot] = null;

        if (data.f_설치프리팹 != null)
            Object.Instantiate(data.f_설치프리팹, player.Position, Quaternion.identity);

        OnChanged?.Invoke();
    }
}
