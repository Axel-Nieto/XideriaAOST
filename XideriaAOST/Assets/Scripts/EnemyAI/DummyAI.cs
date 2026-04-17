using System.Collections;
using UnityEngine;

public class DummyAI : MonoBehaviour, IEnemyAI
{
    private Character _character;

    private void Awake()
    {
        _character = GetComponent<Character>();
    }

    public IEnumerator TakeTurn()
    {
        yield return new WaitForSeconds(0.5f);

        _character.FinishTurn();
    }
}