using UnityEditor;
using UnityEngine;

public static class SampleJitter
{
    public enum SampleType : int
    {
        Default,
        Uniform2,
        Uniform4,
        Uniform4Helix,
        Rotated4,
        Rotated4Helix,
        Halton23X8,
        Halton23X16,
        Halton23X32,
        Halton23X64
    }

    private static readonly float[][] SamplerData = 
    {
        new []  // 0
        {
            0.0f, 0.0f
        },
        new []  // 1
        {
            -0.25f, -0.25f,
             0.25f,  0.25f
        },
        new []  // 2
        {
            -0.25f, -0.25f, //ll
             0.25f, -0.25f, //lr
             0.25f,  0.25f, //ur
            -0.25f,  0.25f, //ul
        },
        new []  // 3
        {
            -0.25f, -0.25f, //ll  3  1
             0.25f,  0.25f, //ur   \/|
             0.25f, -0.25f, //lr   /\|
            -0.25f,  0.25f, //ul  0  2
        },
        new []  // 4
        {
            -0.125f, -0.375f, //ll
             0.375f, -0.125f, //lr
             0.125f,  0.375f, //ur
            -0.375f,  0.125f, //ul
        },
        new []  // 5
        {
            -0.125f, -0.375f, //ll  3  1
             0.125f,  0.375f, //ur   \/|
             0.375f, -0.125f, //lr   /\|
            -0.375f,  0.125f, //ul  0  2
        },
        new float[16],  // 6
        new float[32],  // 7
        new float[64],  // 8
        new float[128]  // 9
    };

    private static int _sampleIndex = 0;
    
    static SampleJitter()
    {
        GetHaltonSequenceUV(SamplerData[6], 2, 3);
        GetHaltonSequenceUV(SamplerData[7], 2, 3);
        GetHaltonSequenceUV(SamplerData[8], 2, 3);
        GetHaltonSequenceUV(SamplerData[9], 2, 3);
    }

    public static Vector2 SampleJitterUV(SampleType sampleType)
    {
        float[] list = SamplerData[(int)sampleType];
        _sampleIndex %= list.Length / 2;
        
        Vector2 res = new Vector2(list[_sampleIndex * 2], list[_sampleIndex * 2 + 1]);
        _sampleIndex++;

        return res;
    }

    private static void GetHaltonSequenceUV(float[] seq, int baseX, int baseY)
    {
        for (int i = 0; i < seq.Length / 2; i++)
        {
            seq[i * 2 + 0] = SampleHaltonSequence(baseX, i) - 0.5f;
            seq[i * 2 + 1] = SampleHaltonSequence(baseY, i) - 0.5f;
        }
    }

    private static float SampleHaltonSequence(int bases, int index)
    {
        index++;        // index start at 1
        float f = 1.0f;
        float r = 0.0f;
        while (index > 0)
        {
            f /= bases;
            r += f * (index % bases);
            index = (int)Mathf.Floor((float)index / bases);
        }

        return r;
    }
}