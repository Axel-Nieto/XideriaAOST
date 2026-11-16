using UnityEngine;

/// <summary>Keeps an animated model centered without changing its logical character root.</summary>
[DisallowMultipleComponent]
public sealed class CharacterVisualCentering : MonoBehaviour
{
    [SerializeField] private Transform visualModel;
    [SerializeField] private Transform center;
    [SerializeField] private string bodyRendererName = "Body";
    private SkinnedMeshRenderer body;
    private Mesh sampledBody;

    private void LateUpdate() => Recenter();

    public void Recenter()
    {
        if (visualModel == null || center == null) return;
        if (body == null)
        {
            foreach (SkinnedMeshRenderer candidate in visualModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (candidate.name == bodyRendererName) { body = candidate; break; }
            if (body == null) return;
        }
        if (sampledBody == null)
        {
            sampledBody = new Mesh { name = "Character visual center", hideFlags = HideFlags.HideAndDontSave };
            sampledBody.MarkDynamic();
        }
        body.BakeMesh(sampledBody);
        Vector3 bodyCenter = body.transform.TransformPoint(sampledBody.bounds.center);
        Vector3 correction = center.position - bodyCenter;
        correction.y = 0f;
        visualModel.position += correction;
    }

    private void OnDestroy()
    {
        if (sampledBody == null) return;
        if (Application.isPlaying) Destroy(sampledBody);
        else DestroyImmediate(sampledBody);
    }
}
