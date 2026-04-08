using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 기물 슬롯 버튼 하나.
/// Inspector: slotIndex(0~3), button, iconImage, countText, cooldownOverlay 연결
///
/// 쿨타임: 사용 후 baseCooldown초 동안 버튼 비활성 + 오버레이 fillAmount 감소
/// 실제 쿨타임 = baseCooldown × (1 - StructureCooldown스탯 / 100)
/// </summary>
public class StructureButton : MonoBehaviour
{
    [SerializeField] private int        slotIndex;
    [SerializeField] private Button     button;
    [SerializeField] private Image      iconImage;
    [SerializeField] private TMP_Text   countText;
    [SerializeField] private GameObject indicator;

    [Header("쿨타임 오버레이")]
    [Tooltip("Image (Fill Method: Radial360 또는 Filled). 쿨타임 중 fillAmount 0→1 감소.")]
    [SerializeField] private Image cooldownOverlay;
    [Tooltip("기본 쿨타임 (초). StructureCooldown 스탯으로 감소됩니다.")]
    [SerializeField] private float baseCooldown = 5f;

    private bool      _subscribed;
    private bool      _onCooldown;
    private Coroutine _cooldownRoutine;

    private void Awake()
    {
        button?.onClick.AddListener(OnClick);
        if (cooldownOverlay) cooldownOverlay.fillAmount = 0f;
        SetEmpty();
    }

    private void Update()
    {
        // StructureInventory가 늦게 초기화될 수 있어 매 프레임 구독 시도
        if (!_subscribed && StructureInventory.Shared != null)
        {
            StructureInventory.Shared.OnChanged     += Refresh;
            StructureInventory.Shared.OnAutoChanged += RefreshIndicator;
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
        }
        _subscribed = false;
    }

    private void Refresh()
    {
        var inv   = StructureInventory.Shared;
        var data  = inv?.GetData(slotIndex);
        int count = inv?.GetCount(slotIndex) ?? 0;

        if (data != null && count > 0)
        {
            if (iconImage != null)
            {
                var icon = LoadStructureIcon(data.f_기물ID);
                iconImage.sprite  = icon;
                iconImage.enabled = icon != null;
            }
            if (countText != null) countText.text = count.ToString();

            // 쿨타임 중이 아닐 때만 버튼 활성화
            if (button != null) button.interactable = !_onCooldown;
        }
        else
        {
            SetEmpty();
        }

        RefreshIndicator();
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

    private static Sprite LoadStructureIcon(string id)
        => StructureWareManager.LoadIcon(id);

    private void SetEmpty()
    {
        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (countText != null) countText.text = "";
        if (button    != null) button.interactable = false;
    }

    private void OnClick()
    {
        if (_onCooldown) return;

        SoundManager.Shared?.Play(SoundKey.UI_Click);
        StructureInventory.Shared?.UseAt(slotIndex, GetButtonWorldPos());
        StartCooldown();
    }

    // ── 쿨타임 ─────────────────────────────────────────────────────────

    private void StartCooldown()
    {
        if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
        _cooldownRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        _onCooldown = true;
        if (button) button.interactable = false;

        // StructureCooldown 스탯 (%) → 실제 쿨타임 계산
        float reduction   = PlayerStatManager.Shared?.GetFinal(StatType.StructureCooldown) ?? 0f;
        float duration    = baseCooldown * Mathf.Clamp01(1f - reduction / 100f);
        duration          = Mathf.Max(0.5f, duration); // 최소 0.5초 보장

        if (cooldownOverlay) cooldownOverlay.fillAmount = 1f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (cooldownOverlay)
                cooldownOverlay.fillAmount = 1f - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        if (cooldownOverlay) cooldownOverlay.fillAmount = 0f;
        _onCooldown      = false;
        _cooldownRoutine = null;

        // 쿨타임 종료 후 슬롯에 기물이 있으면 버튼 재활성화
        Refresh();
    }

    // ── 유틸 ───────────────────────────────────────────────────────────

    /// <summary>Screen Space – Overlay 버튼 위치를 게임 월드 좌표로 변환합니다.</summary>
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
