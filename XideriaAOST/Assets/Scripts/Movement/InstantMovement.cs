using UnityEngine;

public class InstantMovement : IMovementStrategy
{
    public void Move(Transform character, Vector3 targetPosition)
    {
        character.position = targetPosition;
    }
}
