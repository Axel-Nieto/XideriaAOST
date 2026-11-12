using System.Collections;
using UnityEngine;

public class CharacterAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private bool attackHitFrameReached;
    private bool attackAnimationFinished;

    private bool castHitFrameReached;
    private bool castAnimationFinished;

    private static readonly int excavateState = Animator.StringToHash("Base Layer.Ground_Excavar");
    private static readonly int excavateSpeed = Animator.StringToHash("ExcavateSpeed");
    private bool excavating;
    private Vector3 excavateLocalPosition;

    public bool CanPlayExcavate => animator != null && animator.isActiveAndEnabled &&
        animator.runtimeAnimatorController != null && animator.HasState(0, excavateState);

    public IEnumerator PlayExcavateEnter()
    {
        if (!CanPlayExcavate) yield break;
        excavateLocalPosition = animator.transform.localPosition;
        excavating = true;
        animator.ResetTrigger("IsExcavar");
        animator.SetFloat(excavateSpeed, 1f);
        animator.Play(excavateState, 0, 0f);
        animator.Update(0f);
        while (CanPlayExcavate && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == excavateState &&
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f)
            yield return null;

        if (!CanPlayExcavate || animator.GetCurrentAnimatorStateInfo(0).fullPathHash != excavateState) yield break;
        // Clamp to the deepest pose: frame overshoot must not start the exit at the origin.
        animator.SetFloat(excavateSpeed, 0f);
        animator.Play(excavateState, 0, 0.5f);
        animator.Update(0f);
    }

    public IEnumerator PlayExcavateExit()
    {
        if (!excavating || !CanPlayExcavate) yield break;
        animator.SetFloat(excavateSpeed, 1f);
        animator.Play(excavateState, 0, 0.5f);
        animator.Update(0f);
        while (CanPlayExcavate && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == excavateState &&
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;
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

    private void OnDisable() => FinishExcavate();

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