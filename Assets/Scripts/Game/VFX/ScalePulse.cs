using UnityEngine;

/// <summary>
/// 오브젝트의 스케일을 주기적으로 커졌다 줄어들게 합니다.
/// </summary>
public class ScalePulse : MonoBehaviour
{
    [Tooltip("기준 스케일 배율 (1 = 원본 스케일)")]
    [SerializeField] private float minScale  = 0.9f;
    [Tooltip("최대 스케일 배율")]
    [SerializeField] private float maxScale  = 1.1f;
    [Tooltip("1사이클(min→max→min) 완료 시간 (초)")]
    [SerializeField] private float period    = 0.8f;
    [Tooltip("true면 X·Y 비율 유지, false면 X만 펄스")]
    [SerializeField] private bool  uniform   = true;

    private Vector3 _originScale;

    private void Awake()  => _originScale = transform.localScale;
    private void OnEnable() => transform.localScale = _originScale;

    private void Update()
    {
        // 0→1→0 사인파 (period 주기)
        float t    = (Mathf.Sin(Time.time * (2f * Mathf.PI / period) - Mathf.PI * 0.5f) + 1f) * 0.5f;
        float mult = Mathf.Lerp(minScale, maxScale, t);

        transform.localScale = uniform
            ? _originScale * mult
            : new Vector3(_originScale.x * mult, _originScale.y, _originScale.z);
    }

    private void OnDisable() => transform.localScale = _originScale;
}
