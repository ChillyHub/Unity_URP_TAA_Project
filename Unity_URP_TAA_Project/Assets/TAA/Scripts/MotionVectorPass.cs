using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MotionVectorPass : ScriptableRenderPass
{
    private enum MotionVectorPassType : int
    {
        DynamicObjects = 0,
        StaticObjects = 1
    }
    
    // Compute Shader Textures ID
    private static readonly int MotionVectorTargetTextureId = Shader.PropertyToID("_MotionVectorTargetTexture");

    // Shader Variables ID
    private static readonly int PrevMatrixVPId = Shader.PropertyToID("_PrevMatrixVP");
    private static readonly int PrevMatrixInvVPId = Shader.PropertyToID("_PrevMatrixInvVP");
    private static readonly int CurrMatrixVPId = Shader.PropertyToID("_CurrMatrixVP");
    private static readonly int CurrMatrixInvVPId = Shader.PropertyToID("_CurrMatrixInvVP");
    private static readonly int JitterUVId = Shader.PropertyToID("_JitterUV");
    
    private static readonly int MotionVectorTargetWidthId = Shader.PropertyToID("_MotionVectorTargetWidth");
    private static readonly int MotionVectorTargetHeightId = Shader.PropertyToID("_MotionVectorTargetHeight");
    
    // Compute Shader Kernel Name
    private const string MotionVectorCSName = "MotionVectorCSMain";
    
    // Shader Variables
    private Matrix4x4 _prevMatrixVP;
    private Matrix4x4 _prevMatrixInvVP;
    private Matrix4x4 _currMatrixVP;
    private Matrix4x4 _currMatrixInvVP;
    
    // Setting
    private AdvanceSetting _advanceSetting;

#if UNITY_EDITOR
    private DebugSetting _debugSetting;
#endif
    
    // State
    private Matrix4x4 _nonJitterProjection;
    private Vector2 _jitterUV = Vector2.zero;
    private Vector2 _nextJitterUV = Vector2.zero;
    
    // Material
    private Material _motionVectorMaterial;
    private MaterialPropertyBlock _materialPropertyBlock;

    private const string DynamicMotionPassTagName = "MotionVector";
    private FilteringSettings _filteringSettings;

    private ComputeShader _computeShader;
    private bool _useCS;
    
    // TODO: Motion Vector RT
    private RTHandle[] _motionVectorRT;
    private RenderTextureDescriptor _motionVectorTextureDesc;

    // Pass Info
    private ProfilingSampler _profilingSampler = new ProfilingSampler("TAA");

    public MotionVectorPass(Material material, ComputeShader cs = null)
    {
        this.profilingSampler = new ProfilingSampler(nameof(MotionVectorPass));
        
        _motionVectorMaterial = material;
        _materialPropertyBlock = new MaterialPropertyBlock();

        _computeShader = cs;
    }
    
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.camera.cameraType == CameraType.Preview)
        {
            return;
        }
        
        // Reset projection
        renderingData.cameraData.camera.ResetProjectionMatrix();
        
        // Store origin projection
        Matrix4x4 projMatrix = renderingData.cameraData.camera.projectionMatrix;
        float width = renderingData.cameraData.camera.scaledPixelWidth;
        float height = renderingData.cameraData.camera.scaledPixelHeight;
        _nonJitterProjection = projMatrix;
        
        // Update jitter UV
        _jitterUV = _nextJitterUV;

        // Next frame jitter projection
        _nextJitterUV = SampleJitter.SampleJitterUV(_advanceSetting.sampleType);
        projMatrix.m02 = _nextJitterUV.x * 2.0f / width;
        projMatrix.m12 = _nextJitterUV.y * 2.0f / height;

        renderingData.cameraData.camera.projectionMatrix = projMatrix;
    }
    
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        _motionVectorTextureDesc = cameraTextureDescriptor;
        _motionVectorTextureDesc.colorFormat = RenderTextureFormat.RGHalf;
        _motionVectorTextureDesc.depthBufferBits = 0;
        _motionVectorTextureDesc.enableRandomWrite = _useCS;
        
        DrawUtils.RTHandleReAllocateIfNeeded(ref _motionVectorRT[0], _motionVectorTextureDesc, 
            name: TemporalAARendererFeature.motionVectorTextureName);
        
        // ConfigureTarget(_motionVectorRT[0]);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.camera.cameraType == CameraType.Preview)
        {
            return;
        }
        
        DrawingSettings drawingSettings = CreateDrawingSettings(
            new ShaderTagId("UniversalForward"), 
            ref renderingData, 
            renderingData.cameraData.defaultOpaqueSortFlags);
        drawingSettings.overrideMaterial = _motionVectorMaterial;
        drawingSettings.overrideMaterialPassIndex = (int)MotionVectorPassType.DynamicObjects;
        drawingSettings.perObjectData = PerObjectData.MotionVectors;
        
        // Set motion vector
        var textureMode = renderingData.cameraData.camera.depthTextureMode;
        DepthTextureMode depthTextureMode = textureMode;
        textureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;
        renderingData.cameraData.camera.depthTextureMode = textureMode;

        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            CameraData cameraData = renderingData.cameraData;

            // Calculate and Submit Prev and Curr Transform Matrix
            Matrix4x4 nonJitterGPUProjection = GL.GetGPUProjectionMatrix(
                _nonJitterProjection, cameraData.IsCameraProjectionMatrixFlipped());
            _currMatrixVP = nonJitterGPUProjection * cameraData.GetViewMatrix();
            _currMatrixInvVP = _currMatrixVP.inverse;
            
            _motionVectorMaterial.SetMatrix(PrevMatrixVPId, _prevMatrixVP);
            _motionVectorMaterial.SetMatrix(PrevMatrixInvVPId, _prevMatrixInvVP);
            
            _motionVectorMaterial.SetMatrix(CurrMatrixVPId, _currMatrixVP);
            _motionVectorMaterial.SetMatrix(CurrMatrixInvVPId, _currMatrixInvVP);
            
            _motionVectorMaterial.SetVector(JitterUVId, _jitterUV);

            if (_useCS)
            {
                // Static Pass Draw Call
                float width = _motionVectorTextureDesc.width;
                float height = _motionVectorTextureDesc.height;
                
                cmd.SetComputeMatrixParam(_computeShader, PrevMatrixVPId, _prevMatrixVP);
                cmd.SetComputeMatrixParam(_computeShader, PrevMatrixInvVPId, _prevMatrixInvVP);
                cmd.SetComputeMatrixParam(_computeShader, CurrMatrixVPId, _currMatrixVP);
                cmd.SetComputeMatrixParam(_computeShader, CurrMatrixInvVPId, _currMatrixInvVP);
                cmd.SetComputeVectorParam(_computeShader, JitterUVId, _jitterUV);

                cmd.SetComputeFloatParam(_computeShader, MotionVectorTargetWidthId, width);
                cmd.SetComputeFloatParam(_computeShader, MotionVectorTargetHeightId, height);

                int kernel = _computeShader.FindKernel(MotionVectorCSName);
                cmd.SetComputeTextureParam(_computeShader, kernel, 
                    MotionVectorTargetTextureId, _motionVectorRT[0]);

                DrawUtils.Dispatch(cmd, _computeShader, kernel, width, height);
                
                // Dynamic objects pass
                // Must set RT at this, because cs dispatch will change RT
                cmd.SetRenderTarget(_motionVectorRT[0], 
                    renderingData.cameraData.renderer.cameraDepthTarget);
                
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings);
            }
            else
            {
                // Set RT, depth target use origin camera target for ZTest and Stencil Test
                cmd.SetRenderTarget(_motionVectorRT[0], 
                    renderingData.cameraData.renderer.cameraDepthTarget);
                
                // Dynamic objects pass
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings);
                
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                DrawUtils.DrawFullscreenMesh(cmd, _motionVectorMaterial, _materialPropertyBlock, 
                    (int)MotionVectorPassType.StaticObjects);
                cmd.SetViewProjectionMatrices(cameraData.camera.worldToCameraMatrix, 
                    cameraData.camera.projectionMatrix);
            }

            // Reset RT
            cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTarget, 
                renderingData.cameraData.renderer.cameraDepthTarget);

            // Update Matrix VP
            _prevMatrixVP = _currMatrixVP;
            _prevMatrixInvVP = _currMatrixInvVP;
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (cmd == null)
            throw new ArgumentNullException("cmd");
    }

    public void Setup(RTHandle[] motionVectorRT, RenderPassEvent passEvent, 
        ComputeShader computeShader, AdvanceSetting advanceSetting
#if UNITY_EDITOR
        , DebugSetting debugSetting
#endif
        )
    {
        _motionVectorRT = motionVectorRT;
        
        this.renderPassEvent = passEvent;
        
        _computeShader = computeShader;
        _useCS = (_computeShader && SystemInfo.supportsComputeShaders) ? true : false;

        _advanceSetting = advanceSetting;

        _filteringSettings = new FilteringSettings(RenderQueueRange.all, advanceSetting.dynamicFilter);

#if UNITY_EDITOR
        _debugSetting = debugSetting;
#endif
    }
}