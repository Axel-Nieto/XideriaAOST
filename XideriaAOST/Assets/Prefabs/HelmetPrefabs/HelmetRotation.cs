using UnityEngine;

public class HelmetRotation : MonoBehaviour
{
    private Vector3 rot = new Vector3(0, -100, 0);
    private float Speed = 2f;
    private float Height = 0.2f;
    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }
    private void Update()
    {
        transform.Rotate(rot * Time.deltaTime);

        float newY = startPosition.y + Mathf.Sin(Time.time * Speed) * Height;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}
