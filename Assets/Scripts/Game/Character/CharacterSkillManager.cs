using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 고유 스킬 포인트·발동을 관리합니다.
///
/// ■ 흐름
///   PlayerCharacterManager.ApplyCharacterStats() → Initialize(skillId)
///   적 처치 → AddPoint(1)
///   게이지 가득 참 → skillButton 활성화
///   버튼 클릭 → TryActivate() → 스킬 발동 → duration 후 자동 종료
///
/// ■ 프리팹 배치
///   Resources/Skills/{skillId.ToLower()}.prefab 에 CharacterSkillBase 서브클래스를 부착하세요.
/// </summary>
public class CharacterSkillManager : MonoBehaviourSingleton<CharacterSkillManager>
{
    [Header("UI")]
    [SerializeField] private Button skillButton;
    [SerializeField] private Slider skillGaugeSlider;
    [SerializeField] private GameObject skillReadyFX; // 꽉 찼을 때 표시할 이펙트 (선택)

    [Header("게이지 설정")]
    [SerializeField] private int   pointsRequired   = 100;  // 스킬 발동에 필요한 포인트
    [SerializeField] private float hitChance        = 0.5f; // 피격 시 포인트 획득 확률
    [SerializeField] private float skillCooldown    = 5f;   // 스킬 종료 후 재발동 쿨타임(초)

    [Header("기본 스킬 (프리팹 없을 때)")]
    [SerializeField] private float      defaultSpeedMultiplier = 5f;
    [SerializeField] private float      defaultDuration        = 3f;
    [SerializeField] private GameObject skillEffectPrefab;

    protected override bool DontDestroy => false;

    private CharacterSkillBase _skill;
    private int        _currentPoints;
    private bool       _isActive;
    private bool       _onCooldown;
    private bool       _defaultBoostActive;
    private Coroutine  _activeRoutine;
    private Coroutine  _cooldownRoutine;
    private Coroutine  _pulseRoutine;
    private GameObject _spawnedEffect;

    private Vector3    _sliderOriginalScale;

    // ── 초기화 ─────────────────────────────────────────────────────

    /// <summary>캐릭터 장착 시 호출 — 스킬 프리팹을 로드하고 준비합니다.</summary>
    public void Initialize(string skillId)
    {
        // 이전 스킬 정리
        if (_skill != null)
        {
            StopSkill();
            Destroy(_skill.gameObject);
            _skill = null;
        }

        _currentPoints = 0;
        _isActive      = false;
        _onCooldown    = false;
        _skill         = null;

        if (_cooldownRoutine != null) { StopCoroutine(_cooldownRoutine); _cooldownRoutine = null; }

        if (!string.IsNullOrEmpty(skillId))
        {
            var prefab = Resources.Load<GameObject>($"Skills/{skillId.ToLower()}");
            if (prefab != null)
            {
                var parent = PlayerController.Instance != null
                    ? PlayerController.Instance.transform
                    : transform;
                var go = Instantiate(prefab, parent);
                go.transform.localPosition = Vector3.zero;
                _skill = go.GetComponent<CharacterSkillBase>();
            }
        }

        skillButton?.gameObject.SetActive(true);
        if (skillGaugeSlider)
        {
            skillGaugeSlider.gameObject.SetActive(true);
            skillGaugeSlider.minValue = 0f;
            skillGaugeSlider.maxValue = pointsRequired;
            skillGaugeSlider.value    = 0f;
            _sliderOriginalScale      = skillGaugeSlider.transform.localScale;
        }

        SetButtonInteractable(false);
        if (skillReadyFX) skillReadyFX.SetActive(false);
    }

    protected override void Awake()
    {
        base.Awake();
        skillButton?.onClick.AddListener(TryActivate);

        // 씬 시작 시 Initialize 전까지 숨김
        skillButton?.gameObject.SetActive(false);
        if (skillGaugeSlider) skillGaugeSlider.gameObject.SetActive(false);
    }

    // ── 포인트 관리 ─────────────────────────────────────────────────

    /// <summary>적 피격 시 EnemyController에서 호출 — 50% 확률로 포인트 획득 오브 연출.</summary>
    public void TryAddPointFromHit(Vector3 enemyPos)
    {
        if (_isActive) return;
        if (_currentPoints >= pointsRequired) return;
        if (Random.value > hitChance) return;

        FXManager.Shared?.SpawnSkillOrb(enemyPos, () => AddPoint(1));
    }

