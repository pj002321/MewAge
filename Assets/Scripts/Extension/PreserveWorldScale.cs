using UnityEngine;

public class PreserveWorldScale : MonoBehaviour
{
    private Vector3 _initialWorldScale;

    private void Awake()
    {
        _initialWorldScale = transform.lossyScale; // 월드 스케일 기억
    }

    private void LateUpdate()
    {
        // 부모 스케일 변화를 역산해서 상쇄
        Vector3 parentScale = transform.parent.lossyScale;
        transform.localScale = new Vector3(
            _initialWorldScale.x / parentScale.x,
            _initialWorldScale.y / parentScale.y,
            _initialWorldScale.z / parentScale.z
        );
    }
}
