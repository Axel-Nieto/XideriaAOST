using System.Collections.Generic;
using UnityEngine;

public class GridOccupancySystem
{
    private Dictionary<Vector2Int, Character> _occupied = new();

    public void Register(Character character, Vector2Int position)
    {
        _occupied[position] = character;
    }

    public void Unregister(Vector2Int position)
    {
        if (_occupied.ContainsKey(position))
            _occupied.Remove(position);
    }

    public void Move(Character character, Vector2Int from, Vector2Int to)
    {
        Unregister(from);
        Register(character, to);
    }

    public Character GetCharacterAt(Vector2Int position)
    {
        _occupied.TryGetValue(position, out var character);
        return character;
    }

    public bool IsOccupied(Vector2Int position)
    {
        return _occupied.ContainsKey(position);
    }
}