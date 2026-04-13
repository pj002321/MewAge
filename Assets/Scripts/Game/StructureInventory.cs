using UnityEngine;
using Mewage.DB;

/// <summary>
/// 기물 인벤토리 — 최대 4종류의 기물을 슬롯별로 관리합니다.
/// 같은 종류의 기물은 같은 슬롯에 누적됩니다.
/// Auto 모드 활성화 시 쿨타임이 끝난 슬롯을 즉시 사용합니다.
/// </summary>
public class StructureInventory : MonoBehaviourSingleton<StructureInventory>
{
    public const int SlotCount = 4;

    [Header("자동 사용 설정")]
    [Tooltip("Auto 모드에서 슬롯을 사용하는 주기 (초)")]
    [SerializeField] private float slotAutoInterval = 3f;

    [Header("쿨타임")]
    [SerializeField] private float baseCooldown = 5f;

    private readonly DB_기물[] slotData            = new DB_기물[SlotCount];
    private readonly int[]     slotCount           = new int[SlotCount];
    private readonly float[]   _cooldownRemaining  = new float[SlotCount];
    private readonly float[]   _cooldownDuration   = new float[SlotCount];
    private float              _autoTimer;

    protected override bool DontDestroy => false;

    public event System.Action      OnChanged;
    public event System.Action      OnAutoChanged;
    public event System.Action<int> OnSlotUsed;      // 슬롯 사용 시 (수동/자동 공통)
    public event System.Action<int> OnCooldownEnded; // 슬롯 쿨타임 종료 시

    private void Start()
    {
        // 인게임 진입 시에도 빈 프리셋 슬롯 자동 채움 (아웃게임에서 변경됐을 수 있음)
        StructureWareManager.Shared?.EnsurePresetsNotEmpty();
        LoadFromPresets();
    }

    public bool IsAutoMode     { get; private set; }
    /// <summary>AutoMode에서 다음으로 사용될 슬롯 인덱스. 없으면 -1.</summary>
    public int  ActiveAutoSlot { get; private set; } = -1;

    /// <summary>패널이 열려있는 동안 auto-mode 배치를 막습니다.</summary>
    public static bool BlockAuto { get; set; }

    public void ToggleAuto()
    {
        IsAutoMode     = !IsAutoMode;
        _autoTimer     = 0f;
        ActiveAutoSlot = IsAutoMode ? FindNextReadySlot(-1) : -1;
        OnAutoChanged?.Invoke();
    }

    private void Update()
    {
        // 쿨타임 틱 (auto/manual 공통)
        for (int i = 0; i < SlotCount; i++)
        {
            if (_cooldownRemaining[i] > 0f)
            {
                _cooldownRemaining[i] -= Time.deltaTime;
                if (_cooldownRemaining[i] <= 0f)
                {
                    _cooldownRemaining[i] = 0f;
                    OnCooldownEnded?.Invoke(i);
                    // 활성 슬롯이 없었으면 이 슬롯이 후보가 될 수 있으므로 갱신
                    if (IsAutoMode && ActiveAutoSlot < 0)
                    {
                        ActiveAutoSlot = FindNextReadySlot(-1);
                        OnAutoChanged?.Invoke();
                    }
                }
            }
        }

        if (!IsAutoMode) return;
        if (BlockAuto)   return;
        if (Time.timeScale == 0f) return;

        // 활성 슬롯이 없거나 재고가 소진됐으면 다시 탐색
        if (ActiveAutoSlot < 0 || slotCount[ActiveAutoSlot] <= 0 || _cooldownRemaining[ActiveAutoSlot] > 0f)
        {
            int next = FindNextReadySlot(ActiveAutoSlot < 0 ? -1 : ActiveAutoSlot);
            if (next != ActiveAutoSlot) { ActiveAutoSlot = next; OnAutoChanged?.Invoke(); }
            if (ActiveAutoSlot < 0) return;
        }

        _autoTimer += Time.deltaTime;
        if (_autoTimer >= slotAutoInterval)
        {
            _autoTimer = 0f;
            int used = ActiveAutoSlot;
            UseAt(used);
            ActiveAutoSlot = FindNextReadySlot(used);
            OnAutoChanged?.Invoke();
        }
    }

    // ── 쿨타임 조회 (StructureButton에서 오버레이 렌더링용) ────────────
    public bool  IsOnCooldown(int slot) =>
        slot >= 0 && slot < SlotCount && _cooldownRemaining[slot] > 0f;

