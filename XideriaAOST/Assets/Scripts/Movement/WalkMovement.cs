using UnityEngine;
using System.Collections;

public class WalkMovement : IMovementStrategy
{
    private MonoBehaviour owner;
    private float speed;

    public WalkMovement(MonoBehaviour owner, float speed)
    {
        this.owner = owner;
        this.speed = speed;
    }

    public void Move(Transform character, Vector3 targetPosition)
    {
        owner.StartCoroutine(MoveCoroutine(character, targetPosition));
    }

    private IEnumerator MoveCoroutine(Transform character, Vector3 target)
    {
        while (Vector3.Distance(character.position, target) > 0.01f)
        {
            character.position = Vector3.MoveTowards(
                character.position,
                target,
                speed * Time.deltaTime
            );

            yield return null;
        }

        character.position = target;
    }
}