using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pomocné funkce pro skládání modelů z jednoduchých tvarů.
/// </summary>
public static class Shapes
{
    public static GameObject Make(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool shadows = true)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Object.Destroy(g.GetComponent<Collider>()); // kolize řešíme vlastními obdélníky
        if (parent) g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        if (mat) r.sharedMaterial = mat;
        if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    public static GameObject Box(Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool shadows = true)
    {
        return Make(PrimitiveType.Cube, parent, localPos, scale, mat, shadows);
    }

    public static Transform Pivot(Transform parent, Vector3 localPos, string name = "Pivot")
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    public static Transform Root(string name, Vector3 position)
    {
        var t = new GameObject(name).transform;
        t.position = position;
        return t;
    }
}
