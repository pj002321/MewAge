using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 시스템 총괄 매니저.
/// 웨이브 종료 시 GameManager가 ShowPanel()을 호출합니다.
/// 플레이어가 증강을 선택한 뒤 "다음"을 누르면 UpgradePanel로 넘어갑니다.
///
/// Inspector 연결:
///   improvPanel — ImprovPanel 컴포넌트
///   improvTable — ImprovTable ScriptableObject
/// </summary>
public class ImprovManager : MonoBehaviourSingleton<ImprovManager>
{
    [Header("UI")]
    [SerializeField] private ImprovPanel improvPanel;

    [Header("데이터")]
    [SerializeField] private ImprovTable improvTable;

    private int pendingWave;

    protected override bool DontDestroy => false;

    // ────────────────────────────────────────────────────────────────
    // 패널 열기
    // ────────────────────────────────────────────────────────────────
    public void ShowPanel(int wave)
    {
        pendingWave    = wave;
        Time.timeScale = 0f;

        var draws = DrawImprovs(3);
        improvPanel.Setup(draws);
        improvPanel.gameObject.SetActive(true);
    }

    // ────────────────────────────────────────────────────────────────
    // 증강 랜덤 추출 (중복 없이)
    // ────────────────────────────────────────────────────────────────
    private ImprovData[] DrawImprovs(int count)
    {
        if (improvTable == null || improvTable.entries == null || improvTable.entries.Length == 0)
        {
            Debug.LogWarning("[ImprovManager] ImprovTable이 비어있습니다.");
            return new ImprovData[0];
        }

        var pool   = new List<ImprovData>(improvTable.entries);
        int actual = Mathf.Min(count, pool.Count);
        var result = new ImprovData[actual];

        for (int i = 0; i < actual; i++)
        {
            int idx    = Random.Range(0, pool.Count);
            result[i]  = pool[idx];
            pool.RemoveAt(idx);
        }
        return result;
    }

    // ────────────────────────────────────────────────────────────────
    // 증강 효과 적용 (ImprovPanel이 호출)
    // ────────────────────────────────────────────────────────────────
    public void OnImprovSelected(ImprovData data)
    {
        if (data == null) return;

        var player = PlayerController.Instance;
        if (player == null)
        {
            Debug.LogWarning("[ImprovManager] PlayerController.Instance가 null입니다.");
            return;
        }

        switch (data.type)
        {
            case ImprovType.HealHP:          player.HealHP(data.value);          break;
            case ImprovType.IncreaseMaxHP:   player.IncreaseMaxHP(data.value);   break;
            case ImprovType.IncreaseAttack:  player.IncreaseAttack(data.value);  break;
            case ImprovType.IncreaseDefense: player.IncreaseDefense(data.value); break;
        }

        Debug.Log($"[ImprovManager] 증강 적용: {data.improvName} (type={data.type}, value={data.value})");
    }

    // ────────────────────────────────────────────────────────────────
    // 다음 버튼 (ImprovPanel이 호출) → UpgradePanel로 이동
    // ────────────────────────────────────────────────────────────────
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
