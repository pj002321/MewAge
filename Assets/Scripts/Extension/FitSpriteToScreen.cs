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
        Fill,     // 화면을 빈틈없이 채움 (스프라이트 일부 잘릴 수 있음)
        Fit,      // 스프라이트 전체가 보이도록 맞춤 (여백 생길 수 있음)
        Stretch,  // 가로/세로 독립 스케일 (비율 무시)
        FitWidth, // 좌우 끝만 화면에 딱 맞춤 (세로는 비율 유지)
        FitHeight // 상하 끝만 화면에 딱 맞춤 (가로는 비율 유지)
    }

    [SerializeField] private FitMode fitMode = FitMode.Fill;

    [Tooltip("FitWidth 시 카메라 orthographicSize를 스프라이트 높이에 맞게 자동 조절 (위아래 여백 제거)")]
    [SerializeField] private bool adjustCameraToSprite = false;

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

        if (fitMode == FitMode.Stretch)
        {
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
            return;
        }

        float scale = fitMode switch
        {
            FitMode.Fill      => Mathf.Max(scaleX, scaleY),
            FitMode.Fit       => Mathf.Min(scaleX, scaleY),
            FitMode.FitWidth  => scaleX,
            FitMode.FitHeight => scaleY,
            _                 => 1f
        };

        transform.localScale = new Vector3(scale, scale, 1f);

        // 카메라 orthographicSize를 스케일된 스프라이트 높이에 맞게 조정 (여백 제거)
        // OnValidate는 에디터 모드에서도 호출되므로, 카메라 값 변경은 플레이 중에만 허용
        if (adjustCameraToSprite && Application.isPlaying)
            cam.orthographicSize = spriteSize.y * scale / 2f;
    }

#if UNITY_EDITOR
    // 씬에 올라간 인스턴스에서만 반영 (프리팹 에셋 자체의 스케일 오염 방지)
    private void OnValidate()
    {
        if (!gameObject.scene.isLoaded) return;
        Fit();
    }
#endif
}
