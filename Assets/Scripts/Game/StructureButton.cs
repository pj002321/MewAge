using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 기물 슬롯 버튼 하나.
/// 쿨타임 상태는 StructureInventory에서 관리하며, 이 클래스는 시각만 담당합니다.
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

    [Header("습득 스케일 효과")]
    [SerializeField] private float punchScale    = 1.35f;
    [SerializeField] private float punchUpTime   = 0.12f;
    [SerializeField] private float punchDownTime = 0.1f;

    private bool      _subscribed;
    private Coroutine _scaleRoutine;
    private int       _prevCount = -1;
    private Vector3   _baseScale;

    private void Awake()
    {
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
            _subscribed = true;
            Refresh();
        }

        if (!_subscribed) return;

        // 쿨타임 오버레이 매 프레임 갱신
        var inv = StructureInventory.Shared;
        if (inv == null) return;

        bool  onCD = inv.IsOnCooldown(slotIndex);
        float fill = inv.GetCooldownProgress(slotIndex);

        if (cooldownOverlay)
        {
            cooldownOverlay.gameObject.SetActive(onCD);
            if (onCD) cooldownOverlay.fillAmount = fill;
        }

        if (button) button.interactable = !onCD && inv.GetCount(slotIndex) > 0;
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

        if (iconImage != null)
        {
            bool hasItem = data != null && count > 0;
            iconImage.sprite  = hasItem ? StructureWareManager.LoadIcon(data.f_기물ID) : null;
            iconImage.enabled = hasItem;
        }

        if (_prevCount >= 0 && count > _prevCount)
            PlayPunch();
        _prevCount = count;

        if (countText) countText.text = count.ToString();

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
        var inv = StructureInventory.Shared;
        if (inv == null || inv.IsOnCooldown(slotIndex)) return;
        if (Time.timeScale == 0f) return;

        SoundManager.Shared?.Play(SoundKey.UI_Click);
        inv.UseAt(slotIndex, GetButtonWorldPos());
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
