using System.Collections;
using UnityEngine;

public partial class CharacterAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private bool attackHitFrameReached;
    private bool attackAnimationFinished;

    private bool castHitFrameReached;
    private bool castAnimationFinished;

    private static readonly int twirlState = Animator.StringToHash("Base Layer.Twirl_Tete");
    private static readonly int twirlTrigger = Animator.StringToHash("IsTwirl");
    // Twirl_Tete is 61 frames at 60 FPS. Prepare the slash at frame 15;
    // its graph emits 0.15 seconds later, together with the impact at frame 24.
    private const float twirlSlashFrame = 15f / 61f;
    private const float twirlHitFrame = 24f / 61f;
    private bool twirling;
    public bool TwirlHitFrameReached { get; private set; }
    public bool CanPlayTwirl => animator != null && animator.isActiveAndEnabled &&
        animator.runtimeAnimatorController != null && animator.HasState(0, twirlState);

    public IEnumerator PlayTwirlUntilHitFrame(System.Action<float> playSlash)
    {
        TwirlHitFrameReached = false;
        if (!CanPlayTwirl)
        {
            playSlash?.Invoke(1f);
            TwirlHitFrameReached = true;
            yield break;
        }

        twirling = true;
        animator.SetBool("IsWalk", false);
        animator.ResetTrigger(twirlTrigger);
        animator.SetTrigger(twirlTrigger);
        animator.Update(0f);

        // The trigger may need to wait for the previous walk/hit to return to Idle.
        float entryTimeout = 2f;
        while (CanPlayTwirl && !TryGetTwirlState(out _) && entryTimeout > 0f)
        {
            entryTimeout -= Time.deltaTime;
            yield return null;
        }
        while (TryGetTwirlState(out AnimatorStateInfo state) && state.normalizedTime < twirlSlashFrame)
            yield return null;
        if (!TryGetTwirlState(out AnimatorStateInfo slashState)) yield break;
        playSlash?.Invoke(Mathf.Max(0.01f, animator.speed * slashState.speed * slashState.speedMultiplier));
        while (TryGetTwirlState(out AnimatorStateInfo state) && state.normalizedTime < twirlHitFrame)
            yield return null;
        TwirlHitFrameReached = TryGetTwirlState(out _);
    }

    public IEnumerator WaitTwirlFinished()
    {
        while (TryGetTwirlState(out _))
            yield return null;
    }

    private bool TryGetTwirlState(out AnimatorStateInfo state)
    {
        state = default;
        if (!twirling || !CanPlayTwirl) return false;
        state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash == twirlState) return true;
        if (!animator.IsInTransition(0)) return false;
        state = animator.GetNextAnimatorStateInfo(0);
        return state.fullPathHash == twirlState;
    }

    public void FinishTwirl()
    {
        if (!twirling) return;
        bool inTwirl = TryGetTwirlState(out _);
        twirling = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.ResetTrigger(twirlTrigger);
        if (inTwirl)
        {
            animator.Play("Base Layer.metarig|IDL", 0, 0f);
            animator.Update(0f);
        }
    }


    private static readonly int excavateState = Animator.StringToHash("Base Layer.Ground_Excavar");
    private static readonly int excavateSpeed = Animator.StringToHash("ExcavateSpeed");
    private static readonly int chargeState = Animator.StringToHash("Base Layer.Charge");
    private static readonly int chargeSpeed = Animator.StringToHash("ChargeSpeed");
    private bool charging;
    private bool excavating;
    private Vector3 excavateLocalPosition;

    public bool CanPlayExcavate => animator != null && animator.isActiveAndEnabled &&
        animator.runtimeAnimatorController != null && animator.HasState(0, excavateState);

    public IEnumerator PlayExcavateEnter(float duration = 0f)
    {
        if (!CanPlayExcavate) yield break;
        excavateLocalPosition = animator.transform.localPosition;
        excavating = true;
        animator.ResetTrigger("IsExcavar");
        animator.SetFloat(excavateSpeed, 1f);
        animator.Play(excavateState, 0, 0f);
        animator.Update(0f);
        MatchExcavateDuration(duration);
        while (CanPlayExcavate && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == excavateState &&
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f)
            yield return null;

        if (!CanPlayExcavate || animator.GetCurrentAnimatorStateInfo(0).fullPathHash != excavateState) yield break;
        // Clamp to the deepest pose: frame overshoot must not start the exit at the origin.
        animator.SetFloat(excavateSpeed, 0f);
        animator.Play(excavateState, 0, 0.5f);
        animator.Update(0f);
    }

    public IEnumerator PlayExcavateExit(float duration = 0f)
    {
        if (!excavating || !CanPlayExcavate) yield break;
        animator.SetFloat(excavateSpeed, 1f);
        animator.Play(excavateState, 0, 0.5f);
        animator.Update(0f);
        MatchExcavateDuration(duration);
        while (CanPlayExcavate && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == excavateState &&
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;
    }

    private void MatchExcavateDuration(float duration)
    {
        if (duration <= 0f) return;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        // State length already includes the Animator and state playback speeds.
        if (state.length > 0f)
            animator.SetFloat(excavateSpeed, state.length * 0.5f / duration);
    }

    public void FinishExcavate()
    {
        if (!excavating || animator == null) return;
        excavating = false;
        if (animator.runtimeAnimatorController != null)
        {
            animator.SetFloat(excavateSpeed, 1f);
            if (animator.isActiveAndEnabled && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == excavateState)
            {
                animator.Play("Base Layer.metarig|IDL", 0, 0f);
                animator.Update(0f);
            }
        }
        // The clip animates the model root, independently of Character.GridPosition.
        animator.transform.localPosition = excavateLocalPosition;
    }

    private void OnDisable()
    {
        FinishTwirl();
        FinishExcavate();
        FinishCharge();
    }

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

        Debug.Log("CAST: inicio");


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
        if (TrySetBossPhaseTwoWalking(walking)) return;
        if (animator == null || charging)
            return;

        animator.SetBool("IsWalk", walking);
    }

    public void BeginCharge(float duration)
    {
        if (animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null || !animator.HasState(0, chargeState))
            return;
        charging = true;
        animator.SetBool("IsWalk", false);
        animator.SetFloat(chargeSpeed, 1f);
        animator.Play(chargeState, 0, 0f);
        animator.Update(0f);
        float length = animator.GetCurrentAnimatorStateInfo(0).length;
        if (length > 0f && duration > 0f)
            animator.SetFloat(chargeSpeed, length / duration);
    }

    public void FinishCharge()
    {
        if (!charging) return;
        charging = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetFloat(chargeSpeed, 1f);
        if (animator.isActiveAndEnabled && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == chargeState)
        {
            animator.Play("Base Layer.Idle", 0, 0f);
            animator.Update(0f);
        }
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
        Debug.Log("CAST: esperando fin de animación");

        while (!castAnimationFinished)
            yield return null;

        Debug.Log("CAST: animación terminada");
    }

    public void OnCastHitFrame()
    {
        Debug.Log("CAST EVENT: OnCastHitFrame");

        castHitFrameReached = true;
    }

    public void OnCastAnimationFinished()
    {
        Debug.Log("CAST EVENT: OnCastAnimationFinished");

        castAnimationFinished = true;
    }

    public void PlayHit()
    {
        if (TryPlayBossPhaseTwoHit()) return;
        if (animator == null)
            return;

        animator.SetTrigger("IsHit");
    }

    public void PlayDeath()
    {
        if (TryGetDeathState(animator, out int layer, out int stateHash))
            animator.Play(stateHash, layer, 0f);
    }

    public static IEnumerator PlayDeathAndWait(Animator deathAnimator, System.Action onCompleted)
    {
        if (!TryGetDeathState(deathAnimator, out int layer, out int stateHash))
        {
            if (deathAnimator != null)
                Debug.LogWarning("El enemigo no tiene un estado de muerte compatible; se completara su retirada logica.", deathAnimator);

            onCompleted?.Invoke();
            yield break;
        }

        // Enter the terminal state from any pose, without relying on Idle-only transitions.
        if (deathAnimator.isActiveAndEnabled)
            deathAnimator.Play(stateHash, layer, 0f);

        // Let the Animator evaluate the requested state before inspecting its progress.
        yield return null;

        bool enteredDeathState = false;
        while (deathAnimator != null && deathAnimator.isActiveAndEnabled)
        {
            if (deathAnimator.runtimeAnimatorController == null || layer >= deathAnimator.layerCount ||
                !deathAnimator.HasState(layer, stateHash))
            {
                Debug.LogWarning("El controlador cambio durante la muerte; no se puede confirmar su final.", deathAnimator);
                yield break;
            }

            AnimatorStateInfo state = deathAnimator.GetCurrentAnimatorStateInfo(layer);
            if (state.fullPathHash != stateHash)
            {
                if (enteredDeathState)
                {
                    Debug.LogWarning("La animacion de muerte fue interrumpida; no se otorgara energia anticipadamente.", deathAnimator);
                    yield break;
                }

                yield return null;
                continue;
            }

            enteredDeathState = true;
            if (state.loop)
            {
                Debug.LogWarning("La animacion de muerte esta configurada en bucle; no se puede confirmar su final.", deathAnimator);
                yield break;
            }

            if (state.normalizedTime >= 1f && !deathAnimator.IsInTransition(layer))
            {
                onCompleted?.Invoke();
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning("El Animator se desactivo antes de completar la muerte; no se otorgara energia anticipadamente.", deathAnimator);
    }

    private static bool TryGetDeathState(Animator deathAnimator, out int layer, out int stateHash)
    {
        layer = 0;
        stateHash = 0;
        if (deathAnimator == null || deathAnimator.runtimeAnimatorController == null)
            return false;

        for (int index = 0; index < deathAnimator.layerCount; index++)
        {
            string prefix = deathAnimator.GetLayerName(index) + ".";
            int candidate = Animator.StringToHash(prefix + "Death");
            if (!deathAnimator.HasState(index, candidate))
            {
                candidate = Animator.StringToHash(prefix + "Dead_Larva");
                if (!deathAnimator.HasState(index, candidate))
                    continue;
            }

            layer = index;
            stateHash = candidate;
            return true;
        }

        return false;
    }
}