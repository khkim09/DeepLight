using UnityEngine;

/// <summary>
/// 빔 머티리얼과 셰이더 파라미터를 런타임에 로그로 확인한다.
/// </summary>
public sealed class BeamMaterialDebugProbe : MonoBehaviour
{
    [SerializeField] private MeshRenderer targetRenderer;

    /// <summary>
    /// 시작 시 머티리얼 상태를 출력한다.
    /// </summary>
    private void Start()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }

        if (targetRenderer == null)
        {
            Debug.LogError($"[BeamDebug] MeshRenderer not found on {name}", this);
            return;
        }

        Material sharedMat = targetRenderer.sharedMaterial;
        Material instMat = targetRenderer.material;

        LogMaterial("sharedMaterial", sharedMat);
        LogMaterial("material", instMat);
    }

    /// <summary>
    /// 머티리얼의 주요 셰이더 값을 출력한다.
    /// </summary>
    private void LogMaterial(string label, Material mat)
    {
        if (mat == null)
        {
            Debug.LogWarning($"[BeamDebug] {label} is null on {name}", this);
            return;
        }

        string shaderName = mat.shader != null ? mat.shader.name : "null";
        int renderQueue = mat.renderQueue;

        Color beamColor = mat.HasProperty("_BeamColor") ? mat.GetColor("_BeamColor") : Color.magenta;
        Vector4 beamParams0 = mat.HasProperty("_BeamParams0") ? mat.GetVector("_BeamParams0") : new Vector4(-999f, -999f, -999f, -999f);
        Vector4 beamParams1 = mat.HasProperty("_BeamParams1") ? mat.GetVector("_BeamParams1") : new Vector4(-999f, -999f, -999f, -999f);
        Vector4 beamOrigin = mat.HasProperty("_BeamOriginWS") ? mat.GetVector("_BeamOriginWS") : Vector4.zero;
        Vector4 beamForward = mat.HasProperty("_BeamForwardWS") ? mat.GetVector("_BeamForwardWS") : Vector4.zero;

        Debug.Log(
            $"[BeamDebug] {label} | go={name} | mat={mat.name} | shader={shaderName} | renderQueue={renderQueue}\n" +
            $"[BeamDebug] BeamColor={beamColor}\n" +
            $"[BeamDebug] _BeamParams0={beamParams0} | _BeamParams1={beamParams1}\n" +
            $"[BeamDebug] _BeamOriginWS={beamOrigin} | _BeamForwardWS={beamForward}",
            this);
    }
}
