using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 기물 슬롯 버튼 하나.
/// 아이콘은 Inspector에서 고정 설정 — 런타임에 변경하지 않습니다.
/// Refresh는 카운트 텍스트 + 버튼 활성화만 담당합니다.
/// 쿨타임 오버레이는 OnClick 시 자동 표시됩니다.
/// </summary>
public class StructureButton : MonoBehaviour
{
    [SerializeField] private int        slotIndex;
    [SerializeField] private Button     button;
    [SerializeField] private Image      iconImage;
    [SerializeField] private TMP_Text   countText;
    [SerializeField] private GameObject indicator;

    [Header("쿨타임 오버레이")]
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private float baseCooldown = 5f;

    [Header("습득 스케일 효과")]
    [SerializeField] private float punchScale    = 1.35f;
    [SerializeField] private float punchUpTime   = 0.12f;
    [SerializeField] private float punchDownTime = 0.1f;

    private bool      _subscribed;
    private bool      _onCooldown;
    private Coroutine _cooldownRoutine;
    private Coroutine _scaleRoutine;
    private int       _prevCount = -1;
    private Vector3   _baseScale;

    private void Awake()
    {
        // iconImage가 Inspector에서 미할당이면 Button의 Image를 자동으로 사용
        if (iconImage == null) iconImage = button?.image;

        _baseScale = transform.localScale;
        button?.onClick.AddListener(OnClick);
        if (cooldownOverlay) { cooldownOverlay.fillAmount = 0f; cooldownOverlay.gameObject.SetActive(false); }
    }

    private void Update()
    {
        if (!_subscribed && StructureInventory.Shared != null)
        {
            StructureInventory.Shared.OnChanged     += Refresh;
            StructureInventory.Shared.OnAutoChanged += RefreshIndicator;
            StructureInventory.Shared.OnSlotUsed    += OnSlotUsed;
            _subscribed = true;
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (_subscribed && StructureInventory.Shared != null)
        {
            StructureInventory.Shared.OnChanged     -= Refresh;
            StructureInventory.Shared.OnAutoChanged -= RefreshIndicator;
            StructureInventory.Shared.OnSlotUsed    -= OnSlotUsed;
        }
        _subscribed = false;
    }

    private void Refresh()
    {
        var inv   = StructureInventory.Shared;
        var data  = inv?.GetData(slotIndex);
        int count = inv?.GetCount(slotIndex) ?? 0;

        if (iconImage != null)
        {
            bool hasItem = data != null && count > 0;
            iconImage.sprite  = hasItem ? StructureWareManager.LoadIcon(data.f_기물ID) : null;
            iconImage.enabled = hasItem;
        }

        // 개수가 늘었을 때(습득) 스케일 펀치
        if (_prevCount >= 0 && count > _prevCount)
            PlayPunch();
        _prevCount = count;

        if (countText) countText.text = count.ToString();
        if (button)    button.interactable = count > 0 && !_onCooldown;

        RefreshIndicator();
    }

    private void PlayPunch()
    {
        if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
        _scaleRoutine = StartCoroutine(PunchRoutine());
    }

    private IEnumerator PunchRoutine()
    {
        Vector3 big = _baseScale * punchScale;

        float t = 0f;
        while (t < punchUpTime)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(_baseScale, big, t / punchUpTime);
            yield return null;
        }

        t = 0f;
        while (t < punchDownTime)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(big, _baseScale, t / punchDownTime);
            yield return null;
        }

        transform.localScale = _baseScale;
        _scaleRoutine = null;
    }

    private void RefreshIndicator()
    {
        if (indicator == null) return;
        var inv = StructureInventory.Shared;
        bool active = inv != null
                   && inv.IsAutoMode
                   && inv.ActiveAutoSlot == slotIndex
                   && inv.GetCount(slotIndex) > 0;
        indicator.SetActive(active);
    }

    private void OnClick()
    {
        if (_onCooldown) return;
        if (Time.timeScale == 0f) return;

        SoundManager.Shared?.Play(SoundKey.UI_Click);
        StructureInventory.Shared?.UseAt(slotIndex, GetButtonWorldPos());
        // cooldown은 OnSlotUsed 이벤트에서 시작
    }

    private void OnSlotUsed(int slot)
    {
        if (slot == slotIndex) StartCooldown();
    }

    private void StartCooldown()
    {
        if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
        _cooldownRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        _onCooldown = true;
        if (button) button.interactable = false;

        float reduction = PlayerStatManager.Shared?.GetFinal(StatType.StructureCooldown) ?? 0f;
        float duration  = Mathf.Max(0.5f, baseCooldown * Mathf.Clamp01(1f - reduction / 100f));

        if (cooldownOverlay) { cooldownOverlay.gameObject.SetActive(true); cooldownOverlay.fillAmount = 1f; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (cooldownOverlay) cooldownOverlay.fillAmount = 1f - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        if (cooldownOverlay) { cooldownOverlay.fillAmount = 0f; cooldownOverlay.gameObject.SetActive(false); }
        _onCooldown      = false;
        _cooldownRoutine = null;

        Refresh();
    }

    private Vector3 GetButtonWorldPos()
    {
        var cam = Camera.main;
        if (cam == null || button == null) return Vector3.zero;
        Vector3 screenPos = button.transform.position;
        float   depth     = Mathf.Abs(cam.transform.position.z);
        Vector3 world     = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        world.z = 0f;
        return world;
    }
}
