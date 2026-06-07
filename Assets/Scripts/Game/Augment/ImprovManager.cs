using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

public class ImprovManager : MonoBehaviourSingleton<ImprovManager>
{
    [Header("UI")]
    [SerializeField] private ImprovPanel improvPanel;

    [Header("설정")]
    [SerializeField] private int rerollPrice = 2;

    private static readonly HashSet<int> AugmentWaves = new() { 5, 10, 15, 20, 25 };

    public static bool IsAugmentWave(int wave) => AugmentWaves.Contains(wave);

    private int pendingWave;

    protected override bool DontDestroy => false;

    private readonly Dictionary<string, (DB_증강 data, int count)> _acquired = new();
    public IReadOnlyDictionary<string, (DB_증강 data, int count)> Acquired => _acquired;

    private readonly List<AugmentEffectBase> _activeEffects = new();

    // ── 증강 ID → 구현 클래스 등록 ───────────────────────────────
    private static readonly Dictionary<ImprovType, Func<AugmentEffectBase>> _registry = new()
    {
        // CMN
        { ImprovType.AUG_HP_HEAL,         () => new AugmentHpHeal()       },
        { ImprovType.AUG_HP_POTION,       () => new AugmentHpPotion()     },
        { ImprovType.AUG_STOP_ATK_SPD,    () => new AugmentStopAtkSpd()   },
        { ImprovType.AUG_LOWHP_ATK,       () => new AugmentLowHpAtk()     },
        { ImprovType.AUG_MOV_DOWN_ATK_UP, () => new AugmentMovDownAtkUp() },
        // C1 원시시대
        { ImprovType.AUG_FIRST_HUNT,       () => new AugmentFirstHunt()      },
        { ImprovType.AUG_WILD_FUSION,      () => new AugmentWildFusion()     },
        { ImprovType.AUG_SPAWN_TURRET,     () => new AugmentSpawnTurret()    },
        { ImprovType.AUG_PET_BEAST,        () => new AugmentPetBeast()       },
        { ImprovType.AUG_MUD_OPENING,      () => new AugmentMudOpening()     },
        { ImprovType.AUG_ICE_AGE,          () => new AugmentIceAge()         },
        { ImprovType.AUG_DELAYED_FISHBONE, () => new AugmentDelayedFishbone()},
        { ImprovType.AUG_ROTTEN_CATNIP,    () => new AugmentRottenCatnip()   },
        { ImprovType.AUG_BURNING_PAWS,     () => new AugmentBurningPaws()    },
    };

    // ── 이벤트 ───────────────────────────────────────────────────

    private void Start()
    {
        if (improvPanel) improvPanel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (var e in _activeEffects) e.OnRemove();
        _activeEffects.Clear();
    }

    // ── 웨이브 종료 시 GameManager에서 호출 ──────────────────────
    public void NotifyWaveEnd()
    {
        foreach (var e in _activeEffects) e.OnWaveEnd();
    }

    // ── 패널 열기 ─────────────────────────────────────────────────
    public void ShowPanel(int wave)
    {
        pendingWave = wave;

        var draws = DrawImprovs(2);
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

        StartCoroutine(EnsurePanelVisible());
    }

    private IEnumerator EnsurePanelVisible()
    {
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
        improvPanel.Setup(DrawImprovs(2));
    }

    // ── 가중치 랜덤 추출 ─────────────────────────────────────────
    private DB_증강[] DrawImprovs(int count)
    {
        string chapterKey = GameManager.Shared?.CurrentChapterID?.Split('_')[0] ?? "C1";

        float currentHp  = PlayerController.Instance != null ? PlayerController.Instance.CurrentHP : float.MaxValue;
        bool  nextIsBoss = GameManager.Shared != null &&
                           GameManager.Shared.GetNextBossWave(pendingWave) == pendingWave + 1;

        bool ExcludeFilter(DB_증강 e) =>
            (pendingWave == 0 && e.f_증강ID == nameof(ImprovType.AUG_HP_HEAL));

        var cmnPool     = DB_증강.FindEntities(e => e.f_챕터 == "CMN"     && !ExcludeFilter(e));
        var chapterPool = DB_증강.FindEntities(e => e.f_챕터 == chapterKey && !ExcludeFilter(e));

        var result = new List<DB_증강>(count);
        if (cmnPool.Count     > 0) result.Add(PickWeighted(cmnPool));
        if (chapterPool.Count > 0) result.Add(PickWeighted(chapterPool));

        if (result.Count == 0)
        {
            this.DLog("[ImprovManager] DB_증강 데이터가 없습니다.");
            return Array.Empty<DB_증강>();
        }

        return result.ToArray();
    }

    private static DB_증강 PickWeighted(List<DB_증강> pool)
    {
        float total = 0f;
        foreach (var e in pool) total += e.f_가중치;

        float roll = UnityEngine.Random.Range(0f, total);
        float acc  = 0f;
        foreach (var e in pool)
        {
            acc += e.f_가중치;
            if (roll < acc) return e;
        }
        return pool[pool.Count - 1];
    }

    // ── 증강 효과 적용 ────────────────────────────────────────────
    public void OnImprovSelected(DB_증강 data)
    {
        if (data == null) return;

        if (!Enum.TryParse<ImprovType>(data.f_증강ID, out var augType) ||
            !_registry.TryGetValue(augType, out var factory))
        {
            this.DError($"[ImprovManager] 등록되지 않은 증강: {data.f_증강ID}");
            return;
        }

        var effect = factory();
        effect.Initialize(data);
        effect.OnGain();
        _activeEffects.Add(effect);

        string key = data.f_증강ID;
        if (_acquired.TryGetValue(key, out var entry))
            _acquired[key] = (entry.data, entry.count + 1);
        else
            _acquired[key] = (data, 1);

        this.DLog($"[ImprovManager] 증강 적용: {data.f_이름} (type={data.f_증강ID})");
    }

    public void DebugReroll()
    {
        if (improvPanel == null || !improvPanel.gameObject.activeSelf) return;
        var draws = DrawImprovs(2);
        if (draws.Length == 0) return;
        improvPanel.Setup(draws);
    }

    // ── 카드 선택 후 다음 단계 진행 ──────────────────────────────
    public void OnNextClicked()
    {
        improvPanel.gameObject.SetActive(false);
        Time.timeScale = 1f;
        if (pendingWave > 1 && UpgradeManager.Shared != null)
            UpgradeManager.Shared.ShowPanel(pendingWave);
        else
            GameManager.Shared?.StartNextWave();
    }
}
