using System.Collections.Generic;
using UnityEngine;

public interface ITargetingStrategy
{
    List<Character> GetTargets(Character actor, Vector2Int selectedCell);
}