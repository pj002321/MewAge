using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 증강 시스템 총괄 매니저.
/// DB_증강 테이블에서 가중치 랜덤으로 3개를 뽑아 ImprovPanel에 전달합니다.
///
/// 증강 적용 원칙:
/// · 능력치 계열 → PlayerStatManager.AddPctMod(statType, pct)
///   최종값 = base × (1 + totalPct / 100)  (예: ATK 100 + 10% → 110)
/// · 즉시 회복   → 현재 MaxHP 기준 % 회복
/// · 처치 회복   → 주수치(%) 확률 / 최대체력의 보조수치(%) 회복
/// · 회복 구슬   → 주수치(%) 확률 스폰 / 최대체력의 보조수치(%) 회복
/// </summary>
public class ImprovManager : MonoBehaviourSingleton<ImprovManager>
{
    [Header("UI")]
    [SerializeField] private ImprovPanel improvPanel;

    [Header("설정")]
    [SerializeField] private int rerollPrice = 2;

    private int pendingWave;

    protected override bool DontDestroy => false;

    // 획득한 증강 이력: 증강ID → (데이터, 횟수)
    private readonly Dictionary<string, (DB_증강 data, int count)> _acquired = new();
    public IReadOnlyDictionary<string, (DB_증강 data, int count)> Acquired => _acquired;

    private void Start()
    {
        if (improvPanel) improvPanel.gameObject.SetActive(false);
    }

    // ── 패널 열기 ─────────────────────────────────────────────────
    public void ShowPanel(int wave)
    {
        pendingWave = wave;

        var draws = DrawImprovs(3);
        if (draws.Length == 0)
        {
            OnNextClicked();
            return;
        }

        Time.timeScale = 0f;
        improvPanel.Setup(draws);

        var blur = improvPanel.GetComponentInChildren<BlurBackground>(true);
        if (blur != null)
            StartCoroutine(blur.Capture(improvPanel.gameObject));
        else
            improvPanel.gameObject.SetActive(true);

        // 안전망: 0.5초 후에도 패널이 안 떠있으면 강제 활성화
        StartCoroutine(EnsurePanelVisible());
    }

    private IEnumerator EnsurePanelVisible()
    {
        // WaitForEndOfFrame은 timeScale 0에서도 동작
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        if (improvPanel != null && !improvPanel.gameObject.activeSelf)
        {
            this.DError("[ImprovManager] BlurBackground 캡처 후에도 패널이 비활성 — 강제 활성화");
            improvPanel.gameObject.SetActive(true);
        }
    }

    // ── 리롤 ──────────────────────────────────────────────────────
    public void Reroll()
    {
        var stats = PlayerStats.Shared;
        if (stats == null || stats.Gold < rerollPrice) return;

        stats.SpendGold(rerollPrice);
        var draws = DrawImprovs(3);
        improvPanel.Setup(draws);
    }

    // ── 가중치 랜덤 추출 (중복 없이) ─────────────────────────────
    private DB_증강[] DrawImprovs(int count)
    {
        var pool = DB_증강.FindEntities(e => e.f_증강ID != ImprovType.AUG_SKL);
        if (pool == null || pool.Count == 0)
        {
            this.DLog("[ImprovManager] DB_증강 데이터가 없습니다.");
            return new DB_증강[0];
        }

        int actual = Mathf.Min(count, pool.Count);
        var result = new DB_증강[actual];

        for (int i = 0; i < actual; i++)
        {
            float total = 0f;
            foreach (var e in pool) total += e.f_가중치;

            float roll = Random.Range(0f, total);
            float acc  = 0f;
            DB_증강 picked = pool[pool.Count - 1];
            foreach (var e in pool)
            {
                acc += e.f_가중치;
                if (roll < acc) { picked = e; break; }
            }

            result[i] = picked;
            pool.Remove(picked);
        }
        return result;
    }

