using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Image(또는 RectTransform)를 지속적으로 회전시킵니다.
/// Image 컴포넌트가 없어도 동작합니다 (RectTransform만 있으면 됨).
/// </summary>
public class ImageRotator : MonoBehaviour
{
    [Header("회전 속도 (도/초, 양수=반시계, 음수=시계)")]
    public float speed = 90f;

    [Header("로컬 회전 기준 (true) / 월드 기준 (false)")]
    public bool localRotation = true;

    void Update()
    {
        float delta = speed * Time.unscaledDeltaTime;
        if (localRotation)
            transform.Rotate(0f, 0f, delta, Space.Self);
        else
            transform.Rotate(0f, 0f, delta, Space.World);
    }
}
