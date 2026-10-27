using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the current session balances; it never owns or awards currency.</summary>
public sealed class CurrencyHUD : MonoBehaviour
{
    [Header("Xideria Fragments")]
    [SerializeField] private Image xideriaFragmentsIcon;
    [SerializeField] private TMP_Text xideriaFragmentsText;

    [Header("Gold")]
    [SerializeField] private Image goldIcon;
    [SerializeField] private TMP_Text goldText;

    private PlayerSessionData session;

    private void OnEnable()
    {
        ConfigureIcon(xideriaFragmentsIcon);
        ConfigureIcon(goldIcon);

        // Also works when this HUD is enabled before the session's Awake.
        PlayerSessionData.InstanceChanged -= HandleSessionChanged;
        PlayerSessionData.InstanceChanged += HandleSessionChanged;
        BindSession(PlayerSessionData.Instance);
    }

    private void OnDisable() => Unsubscribe();

    private void OnDestroy() => Unsubscribe();

    private void HandleSessionChanged(PlayerSessionData newSession) => BindSession(newSession);

    private void BindSession(PlayerSessionData newSession)
    {
        UnbindSession();
        session = newSession;
        if (session != null)
            session.BalanceChanged += HandleBalanceChanged;

        RefreshBalances();
    }

    private void HandleBalanceChanged(RunCurrency currency, int newBalance)
    {
        // Read the authoritative balances, including both zeros after AbandonRun.
        RefreshBalances();
    }

    private void RefreshBalances()
    {
        if (xideriaFragmentsText != null)
            xideriaFragmentsText.text = (session != null ? session.XideriaFragments : 0)
                .ToString(CultureInfo.InvariantCulture);

        if (goldText != null)
            goldText.text = (session != null ? session.Gold : 0)
                .ToString(CultureInfo.InvariantCulture);
    }

    private void Unsubscribe()
    {
        PlayerSessionData.InstanceChanged -= HandleSessionChanged;
        UnbindSession();
    }

    private void UnbindSession()
    {
        if (session != null)
            session.BalanceChanged -= HandleBalanceChanged;
        session = null;
    }

    private static void ConfigureIcon(Image icon)
    {
        if (icon == null)
            return;

        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }
}
