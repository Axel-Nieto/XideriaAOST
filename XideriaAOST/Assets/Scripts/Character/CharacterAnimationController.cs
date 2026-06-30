using System.Collections;
using UnityEngine;

public class CharacterAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private bool attackHitFrameReached;
    private bool attackAnimationFinished;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public IEnumerator PlayAttackUntilHitFrame()
    {
        attackHitFrameReached = false;
        attackAnimationFinished = false;

        animator.SetTrigger("Physical Attack");

        while (!attackHitFrameReached)
            yield return null;
    }

    public IEnumerator WaitAttackFinished()
    {
        while (!attackAnimationFinished)
            yield return null;
    }

    public void OnAttackHitFrame()
    {
        attackHitFrameReached = true;
    }

    public void OnAttackAnimationFinished()
    {
        attackAnimationFinished = true;
    }

    public void SetWalking(bool walking)
    {
        animator.SetBool("IsWalk", walking);
    }
}