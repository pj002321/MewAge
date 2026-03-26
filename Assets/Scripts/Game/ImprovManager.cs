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
        improvPanel.gameObject.SetActive(true);
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
        var pool = DB_증강.FindEntities(_ => true);
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

        if (sm == null || player == null)
        {
            this.DLog("[ImprovManager] PlayerStatManager 또는 PlayerController가 null입니다.");
            return;
        }

        float main = data.f_주수치;
        float sub  = data.f_보조수치;

        switch (data.f_증강ID)
        {
            // ── 능력치 계열: % 모디파이어 누적 ──────────────────────
            case ImprovType.AUG_ATK:
                sm.AddPctMod(StatType.ATK, main);
                break;

            case ImprovType.AUG_DEF:
                sm.AddPctMod(StatType.DEF, main);
                break;

            case ImprovType.AUG_MAX_HP:
                sm.AddPctMod(StatType.MaxHP, main);
                break;

            case ImprovType.AUG_SPD_MOV:
                sm.AddPctMod(StatType.MoveSpeed, main);
                break;

            case ImprovType.AUG_SPD_ATK:
                sm.AddPctMod(StatType.AtkSpeed, main);
                break;

            case ImprovType.AUG_EVA:
                sm.AddPctMod(StatType.Evasion, main);
                break;

            case ImprovType.AUG_CRI_RATE:
                sm.AddPctMod(StatType.CritChance, main);
                break;

            case ImprovType.AUG_CRI_DMG:
                sm.AddPctMod(StatType.CritDmg, main);
                break;

            case ImprovType.AUG_DOT_DMG:
                sm.AddPctMod(StatType.BleedPower, main);
                break;

            // 기물 쿨타임 단축: 주수치% 감소 → 음수 모디파이어
            case ImprovType.AUG_SKL:
                sm.AddPctMod(StatType.StructureCooldown, -main);
                break;

            // 흡수 반경 증가
            case ImprovType.AUG_REWARD_RNG:
                sm.AddPctMod(StatType.MagnetRadius, main);
                break;

            // 몸집 크기 변화 + 최대 체력 % 증감
            case ImprovType.AUG_SIZE_HP_UP:
                player.IncreaseSizeHP(main, 0f);   // 크기 배율
                sm.AddPctMod(StatType.MaxHP, sub);  // 체력 %
                break;

            case ImprovType.AUG_SIZE_HP_DOWN:
                player.DecreaseSizeHP(main, 0f);    // 크기 배율
                sm.AddPctMod(StatType.MaxHP, -sub); // 체력 % 감소
                break;

            // ── 즉시 회복: 현재 MaxHP의 main% 즉시 회복 ─────────────
            case ImprovType.AUG_HEAL:
                float healAmount = player.MaxHPValue * (main / 100f);
                player.HealHP(healAmount);
                break;

            // ── 처치 회복: main% 만큼 발동확률 스탯 누적 + 회복량(sub) 비율 추가 ─
            case ImprovType.AUG_HEAL_DEAD:
                sm.AddPctMod(StatType.HealOnKill, main);
                player.IncreaseHealOnKillFactor(sub / 100f);
                break;

            // ── 회복 구슬: main% 확률 스폰 + MaxHP의 sub% 회복 ──────
            case ImprovType.AUG_SPW_HP_SKL:
                player.IncreaseHealOrbChance(
                    chance:     main / 100f,   // 스폰 확률 (fraction)
                    healFactor: sub  / 100f    // MaxHP 대비 회복 비율
                );
                break;
        }

        this.DLog($"[ImprovManager] 증강 적용: {data.f_이름} (type={data.f_증강ID}, 주={main}, 보조={sub})");
    }

    // ── 다음 버튼 → UpgradePanel ─────────────────────────────────
    public void OnNextClicked()
    {
        improvPanel.gameObject.SetActive(false);

        if (UpgradeManager.Shared != null)
            UpgradeManager.Shared.ShowPanel(pendingWave);
        else
        {
            Time.timeScale = 1f;
            GameManager.Shared?.StartNextWave();
        }
    }
}

// ── Unity 구현 가이드 ───────────────────────────────────────────────
// 1. ImprovManager GameObject에 이 컴포넌트를 붙입니다.
// 2. Inspector에서 improvPanel 슬롯에 ImprovPanel을 연결합니다.
// 3. DB_증강 테이블에서 f_주수치 / f_보조수치를 % 단위로 입력합니다.
//    예) AUG_ATK: 주수치=10 → 공격력 +10%
//    예) AUG_HEAL: 주수치=20 → 최대체력의 20% 즉시 회복
//    예) AUG_HEAL_DEAD: 주수치=30(확률), 보조수치=15(MaxHP의 15% 회복)