    /// <summary>1 = 방금 시작, 0 = 완료. 오버레이 fillAmount용.</summary>
    public float GetCooldownProgress(int slot)
    {
        if (slot < 0 || slot >= SlotCount) return 0f;
        float dur = _cooldownDuration[slot];
        return dur > 0f ? Mathf.Clamp01(_cooldownRemaining[slot] / dur) : 0f;
    }

    // ── 프리셋 로드 ──────────────────────────────────────────────────
    private void LoadFromPresets()
    {
        var ware = StructureWareManager.Shared;
        if (ware == null) return;

        var assigned = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < SlotCount; i++)
        {
            string id = ware.GetPreset(i);
            if (string.IsNullOrEmpty(id)) continue;
            if (!assigned.Add(id)) continue;

            var data = DB_기물.FindEntity(r => r.f_기물ID == id);
            if (data == null) continue;

            slotData[i]  = data;
            slotCount[i] = 1;
        }
        OnChanged?.Invoke();
    }

    // ── 기물 설치 ────────────────────────────────────────────────────
    private static void SpawnStructure(DB_기물 data, Vector3 pos)
    {
        string id = data?.f_기물ID;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[StructureInventory] 기물 ID를 찾을 수 없음");
            return;
        }

        string[] parts   = id.Split('_');
        string   chapter = parts.Length > 0 ? parts[0] : id;
        string   path    = $"Ware/{chapter}/{id.ToLower()}_s";

        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[StructureInventory] 설치 프리팹 없음: Resources/{path}");
            return;
        }

        Object.Instantiate(prefab, pos, Quaternion.identity);
        Debug.Log($"[StructureInventory] 기물 설치: {id} at {pos}");
    }

    /// <summary>from 다음부터 순환 탐색 — 재고 있고 쿨타임 아닌 첫 슬롯. 없으면 -1.</summary>
    private int FindNextReadySlot(int from)
    {
        for (int i = 1; i <= SlotCount; i++)
        {
            int idx = (from + i + SlotCount) % SlotCount;
            if (slotData[idx] != null && slotCount[idx] > 0 && _cooldownRemaining[idx] <= 0f)
                return idx;
        }
        return -1;
    }

    // ── 조회 ────────────────────────────────────────────────────────
    public DB_기물 GetData(int slot)  => slot >= 0 && slot < SlotCount ? slotData[slot]  : null;
    public int     GetCount(int slot) => slot >= 0 && slot < SlotCount ? slotCount[slot] : 0;

    // ── 획득 ────────────────────────────────────────────────────────
    public void Push(DB_기물 data)
    {
        if (data == null) return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] != null && slotData[i].f_기물ID == data.f_기물ID)
            {
                slotCount[i]++;
                OnChanged?.Invoke();
                return;
            }
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] == null) { slotData[i] = data; slotCount[i] = 1; OnChanged?.Invoke(); return; }
        }

        Debug.LogWarning("[StructureInventory] 슬롯이 가득 찼습니다.");
    }

    // ── 사용 ────────────────────────────────────────────────────────
    /// <param name="launchOriginWorld">발사 시작 월드 좌표. null이면 플레이어 위치 사용.</param>
    public void UseAt(int slot, Vector3? launchOriginWorld = null)
    {
        if (slot < 0 || slot >= SlotCount) return;
        if (slotData[slot] == null || slotCount[slot] <= 0) return;
        if (_cooldownRemaining[slot] > 0f) return;  // 쿨타임 중

        var player = PlayerController.Instance;
        if (player == null) return;

        var data = slotData[slot];
        slotCount[slot]--;
        if (slotCount[slot] <= 0) slotData[slot] = null;

        // 쿨타임 시작
        float reduction             = PlayerStatManager.Shared?.GetFinal(StatType.StructureCooldown) ?? 0f;
        float duration              = Mathf.Max(0.5f, baseCooldown * Mathf.Clamp01(1f - reduction / 100f));
        _cooldownDuration[slot]     = duration;
        _cooldownRemaining[slot]    = duration;

        Vector3 launchOrigin = launchOriginWorld ?? (Vector3)(Vector2)player.transform.position;
        StructurePlacer.Shared?.Place(data, launchOrigin);

        OnSlotUsed?.Invoke(slot);
        OnChanged?.Invoke();
    }
}
