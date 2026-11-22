using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class TurnManager : MonoBehaviour
{
    [SerializeField] private List<Character> turnOrder = new();
    private int currentIndex = 0;
    
    private void Start()
    {
        StartCoroutine(InitializeAndStart());
    }

    private IEnumerator InitializeAndStart()
    {
        // esperar a que spawnee todo
        yield return null;

        var allCharacters = new List<Character>(FindObjectsOfType<Character>());

        // ordenar: primero el player, después los enemigos
        allCharacters.Sort((a, b) => b.IsPlayer.CompareTo(a.IsPlayer));

        turnOrder = allCharacters;

        StartCoroutine(TurnLoop());
    }

    private IEnumerator TurnLoop()
    {
        bool resumeSameTurn = false;
        while (!GameSession.IsGameOver && turnOrder.Count > 0)
        {
            currentIndex %= turnOrder.Count;
            Character current = turnOrder[currentIndex];
            if (!CanTakeTurn(current))
            {
                currentIndex = (currentIndex + 1) % turnOrder.Count;
                yield return null;
                continue;
            }
            CurrentCharacter = current;
            if (!resumeSameTurn) GameEvents.OnTurnStarted?.Invoke(current);
            resumeSameTurn = false;
            var ai = current.GetComponent<IEnemyAI>();
            aiFinished = ai == null;
            if (ai != null && immediateTurn == null && CanTakeTurn(current))
            {
                aiRoutine = new InterruptibleRoutine(ai.TakeTurn());
                aiCoroutine = StartCoroutine(ExecuteAI(aiRoutine));
            }
            while (!GameSession.IsGameOver && immediateTurn == null && CanTakeTurn(current) &&
                (ai != null ? !aiFinished : !current.IsTurnFinished))
                yield return null;

            CancelAI();
            if (immediateTurn != null && CanTakeTurn(immediateTurn))
            {
                Character next = immediateTurn;
                immediateTurn = null;
                GameEvents.OnTurnInterrupted?.Invoke(current);
                if (next != current)
                {
                    current.FinishTurn();
                    GameEvents.OnTurnEnded?.Invoke(current);
                }
                else resumeSameTurn = true; // Do not tick statuses/energy twice.
                currentIndex = turnOrder.IndexOf(next);
                continue;
            }
            immediateTurn = null;
            GameEvents.OnTurnEnded?.Invoke(current);
            currentIndex = (currentIndex + 1) % turnOrder.Count;
            yield return null;
        }
        CurrentCharacter = null;
    }
}
