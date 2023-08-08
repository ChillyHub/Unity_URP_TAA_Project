using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class AdvanceSetting
{
    public SampleJitter.SampleType sampleType = SampleJitter.SampleType.Default;

    public bool enableClosest = true;

    // Tone Mapping
    public enum ToneMappingType
    {
        None,
        YCoCg
    }
    [Space][Header("Tone Mapping Setting")]
    public ToneMappingType toneMappingType = ToneMappingType.None;

    // Blend Weight
    public enum BlendWeightType
    {
        Fixed,
        RangeByMotionVector,
        RangeByLuminance,
        RangeByBoth
    }
    [Space][Header("Blend Weight Setting")]
    public BlendWeightType blendWeightType = BlendWeightType.Fixed;

    [Range(0.0f, 1.0f)]
    public float fixedBlendWeight = 0.05f;

    [Range(0.0f, 0.5f)]
    public float rangeWeightMin = 0.05f;
    [Range(0.0f, 1.0f)]
    public float rangeWeightMax = 0.1f;
        
    // Filter
    [Space][Header("Dynamic Filter Setting")]
    public LayerMask dynamicFilter = 0;
}

[Serializable]
public class DebugSetting
{
    public bool enableDebug = false;

    [Range(0.0f, 100.0f)]
    public float intensity = 1.0f;
    
    public enum ShowRT
    {
        ResultRT,
        HistoryRT,
        MotionVectorRT
    }

    public ShowRT showRT = ShowRT.ResultRT;
}

public class TemporalAARendererFeature : ScriptableRendererFeature
{
    // Shader Textures Name
    [NonSerialized]
    public static string motionVectorTextureName = "_MotionVectorTexture";
    
    [NonSerialized]
    public static string[] historyFrameTextureNames =
    {
        "_HistoryFrameTextureA",
        "_HistoryFrameTextureB"
    };
    
    // TODO: TAA Renderer feature setting
    public ComputeShader computeShader;
    
    public AdvanceSetting advanceSetting;
    
#if UNITY_EDITOR
    public DebugSetting debugSetting;
#endif
    
    // TODO: Motion Vector RT buffer
    private readonly RTHandle[] _motionVectorRT = new RTHandle[1]; 
    
    // TODO: History frame RT buffer
    private readonly RTHandle[] _historyFrameRTs = new RTHandle[2];
    
    // TODO: TAA Passes
    private MotionVectorPass _motionVectorPass;
    private ReprojectionPass _reprojectionPass;
    private MotionBlurPass _motionBlurPass;

    public override void Create()
    {
        const string motionVectorShaderName = "Hidden/Custom/TAA/MotionVector";
        const string reprojectionShaderName = "Hidden/Custom/TAA/Reprojection";
        const string motionBlurShaderName = "Hidden/Custom/TAA/MotionBlur";
        
        Shader motionVectorShader = Shader.Find(motionVectorShaderName);
        Shader reprojectionShader = Shader.Find(reprojectionShaderName);
        Shader motionBlurShader = Shader.Find(motionBlurShaderName);

        if (motionVectorShader == null)
        {
            Debug.LogError($"Can not find shader: {motionVectorShaderName}");
        }

        if (reprojectionShader == null)
        {
            Debug.LogError($"Can not find shader: {reprojectionShaderName}");
        }
        
        if (motionBlurShader == null)
        {
            Debug.LogError($"Can not find shader: {motionBlurShaderName}");
        }

        Material motionVectorMaterial = CoreUtils.CreateEngineMaterial(motionVectorShader);
        Material reprojectionMaterial = CoreUtils.CreateEngineMaterial(reprojectionShader);
        Material motionBlurMaterial = CoreUtils.CreateEngineMaterial(motionBlurShader);

        _motionVectorPass = new MotionVectorPass(motionVectorMaterial);
        _reprojectionPass = new ReprojectionPass(reprojectionMaterial);
        _motionBlurPass = new MotionBlurPass(motionBlurMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _motionVectorPass.Setup(_motionVectorRT, 
            RenderPassEvent.BeforeRenderingPostProcessing, computeShader, advanceSetting
#if UNITY_EDITOR
            , debugSetting
#endif
            );
        renderer.EnqueuePass(_motionVectorPass);
        
        _reprojectionPass.Setup(_historyFrameRTs, historyFrameTextureNames, _motionVectorRT, 
            RenderPassEvent.BeforeRenderingPostProcessing, computeShader, advanceSetting
#if UNITY_EDITOR
            , debugSetting
#endif
        );
        renderer.EnqueuePass(_reprojectionPass);
        
        _motionBlurPass.Setup(_historyFrameRTs, _motionVectorRT, 
            RenderPassEvent.BeforeRenderingPostProcessing, computeShader, advanceSetting
#if UNITY_EDITOR
            , debugSetting
#endif
        );
        renderer.EnqueuePass(_motionBlurPass);
    }

    private void Reset()
    {
        ReleaseRT();
    }

    private void OnDisable()
    {
        ReleaseRT();
    }

    private void OnDestroy()
    {
        ReleaseRT();
    }

    private void ReleaseRT()
    {
        _motionVectorRT[0]?.Release();
        _historyFrameRTs[0]?.Release();
        _historyFrameRTs[1]?.Release();
    }
}