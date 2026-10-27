using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class HelmetInventoryUI : MonoBehaviour
{
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Character playerCharacter;
    [SerializeField] private BattleHUDThemeController hudThemeController;
    [SerializeField] private BattleHUD battleHUD;

    private GridInputHandler gridInputHandler;
    private HelmetSlotUI[] helmetSlots;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        gridInputHandler = FindFirstObjectByType<GridInputHandler>();

        if (inventoryPanel != null)
        {
            EnsureRockSlot();
            helmetSlots =
                inventoryPanel.GetComponentsInChildren<HelmetSlotUI>(true);

            inventoryPanel.SetActive(false);
        }

        IsOpen = false;
    }

    private void EnsureRockSlot()
    {
        HelmetSlotUI[] existing = inventoryPanel.GetComponentsInChildren<HelmetSlotUI>(true);
        foreach (HelmetSlotUI slot in existing)
            if (slot.HelmetType == HelmetType.Rock) return;
        if (existing.Length == 0) return;
        HelmetSlotUI template = existing[existing.Length - 1];
        GameObject clone = Instantiate(template.gameObject, template.transform.parent, false);
        clone.name = "Helmet_Rock_Slot";
        clone.GetComponent<HelmetSlotUI>().ConfigureType(HelmetType.Rock);
        if (template.transform.parent.GetComponent<LayoutGroup>() == null)
        {
            RectTransform rect = clone.GetComponent<RectTransform>();
            if (rect != null) rect.anchoredPosition += Vector2.right * (rect.rect.width + 20f);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.tabKey.wasPressedThisFrame)
            return;

        Toggle();
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        Open();
    }

    public void Open()
    {
        if (gridInputHandler == null)
            gridInputHandler = FindFirstObjectByType<GridInputHandler>();

        if (gridInputHandler == null)
            return;

        if (!gridInputHandler.CanOpenHelmetInventory())
            return;

        if (!EnsurePlayerCharacter())
            return;

        IsOpen = true;

        RefreshSlots();

        if (inventoryPanel != null)
            inventoryPanel.SetActive(true);

        gridInputHandler.SetHelmetInventoryOpen(true);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        gridInputHandler?.SetHelmetInventoryOpen(false);
    }

    public void ToggleFromUI()
    {
        Toggle();
    }

    private void RefreshSlots()
    {
        if (helmetSlots == null)
            return;

        foreach (HelmetSlotUI slot in helmetSlots)
        {
            if (slot != null)
                slot.Refresh();
        }
    }

    public void OnHelmetEquipped()
    {
        if (playerCharacter != null &&
            hudThemeController != null)
        {
            hudThemeController.RefreshFromHelmet(
                playerCharacter.EquippedHelmet,
                true
            );
        }

        battleHUD?.Refresh();

        Close();
    }

    private bool EnsurePlayerCharacter()
    {
        if (playerCharacter == null)
        {
            Character[] characters =
                FindObjectsByType<Character>(
                    FindObjectsSortMode.None
                );

            foreach (Character character in characters)
            {
                if (character != null && character.IsPlayer)
                {
                    playerCharacter = character;
                    break;
                }
            }
        }

        if (playerCharacter == null)
        {
            Debug.LogError(
                "[HELMET INVENTORY] No se encontró al Player Character."
            );

            return false;
        }

        if (helmetSlots != null)
        {
            foreach (HelmetSlotUI slot in helmetSlots)
            {
                if (slot != null)
                    slot.Initialize(playerCharacter);
            }
        }

        return true;
    }
}