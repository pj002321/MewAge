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

    protected override bool DontDestroy => false;

    private CharacterSkillBase _skill;
    private int   _currentPoints;
    private int   _maxPoints;
    private bool  _isActive;
    private Coroutine _activeRoutine;

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

        if (string.IsNullOrEmpty(skillId))
        {
            skillButton?.gameObject.SetActive(false);
            if (skillGaugeSlider) skillGaugeSlider.gameObject.SetActive(false);
            return;
        }

        var prefab = Resources.Load<GameObject>($"Skills/{skillId.ToLower()}");
        if (prefab == null)
        {
            Debug.LogWarning($"[SkillManager] 스킬 프리팹 없음: Resources/Skills/{skillId.ToLower()}");
            skillButton?.gameObject.SetActive(false);
            if (skillGaugeSlider) skillGaugeSlider.gameObject.SetActive(false);
            return;
        }

        // 스킬 오브젝트는 플레이어 자식으로 스폰 — 위치·이동을 자동으로 따라감
        // 플레이어가 없으면 Manager 자식으로 fallback
        var parent = PlayerController.Instance != null
            ? PlayerController.Instance.transform
            : transform;
        var go = Instantiate(prefab, parent);
        go.transform.localPosition = Vector3.zero;
        _skill    = go.GetComponent<CharacterSkillBase>();
        _maxPoints = _skill != null ? _skill.PointsRequired : 10;

        skillButton?.gameObject.SetActive(true);
        if (skillGaugeSlider)
        {
            skillGaugeSlider.gameObject.SetActive(true);
            skillGaugeSlider.minValue = 0f;
            skillGaugeSlider.maxValue = _maxPoints;
            skillGaugeSlider.value    = 0f;
        }

        SetButtonInteractable(false);
        if (skillReadyFX) skillReadyFX.SetActive(false);
    }

    protected override void Awake()
    {
        base.Awake();
        skillButton?.onClick.AddListener(TryActivate);
    }

    // ── 포인트 관리 ─────────────────────────────────────────────────

    /// <summary>적 처치 시 호출합니다. (EnemyController에서 호출)</summary>
    public void AddPoint(int amount = 1)
    {
        if (_skill == null || _isActive) return;
        if (_currentPoints >= _maxPoints) return;

        _currentPoints = Mathf.Min(_currentPoints + amount, _maxPoints);
        RefreshUI();

        if (_currentPoints >= _maxPoints)
            OnGaugeFull();
    }

    // ── 스킬 발동 ──────────────────────────────────────────────────

    /// <summary>스킬 버튼 클릭 시 호출됩니다.</summary>
    public void TryActivate()
    {
        if (_skill == null || _isActive) return;
        if (_currentPoints < _maxPoints) return;

        _isActive      = true;
        _currentPoints = 0;
        SetButtonInteractable(false);
        if (skillReadyFX) skillReadyFX.SetActive(false);
        RefreshUI();

        _skill.Activate();
        _activeRoutine = StartCoroutine(SkillDurationRoutine(_skill.Duration));
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
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
        _skill?.Deactivate();
        _isActive = false;
    }

    private void OnGaugeFull()
    {
        SetButtonInteractable(true);
        if (skillReadyFX) skillReadyFX.SetActive(true);
    }

    private void RefreshUI()
    {
        if (skillGaugeSlider)
            skillGaugeSlider.value = _currentPoints;
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (skillButton) skillButton.interactable = interactable;
    }
}
