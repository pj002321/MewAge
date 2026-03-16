using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 증강 시스템 총괄 매니저.
/// DB_증강 테이블에서 가중치 랜덤으로 3개를 뽑아 ImprovPanel에 전달합니다.
/// </summary>
public class ImprovManager : MonoBehaviourSingleton<ImprovManager>
{
    [Header("UI")]
    [SerializeField] private ImprovPanel improvPanel;

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
            // 증강 데이터 없음 → 패널 스킵하고 바로 다음으로
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
        var draws = DrawImprovs(3);
        improvPanel.Setup(draws);
    }

    // ── 가중치 랜덤 추출 (중복 없이) ─────────────────────────────
    private DB_증강[] DrawImprovs(int count)
    {
        var pool = DB_증강.FindEntities(_ => true);
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[ImprovManager] DB_증강 데이터가 없습니다.");
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

        var player = PlayerController.Instance;
        if (player == null)
        {
            Debug.LogWarning("[ImprovManager] PlayerController.Instance가 null입니다.");
            return;
        }

        switch (data.f_효과타입)
        {
            case ImprovType.HealHP:           player.HealHP(data.f_주수치);                              break;
            case ImprovType.IncreaseMaxHP:    player.IncreaseMaxHP(data.f_주수치);                       break;
            case ImprovType.IncreaseAttack:   player.IncreaseAttack(data.f_주수치);                      break;
            case ImprovType.IncreaseDefense:  player.IncreaseDefense(data.f_주수치);                     break;
            case ImprovType.IncreaseMoveSpeed: player.IncreaseMoveSpeed(data.f_주수치);                  break;
            case ImprovType.IncreaseSizeHP:   player.IncreaseSizeHP(data.f_주수치, data.f_보조수치);     break;
            case ImprovType.DecreaseSizeHP:   player.DecreaseSizeHP(data.f_주수치, data.f_보조수치);     break;
            case ImprovType.IncreaseAttackSpeed: player.IncreaseAttackSpeed(data.f_주수치);              break;
            case ImprovType.IncreaseDodge:    player.IncreaseDodge(data.f_주수치);                       break;

            case ImprovType.RangedDistanceBonus:
            case ImprovType.MeleeAttackBonus:
            case ImprovType.PeriodicKnockback:
            case ImprovType.WaveHealHP:
                Debug.LogWarning($"[ImprovManager] 미구현 증강 타입: {data.f_효과타입}");
                break;
        }

        Debug.Log($"[ImprovManager] 증강 적용: {data.f_이름} (type={data.f_효과타입}, 주수치={data.f_주수치})");
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