    // ── 증강 효과 적용 ────────────────────────────────────────────
    public void OnImprovSelected(DB_증강 data)
    {
        if (data == null) return;

        var sm     = PlayerStatManager.Shared;
        var player = PlayerController.Instance;

        if (player == null)
        {
            this.DLog("[ImprovManager] PlayerController가 null입니다.");
            return;
        }

        float main = data.f_주수치;
        float sub  = data.f_보조수치;

        // sm(PlayerStatManager)이 필요한 case를 위한 지역 헬퍼
        bool RequireSm()
        {
            if (sm != null) return true;
            this.DLog("[ImprovManager] PlayerStatManager.Shared가 null — 스탯 증강 미적용");
            return false;
        }

        switch (data.f_증강ID)
        {
            // ── 절댓값(flat) 계열 — 설명에 % 없음 ───────────────────
            case ImprovType.AUG_ATK:
                if (RequireSm()) sm.SetBase(StatType.ATK, sm.GetBase(StatType.ATK) + main);
                break;

            case ImprovType.AUG_DEF:
                if (RequireSm()) sm.SetBase(StatType.DEF, sm.GetBase(StatType.DEF) + main);
                break;

            case ImprovType.AUG_MAX_HP:
                if (RequireSm()) sm.SetBase(StatType.MaxHP, sm.GetBase(StatType.MaxHP) + main);
                break;

            case ImprovType.AUG_SPD_MOV:
                if (RequireSm()) sm.SetBase(StatType.MoveSpeed, sm.GetBase(StatType.MoveSpeed) + main);
                break;

            case ImprovType.AUG_EVA:
                if (RequireSm()) sm.SetBase(StatType.Evasion, sm.GetBase(StatType.Evasion) + main);
                break;

            case ImprovType.AUG_CRI_DMG:
                if (RequireSm()) sm.SetBase(StatType.CritDmg, sm.GetBase(StatType.CritDmg) + main);
                break;

            case ImprovType.AUG_REWARD_RNG:
                if (RequireSm()) sm.SetBase(StatType.MagnetRadius, sm.GetBase(StatType.MagnetRadius) + main);
                break;

            case ImprovType.AUG_SIZE_HP_UP:
                player.IncreaseSizeHP(main, 0f);
                if (RequireSm()) sm.SetBase(StatType.MaxHP, sm.GetBase(StatType.MaxHP) + sub);
                break;

            case ImprovType.AUG_SIZE_HP_DOWN:
                player.DecreaseSizeHP(main, 0f);
                if (RequireSm()) sm.AddPctMod(StatType.MaxHP, -sub);
                break;

            // ── % 모디파이어 계열 — 설명에 % 있음 ───────────────────
            case ImprovType.AUG_SPD_ATK:
                if (RequireSm()) sm.AddPctMod(StatType.AtkSpeed, main);
                break;

            case ImprovType.AUG_CRI_RATE:
                if (RequireSm()) sm.AddPctMod(StatType.CritChance, main);
                break;

            case ImprovType.AUG_DOT_DMG:
                if (RequireSm()) sm.AddPctMod(StatType.BleedPower, main);
                break;

            // ── 특수 계열 ─────────────────────────────────────────

            case ImprovType.AUG_HEAL:
                float healAmount = player.MaxHPValue * (main / 100f);
                player.HealHP(healAmount);
                break;

            case ImprovType.AUG_HEAL_DEAD:
                if (RequireSm()) sm.AddPctMod(StatType.HealOnKill, main);
                player.IncreaseHealOnKillFactor(sub / 100f);
                break;

            case ImprovType.AUG_SPW_HP_SKL:
                player.IncreaseHealOrbChance(
                    chance:     main / 100f,
                    healFactor: sub  / 100f
                );
                break;
        }


        // 획득 이력 기록
        string key = data.f_증강ID.ToString();
        Debug.Log($"[ImprovManager] OnImprovSelected 호출 — key='{key}', isEmpty={string.IsNullOrEmpty(key)}, instance={GetInstanceID()}");
        if (!string.IsNullOrEmpty(key))
        {
            if (_acquired.TryGetValue(key, out var entry))
                _acquired[key] = (entry.data, entry.count + 1);
            else
                _acquired[key] = (data, 1);
            Debug.Log($"[ImprovManager] _acquired 저장 완료 — count={_acquired.Count}");
        }

        this.DLog($"[ImprovManager] 증강 적용: {data.f_이름} (type={data.f_증강ID}, 주={main}, 보조={sub})");
    }

    // ── 다음 버튼 → UpgradePanel ─────────────────────────────────
    public void OnNextClicked()
    {
        improvPanel.gameObject.SetActive(false);

        Time.timeScale = 1f;

        GameManager.Shared?.StartNextWave();
    }
}

// ── Unity 구현 가이드 ───────────────────────────────────────────────
// 1. ImprovManager GameObject에 이 컴포넌트를 붙입니다.
// 2. Inspector에서 improvPanel 슬롯에 ImprovPanel을 연결합니다.
// 3. DB_증강 테이블에서 f_주수치 / f_보조수치를 % 단위로 입력합니다.
//    예) AUG_ATK: 주수치=10 → 공격력 +10%
//    예) AUG_HEAL: 주수치=20 → 최대체력의 20% 즉시 회복
//    예) AUG_HEAL_DEAD: 주수치=30(확률), 보조수치=15(MaxHP의 15% 회복)
