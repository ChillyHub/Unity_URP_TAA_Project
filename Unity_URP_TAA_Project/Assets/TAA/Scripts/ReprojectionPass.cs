using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ReprojectionPass : ScriptableRenderPass
{
    // Shader Textures ID
    private static readonly int MotionVectorTextureId = Shader.PropertyToID("_MotionVectorTexture");
    private static readonly int HistoryFrameTextureId = Shader.PropertyToID("_HistoryFrameTexture");
    private static readonly int CurrentFrameTextureId = Shader.PropertyToID("_CurrentFrameTexture");
    private static readonly int ReprojectionTargetTextureId = Shader.PropertyToID("_ReprojectionTargetTexture");
    
    // Shader Variables ID
    private static readonly int FixedBlendWeightId = Shader.PropertyToID("_FixedBlendWeight");
    private static readonly int RangeWeightMinId = Shader.PropertyToID("_RangeWeightMin");
    private static readonly int RangeWeightMaxId = Shader.PropertyToID("_RangeWeightMax");
    
    private static readonly int ReprojectionTargetWidthId = Shader.PropertyToID("_ReprojectionTargetWidth");
    private static readonly int ReprojectionTargetHeightId = Shader.PropertyToID("_ReprojectionTargetHeight");
    
    // Compute Shader Kernel Name
    private const string ReprojectionCSName = "ReprojectionCSMain";
    
    // Shader Variables
    private float _fixedBlendWeight;
    private float _rangeWeightMin;
    private float _rangeWeightMax;

    // Setting
    private AdvanceSetting _advanceSetting;

#if UNITY_EDITOR
    private DebugSetting _debugSetting;
#endif
    
    // Material
    private Material _reprojectionMaterial;
    private MaterialPropertyBlock _materialPropertyBlock;
    
    private ComputeShader _computeShader;
    private bool _useCS;
    
    // TODO: Motion Vector Texture
    private RTHandle[] _motionVectorRT;
    
    // TODO: History RT Buffer
    private RTHandle[] _historyFrameRTs;
    private RenderTextureDescriptor _historyFrameRTDesc;

    // Pass Info
    private ProfilingSampler _profilingSampler = new ProfilingSampler("TAA");
    private bool _init = false;

    public ReprojectionPass(Material material, ComputeShader cs = null)
    {
        this.profilingSampler = new ProfilingSampler(nameof(ReprojectionPass));
        
        _reprojectionMaterial = material;
        _materialPropertyBlock = new MaterialPropertyBlock();
        
        _computeShader = cs;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // TODO: Check RT and change descriptor, configure target
        _historyFrameRTDesc = cameraTextureDescriptor;
        _historyFrameRTDesc.depthBufferBits = 0;
        _historyFrameRTDesc.enableRandomWrite = _useCS;
        
        if (_init == false)
        {
            DrawUtils.RTHandleReAllocateIfNeeded(ref _historyFrameRTs[0], _historyFrameRTDesc, 
                wrapMode: TextureWrapMode.Clamp, 
                name: TemporalAARendererFeature.historyFrameTextureNames[0]);
            _init = true;
        }
        DrawUtils.RTHandleReAllocateIfNeeded(ref _historyFrameRTs[1], _historyFrameRTDesc, 
            wrapMode: TextureWrapMode.Clamp, 
            name: TemporalAARendererFeature.historyFrameTextureNames[1]);
        ConfigureTarget(_historyFrameRTs[1]);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            CameraData cameraData = renderingData.cameraData;

            if (_useCS)
            {
                float width = _historyFrameRTDesc.width;
                float height = _historyFrameRTDesc.height;
                
                cmd.SetComputeFloatParam(_computeShader,FixedBlendWeightId, _fixedBlendWeight);
                cmd.SetComputeFloatParam(_computeShader, RangeWeightMinId, _rangeWeightMin);
                cmd.SetComputeFloatParam(_computeShader, RangeWeightMaxId, _rangeWeightMax);
                
                cmd.SetComputeFloatParam(_computeShader, ReprojectionTargetWidthId, width);
                cmd.SetComputeFloatParam(_computeShader, ReprojectionTargetHeightId, height);

                int kernel = _computeShader.FindKernel(ReprojectionCSName);
                cmd.SetComputeTextureParam(_computeShader, kernel, MotionVectorTextureId, _motionVectorRT[0]);
                cmd.SetComputeTextureParam(_computeShader, kernel, HistoryFrameTextureId, _historyFrameRTs[0]);
                cmd.SetComputeTextureParam(_computeShader, kernel, CurrentFrameTextureId, renderingData.cameraData.renderer.cameraColorTarget);
                cmd.SetComputeTextureParam(_computeShader, kernel, ReprojectionTargetTextureId, _historyFrameRTs[1]);
                
                DrawUtils.Dispatch(cmd, _computeShader, kernel, width, height);
            }
            else
            {
                _materialPropertyBlock.SetFloat(FixedBlendWeightId, _fixedBlendWeight);
                _materialPropertyBlock.SetFloat(RangeWeightMinId, _rangeWeightMin);
                _materialPropertyBlock.SetFloat(RangeWeightMaxId, _rangeWeightMax);

                // TODO: Set History and Motion vector textures
                cmd.SetGlobalTexture(MotionVectorTextureId, _motionVectorRT[0]);
                cmd.SetGlobalTexture(HistoryFrameTextureId, _historyFrameRTs[0]);
                cmd.SetGlobalTexture(CurrentFrameTextureId, renderingData.cameraData.renderer.cameraColorTarget);

                // TODO: Draw Call
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                DrawUtils.DrawFullscreenMesh(cmd, _reprojectionMaterial, _materialPropertyBlock);
                cmd.SetViewProjectionMatrices(cameraData.camera.worldToCameraMatrix, cameraData.camera.projectionMatrix);
            }
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (cmd == null)
            throw new ArgumentNullException("cmd");
    }

    public void Setup(RTHandle[] historyFrameRTs, string[] historyFrameNames, RTHandle[] motionVectorRT, 
        RenderPassEvent passEvent, ComputeShader computeShader, AdvanceSetting advanceSetting
#if UNITY_EDITOR
        , DebugSetting debugSetting
#endif
        )
    {
        CoreUtils.Swap(ref historyFrameRTs[0], ref historyFrameRTs[1]);
        CoreUtils.Swap(ref historyFrameNames[0], ref historyFrameNames[1]);

        _historyFrameRTs = historyFrameRTs;
        _motionVectorRT = motionVectorRT;

        this.renderPassEvent = passEvent;
        
        _computeShader = computeShader;
        _useCS = (_computeShader && SystemInfo.supportsComputeShaders) ? true : false;

        _advanceSetting = advanceSetting;
        
        // SetKeyword
        SetKeyword();

        _fixedBlendWeight = advanceSetting.fixedBlendWeight;
        _rangeWeightMin = advanceSetting.rangeWeightMin;
        _rangeWeightMax = advanceSetting.rangeWeightMax;

#if UNITY_EDITOR
        _debugSetting = debugSetting;
#endif
    }

    private void SetKeyword()
    {
        if (_useCS)
        {
            DrawUtils.SetKeyword(_computeShader, "_ENABLE_YCOCG", 
                _advanceSetting.toneMappingType == AdvanceSetting.ToneMappingType.YCoCg);
            DrawUtils.SetKeyword(_computeShader, "_ENABLE_SAMPLE_CLOSEST_MOTION_VECTOR", 
                _advanceSetting.enableClosest);
            DrawUtils.SetKeyword(_computeShader, "_BLEND_FIXED", 
                _advanceSetting.blendWeightType == AdvanceSetting.BlendWeightType.Fixed);
            DrawUtils.SetKeyword(_computeShader, "_BLEND_MOTION", 
                _advanceSetting.blendWeightType == AdvanceSetting.BlendWeightType.RangeByMotionVector);
            DrawUtils.SetKeyword(_computeShader, "_BLEND_LUMINANCE", 
                _advanceSetting.blendWeightType == AdvanceSetting.BlendWeightType.RangeByLuminance);
            DrawUtils.SetKeyword(_computeShader, "_BLEND_MOTION_LUMINANCE", 
                _advanceSetting.blendWeightType == AdvanceSetting.BlendWeightType.RangeByBoth);
        }
        else
        {
            DrawUtils.SetKeyword(_reprojectionMaterial, "_ENABLE_YCOCG", 
                _advanceSetting.toneMappingType == AdvanceSetting.ToneMappingType.YCoCg);
            DrawUtils.SetKeyword(_reprojectionMaterial, "_ENABLE_SAMPLE_CLOSEST_MOTION_VECTOR", 
                _advanceSetting.enableClosest);
            DrawUtils.SetKeyword(_reprojectionMaterial, "_BLEND_FIXED", 
                _advanceSetting.blendWeightType == AdvanceSetting.BlendWeightType.Fixed);
            DrawUtils.SetKeyword(_reprojectionMaterial, "_BLEND_MOTION", 
                _advanceSetting.blendWeightType == AdvanceSetting.BlendWeightType.RangeByMotionVector);
            DrawUtils.SetKeyword(_reprojectionMaterial, "_BLEND_LUMINANCE", 
                _advanceSetting.blendWeightType == AdvanceSetting.BlendWeightType.RangeByLuminance);
            DrawUtils.SetKeyword(_reprojectionMaterial, "_BLEND_MOTION_LUMINANCE", 
                _advanceSetting.blendWeightType == AdvanceSetting.BlendWeightType.RangeByBoth);
        }
    }
}