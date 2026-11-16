using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Částicové efekty vytvářené kódem: jiskry, výbuch, plameny jetpacku.
/// </summary>
public static class Fx
{
    static ParticleSystem Create(string name, Material mat, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        main.playOnAwake = false;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static Gradient Fade(Color from, Color to)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    static void ShrinkOverLife(ParticleSystem ps)
    {
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
    }

    /// <summary>Systém, ze kterého se jiskry vypouštějí ručně přes Emit().</summary>
    public static ParticleSystem Burst(Material mat, string name, float speedMin, float speedMax, float size)
    {
        var ps = Create(name, mat, null, Vector3.zero);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.gravityModifier = 0.6f;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.3f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, Color.white));

        ShrinkOverLife(ps);
        ps.Play();
        return ps;
    }

    public static void Emit(ParticleSystem ps, Vector3 position, Color color, int count)
    {
        var ep = new ParticleSystem.EmitParams
        {
            position = position,
            applyShapeToPosition = true,
            startColor = color
        };
        ps.Emit(ep, count);
    }

    /// <summary>Plamen trysky mířící dolů. Zapíná se přes emission.enabled.</summary>
    public static ParticleSystem Flame(Material mat, Transform parent, Vector3 localPos)
    {
        var ps = Create("Flame", mat, parent, localPos);
        ps.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startColor = new Color(1f, 0.65f, 0.2f);

        var em = ps.emission;
        em.rateOverTime = 80f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 12f;
        sh.radius = 0.05f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Fade(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.15f, 0.05f)));

        ShrinkOverLife(ps);
        ps.Play();
        em.enabled = false;
        return ps;
    }
}
