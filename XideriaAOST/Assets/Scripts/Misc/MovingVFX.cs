using UnityEngine;

public class MovingVFX : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float destroyDelay = 0f;

    private Vector3 target;
    private bool isMoving;

    public void Initialize(Vector3 targetPosition, float moveSpeed)
    {
        target = targetPosition;
        speed = moveSpeed;
        isMoving = true;
    }

    private void Update()
    {
        if (!isMoving)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, target) <= 0.05f)
        {
            isMoving = false;
            Destroy(gameObject, destroyDelay);
        }
    }
}
