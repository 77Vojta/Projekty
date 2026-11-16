using UnityEngine;

/// <summary>
/// Jednoduchý bloom (záře kolem svítících ploch) pro Built-in Render Pipeline.
/// Dělá z neonových barev opravdový neon.
/// </summary>
[RequireComponent(typeof(Camera))]
public class NeonBloom : MonoBehaviour
{
    public Shader shader;
    [Range(0f, 3f)] public float intensity = 0.45f;
    [Range(0f, 3f)] public float threshold = 1.1f;
    [Range(0f, 1f)] public float softKnee = 0.5f;
    [Range(1, 8)] public int iterations = 6;

    Material mat;
    readonly RenderTexture[] textures = new RenderTexture[16];

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (shader == null || !shader.isSupported)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (mat == null) mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

        float knee = threshold * softKnee;
        mat.SetVector("_Filter", new Vector4(threshold, threshold - knee, 2f * knee, 0.25f / (knee + 0.00001f)));
        mat.SetFloat("_Intensity", intensity);

        int width = source.width / 2;
        int height = source.height / 2;
        RenderTextureFormat format = source.format;

        RenderTexture current = textures[0] = RenderTexture.GetTemporary(width, height, 0, format);
        Graphics.Blit(source, current, mat, 0); // výběr jasných míst
        RenderTexture currentSource = current;

        int i = 1;
        for (; i < iterations; i++)
        {
            width /= 2;
            height /= 2;
            if (height < 2) break;
            current = textures[i] = RenderTexture.GetTemporary(width, height, 0, format);
            Graphics.Blit(currentSource, current, mat, 1); // zmenšování
            currentSource = current;
        }

        for (i -= 2; i >= 0; i--)
        {
            current = textures[i];
            textures[i] = null;
            Graphics.Blit(currentSource, current, mat, 2); // zvětšování a sčítání
            RenderTexture.ReleaseTemporary(currentSource);
            currentSource = current;
        }

        mat.SetTexture("_SourceTex", source);
        Graphics.Blit(currentSource, destination, mat, 3); // složení s obrazem
        RenderTexture.ReleaseTemporary(currentSource);
    }

    void OnDestroy()
    {
        if (mat) Destroy(mat);
    }
}
