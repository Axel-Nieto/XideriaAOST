using UnityEngine;

public class CharacterPositionIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer indicatorRenderer;

    [Header("Materials")]
    [SerializeField] private Material playerMaterial;
    [SerializeField] private Material enemyMaterial;
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private Material turnMaterial;

    private Character character;
    private bool isSelected;
    private bool isTurnActive;

    private void Awake()
    {
        character = GetComponentInParent<Character>();

        if (indicatorRenderer == null)
            indicatorRenderer = GetComponentInChildren<Renderer>();
    }

    private void Start()
    {
        Refresh();
    }

    public void SetSelected(bool value)
    {
        isSelected = value;
        Refresh();
    }

    public void SetTurnActive(bool value)
    {
        isTurnActive = value;
        Refresh();
    }

    public void Refresh()
    {
        if (indicatorRenderer == null || character == null)
            return;

        if (isSelected)
        {
            indicatorRenderer.material = selectedMaterial;
            return;
        }

        if (isTurnActive)
        {
            indicatorRenderer.material = turnMaterial;
            return;
        }

        indicatorRenderer.material = character.IsPlayer
            ? playerMaterial
            : enemyMaterial;
    }
}
