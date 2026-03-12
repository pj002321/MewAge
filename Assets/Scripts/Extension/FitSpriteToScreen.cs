using UnityEngine;

/// <summary>
/// 부착된 SpriteRenderer를 카메라 화면에 꽉 차도록 스케일합니다.
/// 배경 등 전체 화면을 채워야 하는 스프라이트에 사용하세요.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FitSpriteToScreen : MonoBehaviour
{
    public enum FitMode
    {
        Fill,   // 화면을 빈틈없이 채움 (스프라이트 일부 잘릴 수 있음)
        Fit,    // 스프라이트 전체가 보이도록 맞춤 (여백 생길 수 있음)
        Stretch // 가로/세로 독립 스케일 (비율 무시)
    }

    [SerializeField] private FitMode fitMode = FitMode.Fill;

    private void Start()
    {
        Fit();
    }

    private void Fit()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null) return;

        Vector2 spriteSize = sr.sprite.bounds.size;

        float camHeight = cam.orthographicSize * 2f;
        float camWidth  = camHeight * cam.aspect;

        float scaleX = camWidth  / spriteSize.x;
        float scaleY = camHeight / spriteSize.y;

        float scale = fitMode switch
        {
            FitMode.Fill    => Mathf.Max(scaleX, scaleY),
            FitMode.Fit     => Mathf.Min(scaleX, scaleY),
            FitMode.Stretch => 1f, // 아래에서 x/y 개별 처리
            _               => 1f
        };

        if (fitMode == FitMode.Stretch)
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
        else
            transform.localScale = new Vector3(scale, scale, 1f);
    }

#if UNITY_EDITOR
    // 에디터에서 해상도 바꿀 때 즉시 반영
    private void OnValidate() => Fit();
#endif
}
