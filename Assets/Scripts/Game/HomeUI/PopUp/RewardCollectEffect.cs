using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// rewardContent 직접 자식 아이템들을 중앙으로 모은 뒤 사방으로 퍼트리는 연출 컴포넌트.
public class RewardCollectEffect : MonoBehaviour
{
    const float GatherTime  = 0.18f;
    const float BurstTime   = 0.22f;
    const float BurstRadius = 200f;
    const float StaggerStep = 0.02f;

    public bool IsPlaying { get; private set; }

    public void Play(RectTransform rewardContent, RectTransform center)
    {
        if (rewardContent == null || center == null || rewardContent.childCount == 0) return;
        StartCoroutine(PlayRoutine(rewardContent, center));
    }

    IEnumerator PlayRoutine(RectTransform rewardContent, RectTransform center)
    {
        IsPlaying = true;

        Canvas rootCanvas = null;
        for (var t = transform; t != null; t = t.parent)
        {
            var c = t.GetComponent<Canvas>();
            if (c != null && c.isRootCanvas) { rootCanvas = c; break; }
        }
        if (rootCanvas == null) { IsPlaying = false; yield break; }

        int     count     = rewardContent.childCount;
        var     items     = new RectTransform[count];
        var     ghosts    = new RectTransform[count];
        var     ghostCGs  = new CanvasGroup[count];
        var     startPos  = new Vector3[count];
        var     itemCGs   = new CanvasGroup[count];
        Vector3 centerPos = center.position;

        for (int i = 0; i < count; i++)
        {
            items[i]    = rewardContent.GetChild(i) as RectTransform;
            startPos[i] = items[i].position;

            var ghostGO = Instantiate(items[i].gameObject, rootCanvas.transform);
            var ghost   = ghostGO.GetComponent<RectTransform>();
            ghost.position  = startPos[i];
            ghost.sizeDelta = items[i].sizeDelta;
            foreach (var g in ghostGO.GetComponentsInChildren<Graphic>())
                g.raycastTarget = false;
            ghosts[i]   = ghost;
            ghostCGs[i] = ghostGO.AddComponent<CanvasGroup>();

            var cg = items[i].GetComponent<CanvasGroup>();
            if (!cg) cg = items[i].gameObject.AddComponent<CanvasGroup>();
            itemCGs[i] = cg;
        }

        // Phase 1: 중앙으로 모으기
        float elapsed = 0f;
        while (elapsed < GatherTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t    = Mathf.Clamp01(elapsed / GatherTime);
            float ease = t * t;
            for (int i = 0; i < count; i++)
            {
                if (ghosts[i]  != null) ghosts[i].position  = Vector3.Lerp(startPos[i], centerPos, ease);
                if (itemCGs[i] != null) itemCGs[i].alpha = 1f - ease;
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (ghosts[i]  != null) ghosts[i].position = centerPos;
            if (itemCGs[i] != null) itemCGs[i].alpha   = 0f;
        }

        float pause = 0f;
        while (pause < 0.05f) { pause += Time.unscaledDeltaTime; yield return null; }

        // Phase 2: 사방으로 퍼지기
        var burstTargets = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float angle = ((360f / count * i) + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
            float dist  = Random.Range(BurstRadius * 0.7f, BurstRadius);
            burstTargets[i] = centerPos + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
        }

        for (int i = 0; i < count; i++)
        {
            if (ghosts[i] != null)
                StartCoroutine(BurstOne(ghosts[i], ghostCGs[i], centerPos, burstTargets[i], BurstTime, i * StaggerStep));
        }

        float waitEnd = BurstTime + count * StaggerStep + 0.05f;
        float waitElapsed = 0f;
        while (waitElapsed < waitEnd) { waitElapsed += Time.unscaledDeltaTime; yield return null; }

        IsPlaying = false;
    }

    IEnumerator BurstOne(RectTransform ghost, CanvasGroup cg, Vector3 from, Vector3 to, float duration, float delay)
    {
        if (delay > 0f)
        {
            float d = 0f;
            while (d < delay) { d += Time.unscaledDeltaTime; yield return null; }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ghost == null) yield break;
            elapsed    += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float ease  = 1f - (1f - t) * (1f - t);
            ghost.position = Vector3.Lerp(from, to, ease);
            cg.alpha       = 1f - t;
            yield return null;
        }

        if (ghost != null) Destroy(ghost.gameObject);
    }
}
