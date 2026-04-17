using System.Collections;
using UnityEngine;

public class Character : MonoBehaviour
{
    [SerializeField] private Vector2Int startGridPosition;
    public CharacterStats Stats { get; private set; }

    public CharacterMovement Movement { get; private set; }

    public Vector2Int GridPosition { get; private set; }
    public bool IsTurnFinished { get; private set; }
    private GridSystem _grid;
    [SerializeField] private float visualYOffset = 0.5f;
    [SerializeField] private bool isPlayer;
    public bool IsPlayer => isPlayer;

    private void Awake()
    {
        Stats = new CharacterStats(
            attack: 5,
            defense: 2,
            maxHP: 20,
            maxEnergy: 7,
            energyRegen: 3
        );

        Movement = GetComponent<CharacterMovement>();
        InitializePosition();
    }

    private void OnEnable()
    {
        GameEvents.OnTurnStarted += HandleTurnStarted;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= HandleTurnStarted;
    }

    private void InitializePosition()
    {
        if (_grid == null)
            _grid = FindObjectOfType<GridSystem>();

        GridPosition = startGridPosition;

        transform.position = _grid.GridToWorld(startGridPosition)
                             + Vector3.up * visualYOffset;

        _grid.RegisterCharacter(startGridPosition, this);
    }

    private void HandleTurnStarted(Character character)
    {
        if (character != this) return;

        // agregar buffs/debuffs acá después
    }

    public void Initialize(CharacterStats stats, CharacterMovement movement, GridSystem grid, Vector2Int startPos)
    {
        _grid = grid;
        Stats = stats;
        Movement = movement;
        GridPosition = startPos;

        transform.position = _grid.GridToWorld(startPos);

        _grid.RegisterCharacter(startPos, this);
    }


    public void SetGridPosition(Vector2Int newPosition)
    {
        GridPosition = newPosition;
    }

    public void TakeDamage(int amount)
    {
        Stats.TakeDamage(amount);

        if (!Stats.IsAlive)
        {
            Die();
        }
    }

    private void Die()
    {
        _grid.UnregisterCharacter(GridPosition);
        gameObject.SetActive(false);
    }

    public IEnumerator ExecuteAction(IAction action)
    {
        if (!action.CanExecute(this))
            yield break;

        yield return action.Execute(this);
    }

    public void FinishTurn()
    {
        IsTurnFinished = true;
    }

    public void ResetTurn()
    {
        IsTurnFinished = false;

        Stats.RegenEnergy();
    }
}