    private void AddPoint(int amount = 1)
    {
        if (_currentPoints >= pointsRequired) return;

        _currentPoints = Mathf.Min(_currentPoints + amount, pointsRequired);
        RefreshUI();
        PulseSlider();

        if (_currentPoints >= pointsRequired)
            OnGaugeFull();
    }

    // ── 스킬 발동 ──────────────────────────────────────────────────

    /// <summary>스킬 버튼 클릭 시 호출됩니다.</summary>
    public void TryActivate()
    {
        if (_isActive) return;
        if (_onCooldown) return;
        if (_currentPoints < pointsRequired) return;

        FXManager.Shared?.SpawnPlayerText("FEVER!", new Color(0.12f, 0.73f, 1f));

        _isActive      = true;
        _currentPoints = 0;
        SetButtonInteractable(false);
        RefreshUI();

        SpawnEffect();

        // 플레이어 하이라이트 효과
        PlayerController.Instance?.PlaySkillHighlight();

        if (_skill != null)
        {
            _skill.Activate();
            _activeRoutine = StartCoroutine(SkillDurationRoutine(_skill.Duration));
        }
        else
        {
            ApplyDefaultBoost(true);
            _activeRoutine = StartCoroutine(SkillDurationRoutine(defaultDuration));
        }
    }

    /// <summary>스킬을 즉시 강제 종료합니다.</summary>
    public void ForceStop()
    {
        if (!_isActive) return;
        StopSkill();
    }

    // ── 내부 ───────────────────────────────────────────────────────

    private IEnumerator SkillDurationRoutine(float dur)
    {
        yield return new WaitForSeconds(dur);
        StopSkill();
    }

    private void StopSkill()
    {
        if (_activeRoutine != null) { StopCoroutine(_activeRoutine); _activeRoutine = null; }
        if (_skill != null) _skill.Deactivate();
        else ApplyDefaultBoost(false);
        DespawnEffect();
        if (skillReadyFX) skillReadyFX.SetActive(false);
        _isActive = false;

        if (skillCooldown > 0f)
        {
            _onCooldown = true;
            SetButtonInteractable(false);
            if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = StartCoroutine(CooldownRoutine());
        }
    }

    private IEnumerator CooldownRoutine()
    {
        yield return new WaitForSeconds(skillCooldown);
        _onCooldown = false;
        _cooldownRoutine = null;
    }

    private void SpawnEffect()
    {
        if (skillEffectPrefab == null) return;
        if (_spawnedEffect != null) Destroy(_spawnedEffect);

        var parent = PlayerController.Instance != null ? PlayerController.Instance.transform : transform;
        _spawnedEffect = Instantiate(skillEffectPrefab, parent.position, skillEffectPrefab.transform.rotation, parent);
    }

    private void DespawnEffect()
    {
        if (_spawnedEffect == null) return;
        Destroy(_spawnedEffect);
        _spawnedEffect = null;
    }

    private void ApplyDefaultBoost(bool apply)
    {
        var wps = WeaponManager.Instance?.GetActiveWeaponPoints();
if (wps == null || wps.Count == 0) return;
        float mult = apply ? defaultSpeedMultiplier : 1f / defaultSpeedMultiplier;
        foreach (var wp in wps)
            wp.MultiplyAttackSpeed(mult);
        _defaultBoostActive = apply;
    }

    private void OnGaugeFull()
    {
        if (!_onCooldown) SetButtonInteractable(true);
        if (skillReadyFX) skillReadyFX.SetActive(true);
        if (!_onCooldown) TryActivate();
    }

    private void RefreshUI()
    {
        if (skillGaugeSlider)
            skillGaugeSlider.value = _currentPoints;
    }

    private void PulseSlider()
    {
        if (skillGaugeSlider == null) return;
        if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        var rt       = skillGaugeSlider.transform;
        float dur    = 0.12f;
        float peak   = 1.18f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            float t = elapsed / dur;
            float s = t < 0.5f ? Mathf.Lerp(1f, peak, t * 2f) : Mathf.Lerp(peak, 1f, (t - 0.5f) * 2f);
            rt.localScale = _sliderOriginalScale * s;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        rt.localScale = _sliderOriginalScale;
        _pulseRoutine = null;
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (skillButton) skillButton.interactable = interactable;
    }
}
