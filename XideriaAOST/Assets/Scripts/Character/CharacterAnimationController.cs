using System.Collections;
using UnityEngine;

public class CharacterAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private bool attackHitFrameReached;
    private bool attackAnimationFinished;

    private bool castHitFrameReached;
    private bool castAnimationFinished;

    [SerializeField] private float referenceWalkSpeed = 3f;
    [SerializeField] private float minWalkAnimationSpeed = 0.5f;
    [SerializeField] private float maxWalkAnimationSpeed = 2f;

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
        if (animator == null)
            return;

        animator.SetBool("IsWalk", walking);
    }

    public IEnumerator PlayCastUntilHitFrame()
    {
        castHitFrameReached = false;
        castAnimationFinished = false;

        animator.SetTrigger("MagicAttack");

        while (!castHitFrameReached)
            yield return null;
    }

    public IEnumerator WaitCastFinished()
    {
        while (!castAnimationFinished)
            yield return null;
    }

    public void OnCastHitFrame()
    {
        castHitFrameReached = true;
    }

    public void OnCastAnimationFinished()
    {
        castAnimationFinished = true;
    }

    public void PlayHit()
    {
        if (animator == null)
            return;

        animator.SetTrigger("IsHit");
    }

    public void PlayDeath()
    {
        if (animator == null)
            return;

        animator.SetTrigger("Death");
    }
}