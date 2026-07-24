using System.Collections;
using UnityEngine;

public class HelmetPickupAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string pickupTriggerName = "IsDisol";
    [SerializeField] private float fallbackDuration = 0.6f;

    private bool animationFinished;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public IEnumerator PlayPickup()
    {
        if (animator == null)
        {
            yield return new WaitForSeconds(fallbackDuration);
            yield break;
        }

        animationFinished = false;

        animator.SetTrigger(pickupTriggerName);

        float timer = 0f;

        while (!animationFinished && timer < fallbackDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }

    public void OnPickupAnimationFinished()
    {
        animationFinished = true;
    }
}
