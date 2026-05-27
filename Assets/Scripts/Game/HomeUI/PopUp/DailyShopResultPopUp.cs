using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 구매 완료 후 보상 아이템을 뿅뿅뿅 보여주는 결과 팝업.
/// ※ 파일명을 ShopResultPopUp.cs 로 리네임하세요.
/// </summary>
public class ShopResultPopUp : MonoBehaviour
{
    [Header("슬롯")]
    [SerializeField] private RewardSlot slotPrefab;
    [SerializeField] private Transform  slotContainer;

    [Header("버튼")]
    [SerializeField] private Button closeButton;

    [Header("연출")]
    [SerializeField] private float slotPopDuration = 0.28f;
    [SerializeField] private float slotStagger     = 0.1f;

    private void Awake()
    {
        closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
        gameObject.SetActive(false);
    }

    // ── 공개 API ─────────────────────────────────────────────────────

    /// <summary>단일 아이템 (일일상점 / 골드상점 / 루비상점)</summary>
    public void Show(Sprite icon, int amount)
    {
        ShowItems(new[] { (icon, amount) });
    }

    /// <summary>복수 아이템 (챕터 패키지)</summary>
    public void Show(params (Sprite icon, int amount)[] items)
    {
        ShowItems(items);
    }

    // ── 내부 ─────────────────────────────────────────────────────────

    private void ShowItems((Sprite icon, int amount)[] items)
    {
        ClearSlots();
        gameObject.SetActive(true);
        SoundManager.Shared?.Play(SoundKey.UI_Buy);

        for (int i = 0; i < items.Length; i++)
            SpawnSlot(items[i].icon, items[i].amount, delay: i * slotStagger);
    }

    private void SpawnSlot(Sprite icon, int amount, float delay)
    {
        if (slotPrefab == null || slotContainer == null) return;

        var slot = Instantiate(slotPrefab, slotContainer);
        slot.Setup(icon, $"×{amount}");

        var rt = slot.GetComponent<RectTransform>();
        rt.localScale = Vector3.zero;

        StartCoroutine(PopInRoutine(rt, delay));
    }

    private void ClearSlots()
    {
        if (slotContainer == null) return;
        for (int i = slotContainer.childCount - 1; i >= 0; i--)
            Destroy(slotContainer.GetChild(i).gameObject);
    }

    private IEnumerator PopInRoutine(RectTransform rt, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;
        while (elapsed < slotPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(elapsed / slotPopDuration));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
