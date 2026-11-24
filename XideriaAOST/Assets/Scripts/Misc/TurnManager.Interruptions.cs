using System.Collections;
using UnityEngine;

public partial class TurnManager
{
    public Character CurrentCharacter { get; private set; }
    public bool HasPendingImmediateTurn => immediateTurn != null;
    private Character immediateTurn;
    private InterruptibleRoutine aiRoutine;
    private Coroutine aiCoroutine;
    private bool aiFinished;

    public bool RequestImmediateTurn(Character character)
    {
        if (!CanTakeTurn(character) || character.IsPlayer || GameSession.IsGameOver) return false;
        if (!turnOrder.Contains(character)) turnOrder.Add(character);
        immediateTurn = character;
        return true;
    }

    private static bool CanTakeTurn(Character actor) => actor != null && actor.Stats != null &&
        actor.Stats.IsAlive && !actor.IsDying && actor.gameObject.activeInHierarchy;

    private IEnumerator ExecuteAI(InterruptibleRoutine routine)
    {
        try { yield return routine; }
        finally { if (ReferenceEquals(aiRoutine, routine)) aiFinished = true; }
    }

    private void CancelAI()
    {
        aiRoutine?.Cancel();
        if (aiCoroutine != null) StopCoroutine(aiCoroutine);
        aiRoutine = null;
        aiCoroutine = null;
    }

    private void OnDisable()
    {
        CancelAI();
        StopAllCoroutines();
        CurrentCharacter = null;
    }
}
