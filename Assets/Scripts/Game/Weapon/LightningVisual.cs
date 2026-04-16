using UnityEngine;

/// <summary>
/// Handles the visual representation of the lightning bolt.
/// Draws a jagged line between the source and the target.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LightningVisual : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    [SerializeField] private int segments = 10;
    [SerializeField] private float jitterAmount = 0.2f;
    [SerializeField] private float flickerSpeed = 0.05f;

    private Transform _source;
    private Transform _target;
    private float _flickerTimer;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
    }

    public void Setup(Transform source, Transform target)
    {
        _source = source;
        _target = target;
    }

    private void LateUpdate()
    {
        if (_source == null || _target == null)
        {
            _lineRenderer.enabled = false;
            return;
        }

        _lineRenderer.enabled = true;
        _flickerTimer -= Time.deltaTime;

        if (_flickerTimer <= 0f)
        {
            UpdateLightningPositions();
            _flickerTimer = flickerSpeed;
        }
    }

    private void UpdateLightningPositions()
    {
        _lineRenderer.positionCount = segments + 1;
        Vector3 start = _source.position;
        Vector3 end = _target.position;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 pos = Vector3.Lerp(start, end, t);

            if (i > 0 && i < segments)
            {
                pos += (Vector3)Random.insideUnitCircle * jitterAmount;
            }

            _lineRenderer.SetPosition(i, pos);
        }
    }
}
