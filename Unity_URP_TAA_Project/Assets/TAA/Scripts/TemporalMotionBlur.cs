using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
[VolumeComponentMenuForRenderPipeline("Post-processing/Temporal Motion Blur", typeof(UniversalRenderPipeline))]
public class TemporalMotionBlur : VolumeComponent, IPostProcessComponent
{
    [Serializable]
    public enum Quality
    {
        Low,
        High
    }

    [Serializable]
    public sealed class QualityParameter : VolumeParameter<Quality>
    {
        public QualityParameter(Quality value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    public BoolParameter enable = new BoolParameter(false);

    public QualityParameter quality = new QualityParameter(Quality.Low, true);

    public ClampedFloatParameter intensity = new ClampedFloatParameter(1.0f, 0.0f, 20.0f);

    public FloatRangeParameter blurPixelRange =
        new FloatRangeParameter(new Vector2(2.0f, 15.0f), 0.0f, 100.0f);

    public ClampedIntParameter sampleStep = new ClampedIntParameter(3, 1, 10);

    public bool IsActive() => enable.value && intensity.value > 0.0f;
    public bool IsTileCompatible() => false;
}