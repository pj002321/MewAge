using UnityEngine;
using TMPro;
using Mewage.DB;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 저주/축복 카드 UI.
/// Inspector 연결: titleText, blessingText, curseText, iconImg
/// </summary>
public class CurseRewardCard : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text blessingText;
    [SerializeField] private TMP_Text curseText;
    [SerializeField] private Image iconImg;

    [Header("선택 애니메이션")]
    [SerializeField] private float spinDuration = 0.35f;
    [SerializeField] private float selectScale  = 1.08f;

    private Coroutine _spinCoroutine;

    private void OnDisable()
    {
        if (_spinCoroutine != null) { StopCoroutine(_spinCoroutine); _spinCoroutine = null; }
        transform.localEulerAngles = Vector3.zero;
        transform.localScale       = Vector3.one;
    }

    public void PlaySelectEffect()
    {
        if (_spinCoroutine != null) StopCoroutine(_spinCoroutine);
        _spinCoroutine = StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        float elapsed    = 0f;
        Vector3 endScale = Vector3.one * selectScale;

        while (elapsed < spinDuration)
        {
            float t    = elapsed / spinDuration;
            float ease = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            transform.localEulerAngles = new Vector3(0f, Mathf.Lerp(0f, 360f, ease), 0f);
            transform.localScale       = Vector3.Lerp(Vector3.one, endScale, ease);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localEulerAngles = Vector3.zero;
        transform.localScale       = endScale;
        _spinCoroutine = null;
    }

    public void Setup(DB_증강 data)
    {
        if (data == null) return;
        if (titleText) titleText.text = data.f_이름;

        // 아이콘은 축복 AugId로 매칭 (f_증강ID가 아닌 f_축복 사용)
        if (iconImg)
        {
            string blessingId = data.f_축복;
            Sprite sprite = ImprovVisualConfig.Shared?.GetSprite(blessingId)
                         ?? (string.IsNullOrEmpty(blessingId) ? null
                             : Resources.Load<Sprite>("AUG/" + blessingId.ToLower()));
            iconImg.sprite  = sprite;
            iconImg.enabled = sprite != null;
        }

        if (blessingText)
            blessingText.text = AugmentHelper.BuildEffectLineDouble(data.f_축복, data.f_축복수치, positive: true) ?? "-";

        if (curseText)
            curseText.text = AugmentHelper.BuildEffectLineDouble(data.f_저주, data.f_저주수치, positive: false) ?? "-";

        var button = GetComponent<Button>();
        if (button) button.interactable = true;
    }
}
