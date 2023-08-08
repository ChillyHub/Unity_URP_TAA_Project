using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MotionBlurPass : ScriptableRenderPass
{
    // Shader Textures ID
    private static readonly int BlendedFrameTextureId = Shader.PropertyToID("_BlendedFrameTexture");
    private static readonly int MotionVectorTextureId = Shader.PropertyToID("_MotionVectorTexture");

    private static readonly int DownSampling2TextureId = Shader.PropertyToID("_DownSampling2Texture");
    private static readonly int DownSampling4TextureId = Shader.PropertyToID("_DownSampling4Texture");
    private static readonly int DownSampling8TextureId = Shader.PropertyToID("_DownSampling8Texture");
    
    // Shader Variables ID
    private static readonly int MotionBlurIntensity = Shader.PropertyToID("_MotionBlurIntensity");
    private static readonly int MotionBlurRangeMin = Shader.PropertyToID("_MotionBlurRangeMin");
    private static readonly int MotionBlurRangeMax = Shader.PropertyToID("_MotionBlurRangeMax");
    private static readonly int MotionBlurSampleStep = Shader.PropertyToID("_MotionBlurSampleStep");
    
    // Shader Variables
    private float _motionBlurIntensity;
    private float _motionBlurRangeMin;
    private float _motionBlurRangeMax;
    private float _motionBlurSampleStep;
    
    // Setting
#if UNITY_EDITOR
    private DebugSetting _debugSetting;

    private Material _debugMaterial;
#endif
    
    // Material
    private Material _motionBlurMaterial;
    private MaterialPropertyBlock _materialPropertyBlock;
    
    private ComputeShader _computeShader;
    private bool _useCS;
    
    // History RT Handle
    private RTHandle[] _historyFrameRTs;
    
    // Motion Vector RT Handle
    private RTHandle[] _motionVectorRT;

    // Pass Info
    private ProfilingSampler _profilingSampler = new ProfilingSampler("TAA");


    public MotionBlurPass(Material material, ComputeShader cs = null)
    {
        this.profilingSampler = new ProfilingSampler(nameof(MotionBlurPass));
        
        _motionBlurMaterial = material;
        _materialPropertyBlock = new MaterialPropertyBlock();
        
        _computeShader = cs;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var volume = VolumeManager.instance.stack.GetComponent<TemporalMotionBlur>();
        
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            CameraData cameraData = renderingData.cameraData;

            if (volume.quality == TemporalMotionBlur.Quality.High)
            {
                // TODO: High quality blur (un finish)
                var desc = _historyFrameRTs[1].rt.descriptor;

                desc.width /= 2;
                desc.height /= 2;
                cmd.GetTemporaryRT(DownSampling2TextureId, desc);
                cmd.Blit(_historyFrameRTs[1], DownSampling2TextureId);
                
                desc.width /= 2;
                desc.height /= 2;
                cmd.GetTemporaryRT(DownSampling4TextureId, desc);
                cmd.Blit(DownSampling2TextureId, DownSampling4TextureId);
                
                desc.width /= 2;
                desc.height /= 2;
                cmd.GetTemporaryRT(DownSampling8TextureId, desc);
                cmd.Blit(DownSampling4TextureId, DownSampling8TextureId);
            }
            // else
            {
                _materialPropertyBlock.SetFloat(MotionBlurIntensity, _motionBlurIntensity);
                _materialPropertyBlock.SetFloat(MotionBlurRangeMin, _motionBlurRangeMin);
                _materialPropertyBlock.SetFloat(MotionBlurRangeMax, _motionBlurRangeMax);
                _materialPropertyBlock.SetFloat(MotionBlurSampleStep, _motionBlurSampleStep);

                cmd.SetGlobalTexture(BlendedFrameTextureId, _historyFrameRTs[1]);
                cmd.SetGlobalTexture(MotionVectorTextureId, _motionVectorRT[0]);
                
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                DrawUtils.DrawFullscreenMesh(cmd, _motionBlurMaterial, _materialPropertyBlock);
                cmd.SetViewProjectionMatrices(cameraData.camera.worldToCameraMatrix, cameraData.camera.projectionMatrix);
            }

#if UNITY_EDITOR
            DebugBlit(cmd, cameraData);
#endif
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (cmd == null)
            throw new ArgumentNullException("cmd");
    }

    public void Setup(RTHandle[] historyFrameRTs, RTHandle[] motionVectorRT, RenderPassEvent passEvent,
        ComputeShader computeShader, AdvanceSetting advanceSetting
#if UNITY_EDITOR
        , DebugSetting debugSetting
#endif
        )
    {
        var volume = VolumeManager.instance.stack.GetComponent<TemporalMotionBlur>();
        
        _historyFrameRTs = historyFrameRTs;
        _motionVectorRT = motionVectorRT;

        this.renderPassEvent = passEvent;

        _computeShader = computeShader;
        _useCS = (_computeShader && SystemInfo.supportsComputeShaders) ? true : false;
        
        DrawUtils.SetKeyword(_motionBlurMaterial, "_ENABLE_YCOCG", 
            advanceSetting.toneMappingType == AdvanceSetting.ToneMappingType.YCoCg);
        DrawUtils.SetKeyword(_motionBlurMaterial, "_ENABLE_MOTION_BLUR", volume.IsActive());

        _motionBlurIntensity = volume.intensity.value;
        _motionBlurRangeMin = volume.blurPixelRange.value.x;
        _motionBlurRangeMax = volume.blurPixelRange.value.y;
        _motionBlurSampleStep = volume.sampleStep.value;

#if UNITY_EDITOR
        _debugSetting = debugSetting;

        Shader shader = Shader.Find("Hidden/Custom/TAA/DebugBlit");
        if (shader == null)
        {
            Debug.LogError("Can not find shader: Hidden/Custom/TAA/DebugBlit");
        }

        _debugMaterial = CoreUtils.CreateEngineMaterial(shader);
#endif
    }

#if UNITY_EDITOR
    private void DebugBlit(CommandBuffer cmd, CameraData cameraData)
    {
        if (_debugSetting.enableDebug == false)
        {
            return;
        }
        
        _debugMaterial.SetFloat("_ShowIntensity", _debugSetting.intensity);

        switch (_debugSetting.showRT)
        {
            case DebugSetting.ShowRT.HistoryRT:
                _debugMaterial.SetTexture("_MainTex", _historyFrameRTs[0]);
                cmd.Blit(_historyFrameRTs[0], cameraData.renderer.cameraColorTarget, _debugMaterial);
                break;
            case DebugSetting.ShowRT.MotionVectorRT:
                _debugMaterial.SetTexture("_MainTex", _motionVectorRT[0]);
                cmd.Blit(_motionVectorRT[0], cameraData.renderer.cameraColorTarget, _debugMaterial);
                break;
            default:
                break;
        }
    }
#endif
}