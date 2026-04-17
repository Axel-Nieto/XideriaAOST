using UnityEngine;

public interface IMovementStrategy
{
    void Move(Transform character, Vector3 targetPosition);
}
