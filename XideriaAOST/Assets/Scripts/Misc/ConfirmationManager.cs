using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmationManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject confirmationPanel;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action pendingAction;

    private void Awake()
    {
        confirmationPanel.SetActive(false);

        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
    }

    public void Show(
        string title,
        string message,
        Action onConfirm)
    {
        if (onConfirm == null)
            return;

        pendingAction = onConfirm;

        titleText.text = title;
        messageText.text = message;

        confirmationPanel.SetActive(true);
    }

    public void Confirm()
    {
        Action action = pendingAction;

        pendingAction = null;

        confirmationPanel.SetActive(false);

        action?.Invoke();
    }

    public void Cancel()
    {
        pendingAction = null;

        confirmationPanel.SetActive(false);
    }

    public bool IsOpen()
    {
        return confirmationPanel.activeSelf;
    }
}
