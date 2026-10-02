using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HelmetSlotUI : MonoBehaviour
{
    [Header("Helmet")]
    [SerializeField] private HelmetType helmetType;

    [Header("UI")]
    [SerializeField] private Image helmetIcon;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Button button;

    [Header("Locked")]
    [SerializeField] private Sprite lockedIcon;

    private HelmetInventoryUI inventoryUI;
    private Character playerCharacter;

    public HelmetType HelmetType => helmetType;

    private void Awake()
    {
        inventoryUI = GetComponentInParent<HelmetInventoryUI>();

        if (button == null)
            button = GetComponent<Button>();
    }

    public void Initialize(Character player)
    {
        playerCharacter = player;
    }

    public void Refresh()
    {
        if (HelmetInventoryData.Instance == null)
            return;

        if (HelmetCatalog.Instance == null)
            return;

        bool unlocked =
            HelmetInventoryData.Instance.IsUnlocked(helmetType);

        int amount =
            HelmetInventoryData.Instance.GetAmount(helmetType);

        HelmetCatalogEntry catalogEntry =
            HelmetCatalog.Instance.GetEntry(helmetType);

        if (!unlocked)
        {
            if (helmetIcon != null)
                helmetIcon.sprite = lockedIcon;

            if (amountText != null)
                amountText.text = "";

            if (button != null)
                button.interactable = false;

            return;
        }

        if (helmetIcon != null && catalogEntry != null)
            helmetIcon.sprite = catalogEntry.InventoryIcon;

        if (amountText != null)
        {
            int maxAmount = catalogEntry != null
                ? catalogEntry.MaxStored
                : 0;

            amountText.text = $"{amount}/{maxAmount}";
        }

        if (button != null)
            button.interactable = amount > 0;
    }

    public void SelectHelmet()
    {
        if (playerCharacter == null)
        {
            Debug.LogError(
                $"[HELMET SLOT] No hay Player Character asignado para {helmetType}."
            );

            return;
        }

        bool equipped =
            playerCharacter.EquipHelmetFromInventory(helmetType);

        if (!equipped)
            return;

        Refresh();

        inventoryUI?.OnHelmetEquipped();
    }
}