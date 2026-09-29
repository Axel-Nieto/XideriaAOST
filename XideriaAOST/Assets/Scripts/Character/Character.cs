using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Character : MonoBehaviour
{
    [SerializeField] private Vector2Int startGridPosition;
    [SerializeField] private float modelForwardYOffset = 180f;
    [SerializeField] private CharacterStatsData baseStats;
    public HelmetData EquippedHelmet { get; private set; }
    public int HelmetCharges { get; private set; }
    public CharacterStats Stats { get; private set; }
    public CharacterMovement Movement { get; private set; }

    public Vector2Int GridPosition { get; private set; }
    public bool IsTurnFinished { get; private set; }
    private GridSystem _grid;
    [SerializeField] private Vector3 visualSpawnOffset = new Vector3(0f, 0.5f, 0f);
    public Vector3 VisualOffset => visualSpawnOffset;
    [SerializeField] private bool isPlayer;
    [SerializeField] private List<SkillBase> characterSkills = new();
    private Dictionary<string, int> skillCooldowns = new();
    private List<StatusEffect> statusEffects = new();
    private List<PassiveEffect> passiveEffects = new();
    private List<PassiveEffect> basePassives = new();
    private List<PassiveEffect> helmetPassives = new();

    [SerializeField] private Transform helmetSocket;
    private GameObject currentHelmetVisual;

    public bool IsPlayer => isPlayer;
    private bool isDying;
    public bool IsDying => isDying;

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

    private void Start()
    {
        if (IsPlayer)
        {
            LoadPlayerSession();
        }
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
                     + visualSpawnOffset;

        _grid.RegisterCharacter(startGridPosition, this);
    }

    private void HandleTurnStarted(Character character)
    {
        if (character != this)
            return;

        if (isDying || !Stats.IsAlive)
            return;

        if (IsPlayer)
        {
            ProcessHelmetCharges();
        }

        StartTurn();
        ReduceCooldowns();
        ProcessStatusEffects();
    }


    public void SetGridPosition(Vector2Int newPosition)
    {
        GridPosition = newPosition;
    }

    public void FaceCell(Vector2Int targetCell)
    {
        Vector2Int direction = targetCell - GridPosition;

        if (direction == Vector2Int.zero)
            return;

        FaceDirection(direction);
    }

    public void FaceDirection(Vector2Int direction)
    {
        Vector3 worldDirection = new Vector3(direction.x, 0f, direction.y);

        if (worldDirection == Vector3.zero)
            return;

        transform.rotation =
            Quaternion.LookRotation(worldDirection) *
            Quaternion.Euler(0f, modelForwardYOffset, 0f);
    }

    public void TakeDamage(int amount, DamageType damageType)
    {
        if (isDying || !Stats.IsAlive)
            return;

        foreach (var passive in GetPassiveEffects())
        {
            amount = passive.ModifyIncomingDamage(
                this,
                amount,
                damageType
            );
        }

        var floatingText = GetComponent<FloatingDamageSpawner>();

        if (amount <= 0)
        {
            floatingText?.ShowMiss();
            return;
        }

        GetComponentInChildren<CharacterAnimationController>()?.PlayHit();

        Stats.TakeDamage(amount);

        floatingText?.ShowDamage(amount, damageType);

        if (!Stats.IsAlive)
        {
            Die();
        }
    }

    public void TakeDamage(int amount, DamageType damageType, Character attacker)
    {
        if (attacker != null)
            FaceCell(attacker.GridPosition);

        TakeDamage(amount, damageType);
    }

    public int GetMovementCostPerCell()
    {
        if (HasStatusEffect<SlowEffect>())
            return 2;

        return 1;
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
            return;

        Stats.Heal(amount);

        var floatingText = GetComponent<FloatingDamageSpawner>();

        floatingText?.ShowHeal(amount);
    }

    public void AddSkill(SkillBase skill)
    {
        if (skill == null)
            return;

        characterSkills.Add(skill);
    }

    public List<SkillBase> GetActiveSkills()
    {
        if (IsPlayer && EquippedHelmet != null)
            return EquippedHelmet.ActiveSkills;

        return characterSkills;
    }

    public SkillBase GetSkill(int index)
    {
        var skills = GetActiveSkills();

        if (skills == null || index < 0 || index >= skills.Count)
            return null;

        return skills[index];
    }

    public List<StatusEffect> GetStatusEffects()
    {
        return statusEffects;
    }

    private void Die()
    {
        if (isDying)
            return;

        isDying = true;

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        if (!IsPlayer &&
            GetComponentInChildren<CharacterAnimationController>())
        {
            GetComponentInChildren<CharacterAnimationController>()?.PlayDeath();

            yield return new WaitForSeconds(3f);
        }

        if (!isPlayer)
            GetComponent<EnemyLootDropper>()?.TryDrop(this);

        _grid.UnregisterCharacter(GridPosition);

        gameObject.SetActive(false);
    }

    public void AddPassive(PassiveEffect passive)
    {
        if (passive == null)
            return;

        passiveEffects.Add(passive);
    }

    public List<PassiveEffect> GetPassiveEffects()
    {
        List<PassiveEffect> all = new();

        all.AddRange(basePassives);
        all.AddRange(helmetPassives);

        return all;
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

        if (currentHelmetVisual != null)
            Destroy(currentHelmetVisual);

        helmetPassives.Clear();

        if (newHelmet.PassiveEffects != null)
        {
            helmetPassives.AddRange(newHelmet.PassiveEffects);
        }

        if (newHelmet.HelmetVisualPrefab != null && helmetSocket != null)
        {
            currentHelmetVisual = Instantiate(
                newHelmet.HelmetVisualPrefab,
                helmetSocket
            );

            currentHelmetVisual.transform.localPosition = Vector3.zero;
            currentHelmetVisual.transform.localRotation = Quaternion.identity;
            currentHelmetVisual.transform.localScale = Vector3.one;

            Animator[] animators = currentHelmetVisual.GetComponentsInChildren<Animator>();

            foreach (var animator in animators)
            {
                animator.enabled = false;
            }
        }
        GetComponent<CharacterColorByHelmet>()?.ApplyHelmetColor(newHelmet);
    }

    public void StartTurn()
    {
        IsTurnFinished = false;

        Stats.RegenEnergy();
    }

    public void FinishTurn()
    {
        IsTurnFinished = true;
    }

    public void ResetTurn()
    {
        IsTurnFinished = false;
    }

    public bool IsSkillOnCooldown(SkillBase skill)
    {
        if (skill == null)
            return false;

        return skillCooldowns.TryGetValue(skill.Name, out int value) && value > 0;
    }

    public int GetSkillCooldown(SkillBase skill)
    {
        if (skill == null)
            return 0;

        if (skillCooldowns.TryGetValue(skill.Name, out int value))
            return value;

        return 0;
    }

    public void StartCooldown(SkillBase skill)
    {
        if (skill == null)
            return;

        if (skill.Cooldown <= 0)
            return;

        skillCooldowns[skill.Name] = skill.Cooldown;
    }

    private void ReduceCooldowns()
    {
        List<string> keys = new(skillCooldowns.Keys);

        foreach (var key in keys)
        {
            skillCooldowns[key]--;

            if (skillCooldowns[key] <= 0)
                skillCooldowns.Remove(key);
        }
    }

    public void ApplyStatusEffect(StatusEffect effect)
    {
        if (effect == null)
            return;

        if (effect is SlowEffect)
        {
            foreach (var currentEffect in statusEffects)
            {
                if (currentEffect is SlowEffect)
                    return;
            }
        }

        statusEffects.Add(effect);

        effect.OnApply(this);
    }

    private void ProcessStatusEffects()
    {
        for (int i = statusEffects.Count - 1; i >= 0; i--)
        {
            if (isDying || !Stats.IsAlive)
                break;

            var effect = statusEffects[i];

            effect.OnTurnStart(this);

            if (isDying || !Stats.IsAlive)
                break;

            if (effect.IsFinished)
            {
                effect.OnExpire(this);
                statusEffects.RemoveAt(i);
            }
        }
    }

    public bool HasStatusEffect<T>() where T : StatusEffect
    {
        foreach (var effect in statusEffects)
        {
            if (effect is T)
                return true;
        }

        return false;
    }

    public void ProcessCurrentTerrain()
    {
        GridCell cell = _grid.GetCell(GridPosition);

        if (cell == null)
            return;

        if (cell.Type == CellType.Void)
        {
            Debug.Log($"{name} cayó al vacío.");
            Die();
        }
    }

    public Transform GetGroundAnchor()
    {
        CharacterPositionIndicator indicator =
            GetComponentInChildren<CharacterPositionIndicator>();

        if (indicator != null)
            return indicator.transform;

        return transform;
    }

    public void SetSkillCooldown(SkillBase skill, int cooldown)
    {
        if (skill == null)
            return;

        skillCooldowns[skill.Name] = cooldown;
    }

    public void LoadPlayerSession()
    {
        if (!IsPlayer || PlayerSessionData.Instance == null)
            return;

        var session = PlayerSessionData.Instance;

        if (session.forceNewRun)
        {
            session.forceNewRun = false;

            UnequipHelmet();

            return;
        }

        if (!session.hasSavedData)
        {
            UnequipHelmet();
            return;
        }

        Stats.SetCurrentHP(session.currentHP);
        Stats.SetCurrentEnergy(session.currentEnergy);

        if (session.hasHelmet)
        {
            if (HelmetCatalog.Instance == null)
            {
                Debug.LogError(
                    "[CHARACTER] No existe HelmetCatalog.Instance."
                );

                UnequipHelmet();
                return;
            }

            HelmetData helmet =
                HelmetCatalog.Instance.CreateHelmet(
                    session.equippedHelmetType
                );

            EquipHelmet(helmet);

            HelmetCharges = session.equippedHelmetCharges;
        }
        else
        {
            UnequipHelmet();
        }

        foreach (var skill in GetActiveSkills())
        {
            if (session.skillCooldowns.TryGetValue(
                skill.Name,
                out int cooldown))
            {
                SetSkillCooldown(skill, cooldown);
            }
        }
    }

    public void SavePlayerSession()
    {
        if (!IsPlayer || PlayerSessionData.Instance == null)
        {
            return;
        }

        var session = PlayerSessionData.Instance;

        session.hasSavedData = true;
        session.currentHP = Stats.CurrentHP;
        session.currentEnergy = Stats.CurrentEnergy;

        session.hasHelmet =
            EquippedHelmet != null &&
            EquippedHelmet.Type != HelmetType.Default;

        session.equippedHelmetType =
            session.hasHelmet
                ? EquippedHelmet.Type
                : HelmetType.Default;

        session.equippedHelmetCharges =
            session.hasHelmet
                ? HelmetCharges
                : 0;

        session.skillCooldowns.Clear();

        foreach (var skill in GetActiveSkills())
        {
            session.skillCooldowns[skill.Name] = GetSkillCooldown(skill);
        }
    }


    public void UnequipHelmet()
    {
        EquippedHelmet = HelmetFactory.CreateDefaultHelmet();

        HelmetCharges = 0;

        if (currentHelmetVisual != null)
        {
            Destroy(currentHelmetVisual);
            currentHelmetVisual = null;
        }

        helmetPassives.Clear();

        GetComponent<CharacterColorByHelmet>()?.ApplyHelmetColor(null);
    }

    public bool EquipHelmetFromInventory(HelmetType type)
    {
        if (!IsPlayer)
            return false;

        if (type == HelmetType.Default)
            return false;

        if (HelmetInventoryData.Instance == null)
        {
            Debug.LogError(
                "[HELMET] HelmetInventoryData.Instance es NULL."
            );

            return false;
        }

        if (HelmetCatalog.Instance == null)
        {
            Debug.LogError(
                "[HELMET] HelmetCatalog.Instance es NULL."
            );

            return false;
        }

        HelmetCatalogEntry catalogEntry =
            HelmetCatalog.Instance.GetEntry(type);

        if (catalogEntry == null)
        {
            Debug.LogError(
                $"[HELMET] {type} no existe en HelmetCatalog."
            );

            return false;
        }

        if (!HelmetInventoryData.Instance.ConsumeHelmet(type))
        {
            Debug.Log(
                $"[HELMET] No hay unidades disponibles de {type}."
            );

            return false;
        }

        HelmetData helmet =
            HelmetCatalog.Instance.CreateHelmet(type);

        EquipHelmet(helmet);

        HelmetCharges = catalogEntry.MaxCharges;

        Debug.Log(
            $"[HELMET] Equipado {helmet.Name} con " +
            $"{HelmetCharges} cargas."
        );

        return true;
    }

    private void ProcessHelmetCharges()
    {
        if (EquippedHelmet == null ||
            EquippedHelmet.Type == HelmetType.Default)
        {
            return;
        }

        if (HelmetCharges <= 0)
        {
            Debug.Log(
                $"[HELMET] {EquippedHelmet.Name} se quedó sin cargas."
            );

            UnequipHelmet();
            return;
        }

        HelmetCharges--;

        Debug.Log(
            $"[HELMET] {EquippedHelmet.Name}: " +
            $"{HelmetCharges} cargas restantes."
        );
    }
}