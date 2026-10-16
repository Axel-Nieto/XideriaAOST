using System;
using System.Collections;
using UnityEngine;

/// <summary>Lobby wiring only. A completed route, never an intermediate step, may open the shop.</summary>
public sealed class UpgradeShopInteraction : MonoBehaviour
{
    [SerializeField] private Character playerCharacter;
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private GridInputHandler inputHandler;
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private UpgradeShopUI shop;

    private CharacterMovement movement;
    private Coroutine pendingOpen;
    private bool ownsInputLock;

    private void OnEnable()
    {
        Unsubscribe();
        // One component lookup handles Awake ordering; there are no per-frame searches.
        movement = playerCharacter != null ? playerCharacter.GetComponent<CharacterMovement>() : null;
        if (movement != null)
            movement.MovementCompleted += HandleMovementCompleted;
        if (shop != null)
        {
            shop.OpenStateChanged += HandleOpenStateChanged;
            if (shop.IsOpen)
                HandleOpenStateChanged(true);
        }
    }

    private void OnDisable()
    {
        CancelPendingOpen();
        Unsubscribe();
        if (ownsInputLock && shop != null)
            shop.Close();
        ReleaseInputLock();
    }

    private void OnDestroy() => OnDisable();

    private void HandleMovementCompleted(Character actor)
    {
        if (actor != playerCharacter)
            return;

        CancelPendingOpen();
        if (IsShopDestination(actor) && shop != null && !shop.IsOpen)
            pendingOpen = StartCoroutine(OpenAfterMovement(actor, actor.GridPosition));
    }

    private IEnumerator OpenAfterMovement(Character actor, Vector2Int completedOrigin)
    {
        // CharacterMovement completes before the input action's finally block.
        // Wait for that action (including any pickup) and discard stale destinations.
        yield return null;
        while (CanInteract(actor, completedOrigin) && inputHandler.IsExecutingAction)
            yield return null;

        pendingOpen = null;
        if (CanInteract(actor, completedOrigin) && inputHandler.CanOpenUpgradeShop(actor))
            shop.Open();
    }

    private bool IsShopDestination(Character actor)
    {
        if (actor == null || actor != playerCharacter || !actor.IsPlayer || gridSystem == null ||
            actor.gameObject.scene != gameObject.scene || gridSystem.gameObject.scene != gameObject.scene)
            return false;

        foreach (Vector2Int cell in actor.GetOccupiedCells())
            if (gridSystem.GetCell(cell)?.Type == CellType.Shop)
                return true;
        return false;
    }

    private bool CanInteract(Character actor, Vector2Int origin) =>
        isActiveAndEnabled && string.Equals(gameObject.scene.name, "Lobby", StringComparison.OrdinalIgnoreCase) &&
        IsShopDestination(actor) && actor.GridPosition == origin && actor.Stats != null && actor.Stats.IsAlive &&
        !actor.IsDying && actor.gameObject.activeInHierarchy && !GameSession.IsGameOver &&
        inputHandler != null && inputHandler.isActiveAndEnabled && gameStateManager != null &&
        gameStateManager.IsExplorationMode && shop != null && shop.isActiveAndEnabled &&
        shop.gameObject.scene == gameObject.scene &&
        (SceneTransitionManager.Instance == null || !SceneTransitionManager.Instance.IsTransitioning);

    private void HandleOpenStateChanged(bool open)
    {
        if (!open)
        {
            ReleaseInputLock();
            return;
        }

        if (!CanInteract(playerCharacter, playerCharacter != null ? playerCharacter.GridPosition : default) ||
            (!ownsInputLock && !inputHandler.CanOpenUpgradeShop(playerCharacter)))
        {
            shop?.Close();
            return;
        }
        inputHandler.SetUpgradeShopOpen(true);
        ownsInputLock = true;
    }

    private void ReleaseInputLock()
    {
        if (ownsInputLock && inputHandler != null)
            inputHandler.SetUpgradeShopOpen(false);
        ownsInputLock = false;
    }

    private void CancelPendingOpen()
    {
        if (pendingOpen != null)
            StopCoroutine(pendingOpen);
        pendingOpen = null;
    }

    private void Unsubscribe()
    {
        if (movement != null)
            movement.MovementCompleted -= HandleMovementCompleted;
        if (shop != null)
            shop.OpenStateChanged -= HandleOpenStateChanged;
        movement = null;
    }
}
