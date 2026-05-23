using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 유물 조각 뽑기 결과 슬롯 뷰.
/// 프리팹 구성: frameImg (등급 프레임), iconImg (유물 아이콘), countText (조각 수량).
/// </summary>
public class RelicFragSlotView : MonoBehaviour
{
    [SerializeField] Image    frameImg;
    [SerializeField] Image    iconImg;
    [SerializeField] TMP_Text countText;

    public void Bind(RelicData data, int count)
    {
        var cfg = RelicVisualConfig.Shared;

        if (iconImg)
            iconImg.sprite = cfg?.GetRelicIcon(data?.relicId);

        if (frameImg)
        {
            var grade = data != null
                ? RelicVisualConfig.ParseGrade(data.grade)
                : RelicVisualConfig.Grade.Common;
            frameImg.sprite = cfg?.GetIconSprite(grade);
        }

        if (countText)
            countText.text = $"x{count}";
    }

    public void PlayScaleUp(float delay = 0f)
    {
        StartCoroutine(ScaleUpRoutine(delay));
    }

    IEnumerator ScaleUpRoutine(float delay)
    {
        transform.localScale = Vector3.zero;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float dur = 0.25f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            transform.localScale = Vector3.one * BounceOut(Mathf.Clamp01(t / dur));
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    static float BounceOut(float t)
    {
        if (t < 0.7f) return Mathf.Lerp(0f, 1.1f, t / 0.7f);
        return Mathf.Lerp(1.1f, 1f, (t - 0.7f) / 0.3f);
    }
}
