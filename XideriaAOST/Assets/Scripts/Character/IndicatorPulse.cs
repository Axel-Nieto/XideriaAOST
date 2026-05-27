using UnityEngine;

public class IndicatorPulse : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseAmount = 0.08f;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }
}
