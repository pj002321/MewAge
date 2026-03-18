using UnityEngine;

/// <summary>
/// 보스 전용 컴포넌트. EnemyController와 함께 같은 GameObject에 부착합니다.
/// HP가 enrageThreshold 이하로 떨어지면 분노 상태로 전환되고
/// SpriteRenderer 머티리얼의 아웃라인 색상을 빨간색으로 변경합니다.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class BossController : MonoBehaviour
{
    [Header("분노 임계값 (HP 비율)")]
    [SerializeField] private float enrageThreshold = 0.5f;

    [Header("아웃라인 머티리얼 프로퍼티")]
    [SerializeField] private string outlineColorProperty = "_OutlineColorBase";
    [SerializeField] private Color  normalOutlineColor   = Color.white;
    [SerializeField] private Color  enrageOutlineColor   = Color.red;

    [Header("분노 시 속도·피해 배율")]
    [SerializeField] private float enrageSpeedMult  = 1.5f;
    [SerializeField] private float enrageDamageMult = 1.5f;

    [Header("보스 HUD")]
    [SerializeField] private string bossName = "BOSS";

    public bool   IsEnraged { get; private set; }
    public float  HPRatio   => enemy != null ? enemy.HPRatio : 0f;
    public string BossName  => bossName;

    /// <summary>분노 전환 시 1회 발동 — BossBehaviour에서 구독합니다.</summary>
    public event System.Action OnEnraged;

    private EnemyController enemy;
    private SpriteRenderer  sr;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        sr    = GetComponent<SpriteRenderer>();
        enemy.OnDeath += OnOwnerDeath;
    }

    private void Start()
    {
        SetOutlineColor(normalOutlineColor);
        GameManager.Shared?.StartBossWave();
        BossHUDPanel.Shared?.Show(this);
    }

    private void OnOwnerDeath()
    {
        BossHUDPanel.Shared?.Hide();
        GameManager.Shared?.OnBossDefeated();
    }

    private void Update()
    {
        if (!IsEnraged && enemy.HPRatio <= enrageThreshold)
            TriggerEnrage();
    }

    // ── 분노 전환 ───────────────────────────────────────────────
    private void TriggerEnrage()
    {
        IsEnraged = true;
        SetOutlineColor(enrageOutlineColor);
        OnEnraged?.Invoke();
        Debug.Log("[BossController] 분노 상태 돌입!");
    }

    // ── 외부에서 배율 적용 (BossBehaviour에서 호출) ─────────────
    public float EnrageSpeedMult  => enrageSpeedMult;
    public float EnrageDamageMult => enrageDamageMult;

    // ── 아웃라인 색 변경 ─────────────────────────────────────────
    private void SetOutlineColor(Color color)
    {
        if (sr == null || sr.material == null) return;
        sr.material.SetColor(outlineColorProperty, color);
    }
}
