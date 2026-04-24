using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    [SerializeField] private Vector2Int startGridPosition;
    [SerializeField] private CharacterStatsData baseStats;
    public HelmetData EquippedHelmet { get; private set; }
    public CharacterStats Stats { get; private set; }
    public CharacterMovement Movement { get; private set; }

    public Vector2Int GridPosition { get; private set; }
    public bool IsTurnFinished { get; private set; }
    private GridSystem _grid;
    [SerializeField] private float visualYOffset = 0.5f;
    [SerializeField] private bool isPlayer;
    [SerializeField] private List<SkillBase> characterSkills = new();
    public bool IsPlayer => isPlayer;

    public SkillBase BasicSkill { get; private set; }

    private void Awake()
    {
        Stats = new CharacterStats(
            attack: baseStats.attack,
            defense: baseStats.defense,
            maxHP: baseStats.maxHP,
            maxEnergy: baseStats.maxEnergy,
            energyRegen: baseStats.energyRegen
        );

        Movement = GetComponent<CharacterMovement>();

        EquippedHelmet = HelmetFactory.CreateDefaultHelmet();

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

    public void TakeDamage(int amount, DamageType damageType)
    {
        var floatingText = GetComponent<FloatingDamageSpawner>();

        if (amount <= 0)
        {
            floatingText?.ShowMiss();
            return;
        }

        Stats.TakeDamage(amount);
        floatingText?.ShowDamage(amount, damageType);

        if (!Stats.IsAlive)
        {
            Die();
        }
    }

    public SkillBase GetSkill(int index)
    {
        if (EquippedHelmet == null) return null;

        var skills = EquippedHelmet.ActiveSkills;

        if (skills == null || index < 0 || index >= skills.Count)
            return null;

        return skills[index];
    }

    public List<SkillBase> GetActiveSkills()
    {
        if (IsPlayer && EquippedHelmet != null)
            return EquippedHelmet.ActiveSkills;

        return new List<SkillBase>();
    }

    private void Die()
    {
        _grid.UnregisterCharacter(GridPosition);
        gameObject.SetActive(false);
    }

    public IEnumerator ExecuteAction(IAction action)
    {
        if (action == null)
            yield break;

        if (!action.CanExecute(this))
            yield break;

        yield return action.Execute(this);
    }

    public void EquipHelmet(HelmetData newHelmet)
    {
        if (newHelmet == null) return;

        EquippedHelmet = newHelmet;

        Debug.Log($"{name} equipó {newHelmet.Name}");
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