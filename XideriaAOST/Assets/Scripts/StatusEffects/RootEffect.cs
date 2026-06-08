using UnityEngine;

public class RootEffect : StatusEffect
{
    public override string EffectName => "Root";

    private StatusVFXHandle vfxHandle;

    public RootEffect(int duration)
        : base(duration)
    {
    }

    public override void OnApply(Character target)
    {
        vfxHandle = target.gameObject.AddComponent<StatusVFXHandle>();

        vfxHandle.Attach(
            SkillVFXLibrary.Instance.waterPrison,
            target.GetGroundAnchor(),
            Vector3.zero
        );
    }

    public override void OnExpire(Character target)
    {
        if (vfxHandle != null)
            vfxHandle.Clear();

        if (vfxHandle != null)
            Object.Destroy(vfxHandle);
    }
}