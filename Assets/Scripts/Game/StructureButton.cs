using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 기물 슬롯 버튼 하나.
/// Inspector: slotIndex(0~3), button, iconImage, countText 연결
/// </summary>
public class StructureButton : MonoBehaviour
{
    [SerializeField] private int      slotIndex;
    [SerializeField] private Button   button;
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject indicator;

    private bool subscribed;

    private void Awake()
    {
        button?.onClick.AddListener(OnClick);
        SetEmpty();
    }

    private void Update()
    {
        // StructureInventory가 늦게 초기화될 수 있어 매 프레임 구독 시도
        if (!subscribed && StructureInventory.Shared != null)
        {
            StructureInventory.Shared.OnChanged     += Refresh;
            StructureInventory.Shared.OnAutoChanged += RefreshIndicator;
            subscribed = true;
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (subscribed && StructureInventory.Shared != null)
        {
            StructureInventory.Shared.OnChanged     -= Refresh;
            StructureInventory.Shared.OnAutoChanged -= RefreshIndicator;
        }
        subscribed = false;
    }

    private void Refresh()
    {
        var inv   = StructureInventory.Shared;
        var data  = inv?.GetData(slotIndex);
        int count = inv?.GetCount(slotIndex) ?? 0;

        if (data != null && count > 0)
        {
            Sprite icon = LoadStructureIcon(data.f_기물ID);

            if (iconImage != null) { iconImage.sprite = icon; iconImage.enabled = icon != null; }
            if (countText != null) countText.text = count.ToString();
            if (button    != null) button.interactable = true;
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
    {
        if (string.IsNullOrEmpty(id)) return null;
        string[] parts   = id.Split('_');
        string   chapter = parts.Length > 0 ? parts[0] : id;
        string   path    = $"Ware/{chapter}/{id.ToLower()}";
        var prefab = Resources.Load<GameObject>(path);
        return prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>()?.sprite : null;
    }

    private void SetEmpty()
    {
        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (countText != null) countText.text = "";
        if (button    != null) button.interactable = false;
    }

    private void OnClick()
    {
        SoundManager.Shared?.Play(SoundKey.UI_Click);
        StructureInventory.Shared?.UseAt(slotIndex, GetButtonWorldPos());
    }

    /// <summary>Screen Space – Overlay 버튼 위치를 게임 월드 좌표로 변환합니다.</summary>
    private Vector3 GetButtonWorldPos()
    {
        var cam = Camera.main;
        if (cam == null || button == null) return Vector3.zero;

        Vector3 screenPos = button.transform.position; // SS-Overlay: position = 화면 픽셀
        float   depth     = Mathf.Abs(cam.transform.position.z);
        Vector3 world     = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        world.z = 0f;
        return world;
    }
}
