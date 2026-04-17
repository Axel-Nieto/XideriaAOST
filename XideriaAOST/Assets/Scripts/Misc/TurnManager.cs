using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
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
        while (true)
        {
            Character current = turnOrder[currentIndex];

            GameEvents.OnTurnStarted?.Invoke(current);

            var ai = current.GetComponent<IEnemyAI>();

            if (ai != null)
            {
                yield return ai.TakeTurn();
            }
            else
            {
                yield return new WaitUntil(() => current.IsTurnFinished);
            }

            GameEvents.OnTurnEnded?.Invoke(current);

            current.ResetTurn();

            currentIndex = (currentIndex + 1) % turnOrder.Count;
        }
    }
}
