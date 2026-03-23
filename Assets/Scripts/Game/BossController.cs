using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 공통 로직 컴포넌트.
///
/// 역할
///   - 분노(Enrage) 상태 관리 및 아웃라인 색 변경
///   - 같은 GameObject 에 붙은 BossSkillBase 컴포넌트들을 수집·관리
///   - BossBehaviour 가 Chase 후 RandomReadySkill() 로 다음 스킬을 선택
///   - 보스 HUD 표시 / 사망 처리
///
/// 사용법
///   1. Enemy prefab 에 EnemyController + BossBehaviour + BossController 추가
///   2. 원하는 스킬 컴포넌트(BossSlamSkill, BossChargeSkill …)를 추가로 부착
///   3. 스킬이 없으면 Chase 만 반복합니다.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class BossController : MonoBehaviour
{
    [Header("분노 임계값 (HP 비율)")]
    [SerializeField] private float enrageThreshold = 0.5f;

    [Header("아웃라인 색상")]
    [SerializeField] private string outlineColorProperty = "_OutlineColorBase";
    [SerializeField] private Color  normalOutlineColor   = Color.white;
    [SerializeField] private Color  enrageOutlineColor   = Color.red;

    [Header("분노 시 공통 배율")]
    [SerializeField] private float enrageSpeedMult  = 1.5f;
    [SerializeField] private float enrageDamageMult = 1.5f;

    [Header("보스 HUD")]
    [SerializeField] private string bossName = "BOSS";

    // ── 공개 프로퍼티 ──────────────────────────────────────────────
    public bool   IsEnraged      { get; private set; }
    public float  HPRatio        => enemy != null ? enemy.HPRatio : 0f;
    public string BossName       => bossName;
    public float  EnrageSpeedMult  => enrageSpeedMult;
    public float  EnrageDamageMult => enrageDamageMult;

    /// <summary>분노 전환 시 1회 발동 — BossBehaviour 에서 구독합니다.</summary>
    public event System.Action OnEnraged;

    // ── 내부 참조 ──────────────────────────────────────────────────
    private EnemyController enemy;
    private SpriteRenderer  sr;
    private BossSkillBase[] skills;

    // ══════════════════════════════════════════════════════════════
    //  Unity 생명주기
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        enemy  = GetComponent<EnemyController>();
        sr     = GetComponent<SpriteRenderer>();
        skills = GetComponents<BossSkillBase>();

        foreach (var s in skills)
            s.Init(this, enemy);

        enemy.OnDeath += OnOwnerDeath;
    }

    private void Start()
    {
        SetOutlineColor(normalOutlineColor);
        GameManager.Shared?.StartBossWave();
        BossHUDPanel.Shared?.Show(this);
    }

    private void Update()
    {
        // 분노 체크
        if (!IsEnraged && enemy.HPRatio <= enrageThreshold)
            TriggerEnrage();

        // 스킬 쿨다운 틱
        float delta = Time.deltaTime;
        foreach (var s in skills)
            s.TickCooldown(delta);
    }

    // ══════════════════════════════════════════════════════════════
    //  스킬 선택
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 준비 완료(CanUse)된 스킬 중 하나를 랜덤으로 반환합니다.
    /// 없으면 null 을 반환합니다.
    /// </summary>
    public BossSkillBase RandomReadySkill()
    {
        if (skills == null || skills.Length == 0) return null;

        float dist = enemy.PlayerTransform != null
            ? Vector2.Distance(transform.position, enemy.PlayerTransform.position)
            : 0f;

        var ready = new List<BossSkillBase>();
        foreach (var s in skills)
            if (s.CanUse(dist)) ready.Add(s);

        if (ready.Count == 0) return null;
        return ready[Random.Range(0, ready.Count)];
    }

    // ══════════════════════════════════════════════════════════════
    //  분노 전환
    // ══════════════════════════════════════════════════════════════

    private void TriggerEnrage()
    {
        IsEnraged = true;
        SetOutlineColor(enrageOutlineColor);
        OnEnraged?.Invoke();
        Debug.Log("[BossController] 분노 상태 돌입!");
    }

    // ══════════════════════════════════════════════════════════════
    //  사망
    // ══════════════════════════════════════════════════════════════

    private void OnOwnerDeath()
    {
        BossHUDPanel.Shared?.Hide();
        GameManager.Shared?.OnBossDefeated();
    }

    // ══════════════════════════════════════════════════════════════
    //  아웃라인
    // ══════════════════════════════════════════════════════════════

    private void SetOutlineColor(Color color)
    {
        if (sr == null || sr.material == null) return;
        sr.material.SetColor(outlineColorProperty, color);
    }
}
