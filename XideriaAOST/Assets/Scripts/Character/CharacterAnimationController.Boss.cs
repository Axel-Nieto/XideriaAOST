using System.Collections;
using UnityEngine;

public partial class CharacterAnimationController
{
    private bool bossPhaseTwo;
    private bool bossActionBusy;
    private bool bossCenterMovement;
    private int bossActionState;
    public bool BossImpactReached { get; private set; }

    public void PrepareBossPhaseTwo()
    {
        bossPhaseTwo = true;
        bossActionBusy = true;
    }

    public IEnumerator PlayBossTransformation()
    {
        PrepareBossPhaseTwo();
        if (!BeginBossState("Boss_Fase2", "IsF2")) yield break;
        yield return WaitBossActionFinished();
    }

    public IEnumerator PlayBossAttackUntilImpact(bool ranged, System.Action startEffect = null)
    {
        BossImpactReached = false;
        if (!BeginBossState(ranged ? "Boss_SkillF2" : "Boss_AttackF2", ranged ? "IsSkillF2" : "IsAttackF2"))
        {
            startEffect?.Invoke();
            BossImpactReached = true;
            yield break;
        }
        // Both F2 attack clips are one second. Start the persistent ranged rock
        // at 35% so its rise precedes the impact at 55%.
        while (IsBossAction(out AnimatorStateInfo state) && state.normalizedTime < (ranged ? 0.35f : 0.55f))
            yield return null;
        if (!IsBossAction(out _)) yield break;
        startEffect?.Invoke();
        while (IsBossAction(out AnimatorStateInfo state) && state.normalizedTime < 0.55f)
            yield return null;
        BossImpactReached = IsBossAction(out _);
    }

    public IEnumerator WaitBossActionFinished()
    {
        while (IsBossAction(out AnimatorStateInfo state) && state.normalizedTime < 1f)
            yield return null;
    }

    public void FinishBossAction()
    {
        bool ownState = IsBossAction(out _);
        bossActionState = 0;
        bossActionBusy = bossCenterMovement;
        if (ownState) PlayBossIdle();
    }

    public void SetBossCenterMovement(bool allowed)
    {
        bossCenterMovement = allowed;
        bossActionBusy = allowed;
        if (!allowed) PlayBossIdle();
    }

    private bool BeginBossState(string state, string trigger)
    {
        bossActionBusy = true;
        int hash = Animator.StringToHash("Base Layer." + state);
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || !animator.HasState(0, hash))
            return false;
        animator.SetBool("IsWalk", false);
        foreach (string pending in new[] { "Physical Attack", "MagicAttack", "IsHit", "IsF2", "IsAttackF2", "IsSkillF2", "IsHitF2", "IsWalkf2" })
            animator.ResetTrigger(pending);
        // Enter from any interrupted pose instead of waiting for the old Idle.
        animator.SetTrigger(trigger);
        animator.Play(hash, 0, 0f);
        animator.Update(0f);
        animator.ResetTrigger(trigger);
        bossActionState = hash;
        return true;
    }

    private bool IsBossAction(out AnimatorStateInfo state)
    {
        state = default;
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || bossActionState == 0)
            return false;
        state = animator.GetCurrentAnimatorStateInfo(0);
        return state.fullPathHash == bossActionState;
    }

    private void PlayBossIdle()
    {
        if (!bossPhaseTwo || animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Death")) return;
        int idle = Animator.StringToHash("Base Layer.Boss_IdleF2");
        if (!animator.HasState(0, idle)) return;
        animator.Play(idle, 0, 0f);
        animator.Update(0f);
    }

    private bool TryPlayBossPhaseTwoHit()
    {
        if (!bossPhaseTwo) return false;
        if (!bossActionBusy && animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            animator.SetTrigger("IsHitF2");
        return true;
    }

    private bool TrySetBossPhaseTwoWalking(bool walking)
    {
        if (!bossPhaseTwo) return false;
        if (bossCenterMovement && animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
        {
            if (walking)
            {
                int walk = Animator.StringToHash("Base Layer.Boss_Walkf2");
                if (animator.HasState(0, walk)) { animator.Play(walk, 0, 0f); animator.Update(0f); }
            }
            else PlayBossIdle();
        }
        return true;
    }
}
