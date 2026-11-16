using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Hráč: běh vpřed, tři pruhy, skok, podklouznutí a let s jetpackem.
/// Postavička je složená z tvarů a má animované ruce a nohy.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public const float FlyHeight = 6f;

    const float JumpVelocity = 10f;
    const float Gravity = 30f;
    const float SlideDuration = 0.75f;
    const float LaneSwitchSpeed = 14f;
    const float GraceTime = 1.2f;   // nesmrtelnost po přistání z jetpacku

    static readonly Vector3 StandSize = new Vector3(0.9f, 1.8f, 0.9f);
    static readonly Vector3 SlideSize = new Vector3(0.9f, 0.8f, 0.9f);

    float laneWidth;
    Transform visual, legL, legR, armL, armR;
    ParticleSystem flameL, flameR;
    TrailRenderer trail;
    MeshRenderer[] meshRenderers;

    int lane = 1;            // 0 = vlevo, 1 = střed, 2 = vpravo
    float vy;
    float slideTimer;
    float jetpackLeft;
    float grace;
    float phase;
    bool grounded = true;
    bool slideOnLanding;
    bool crashed;

    public bool Sliding => slideTimer > 0f;
    public bool Flying => jetpackLeft > 0f;
    public float JetpackLeft => jetpackLeft;
    public bool Invulnerable => Flying || grace > 0f;
    public Vector3 Center => transform.position + new Vector3(0f, 0.9f, 0f);

    public static PlayerController Create(RunnerGame game)
    {
        var root = new GameObject("Player");
        var pc = root.AddComponent<PlayerController>();
        pc.Build(game);
        return pc;
    }

    void Build(RunnerGame g)
    {
        laneWidth = g.laneWidth;
        visual = Shapes.Pivot(transform, Vector3.zero, "Visual");

        // trup, opasek, helma, hledí
        Shapes.Box(visual, new Vector3(0f, 1.08f, 0f), new Vector3(0.62f, 0.72f, 0.36f), g.suitMat);
        Shapes.Box(visual, new Vector3(0f, 0.76f, 0f), new Vector3(0.64f, 0.08f, 0.38f), g.visorMat, false);
        Shapes.Box(visual, new Vector3(0f, 1.3f, 0.185f), new Vector3(0.4f, 0.06f, 0.02f), g.visorMat, false);
        Shapes.Make(PrimitiveType.Sphere, visual, new Vector3(0f, 1.68f, 0f), Vector3.one * 0.5f, g.helmetMat);
        Shapes.Box(visual, new Vector3(0f, 1.7f, 0.2f), new Vector3(0.36f, 0.13f, 0.12f), g.visorMat, false);

        // batoh s tryskami (jetpack)
        Shapes.Box(visual, new Vector3(0f, 1.1f, -0.3f), new Vector3(0.48f, 0.56f, 0.24f), g.packMat);
        Shapes.Box(visual, new Vector3(0f, 1.1f, -0.425f), new Vector3(0.3f, 0.08f, 0.02f), g.visorMat, false);
        Shapes.Make(PrimitiveType.Cylinder, visual, new Vector3(-0.14f, 0.78f, -0.32f), new Vector3(0.15f, 0.08f, 0.15f), g.packMat);
        Shapes.Make(PrimitiveType.Cylinder, visual, new Vector3(0.14f, 0.78f, -0.32f), new Vector3(0.15f, 0.08f, 0.15f), g.packMat);

        // nohy a ruce na kloubech
        legL = Limb(visual, new Vector3(-0.16f, 0.72f, 0f), g, true);
        legR = Limb(visual, new Vector3(0.16f, 0.72f, 0f), g, true);
        armL = Limb(visual, new Vector3(-0.41f, 1.38f, 0f), g, false);
        armR = Limb(visual, new Vector3(0.41f, 1.38f, 0f), g, false);

        meshRenderers = visual.GetComponentsInChildren<MeshRenderer>();

        // plameny trysek
        flameL = Fx.Flame(g.particleMat, visual, new Vector3(-0.14f, 0.68f, -0.32f));
        flameR = Fx.Flame(g.particleMat, visual, new Vector3(0.14f, 0.68f, -0.32f));

        // neonová stopa za hráčem
        var trailT = Shapes.Pivot(transform, new Vector3(0f, 0.95f, -0.25f), "Trail");
        trail = trailT.gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = g.particleMat;
        trail.time = 0.18f;
        trail.minVertexDistance = 0.3f;
        trail.widthMultiplier = 0.55f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.3f, 1f, 0.95f), 0f), new GradientColorKey(new Color(0.9f, 0.2f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;
    }

    static Transform Limb(Transform parent, Vector3 joint, RunnerGame g, bool leg)
    {
        Transform pivot = Shapes.Pivot(parent, joint, leg ? "Leg" : "Arm");
        if (leg)
        {
            Shapes.Box(pivot, new Vector3(0f, -0.34f, 0f), new Vector3(0.22f, 0.68f, 0.24f), g.suitMat);
            Shapes.Box(pivot, new Vector3(0f, -0.38f, 0.125f), new Vector3(0.12f, 0.08f, 0.02f), g.visorMat, false); // svítící pruh na koleni
            Shapes.Box(pivot, new Vector3(0f, -0.68f, 0.06f), new Vector3(0.25f, 0.1f, 0.36f), g.packMat);          // bota
        }
        else
        {
            Shapes.Box(pivot, new Vector3(0f, -0.28f, 0f), new Vector3(0.16f, 0.56f, 0.18f), g.suitMat);
            Shapes.Make(PrimitiveType.Sphere, pivot, new Vector3(0f, -0.6f, 0f), Vector3.one * 0.17f, g.helmetMat);
        }
        return pivot;
    }

    public Bounds GetBounds()
    {
        Vector3 size = Sliding ? SlideSize : StandSize;
        return new Bounds(transform.position + new Vector3(0f, size.y * 0.5f, 0f), size);
    }

    public void StartJetpack(float duration)
    {
        jetpackLeft = duration;
        slideTimer = 0f;
        slideOnLanding = false;
    }

    public void Tick(float dt, float speed)
    {
        if (crashed) return;

        if (Keys.Left) lane = Mathf.Max(0, lane - 1);
        if (Keys.Right) lane = Mathf.Min(2, lane + 1);
        bool jump = Keys.Jump;
        bool slide = Keys.Slide;

        Vector3 p = transform.position;

        if (Flying)
        {
            jetpackLeft -= dt;
            p.y = Mathf.MoveTowards(p.y, FlyHeight, 12f * dt);
            vy = 0f;
            grounded = false;
            if (jetpackLeft <= 0f)
            {
                jetpackLeft = 0f;
                grace = GraceTime; // začne padat, po dopadu chvíli nesmrtelný
            }
        }
        else
        {
            if (jump && grounded)
            {
                vy = JumpVelocity;
                grounded = false;
                slideTimer = 0f;
            }

            if (slide)
            {
                if (grounded) slideTimer = SlideDuration;
                else
                {
                    vy = Mathf.Min(vy, -JumpVelocity * 1.5f); // ve vzduchu: rychlý pád a podklouznutí po dopadu
                    slideOnLanding = true;
                }
            }

            if (!grounded)
            {
                vy -= Gravity * dt;
                p.y += vy * dt;
                if (p.y <= 0f)
                {
                    p.y = 0f;
                    vy = 0f;
                    grounded = true;
                    if (slideOnLanding)
                    {
                        slideTimer = SlideDuration;
                        slideOnLanding = false;
                    }
                }
            }
        }

        float targetX = (lane - 1) * laneWidth;
        p.x = Mathf.MoveTowards(p.x, targetX, LaneSwitchSpeed * dt);
        p.z += speed * dt;
        transform.position = p;

        if (slideTimer > 0f) slideTimer -= dt;
        if (grace > 0f && grounded) grace -= dt;

        Animate(dt, speed, targetX - p.x);
    }

    void Animate(float dt, float speed, float laneOffset)
    {
        float roll = Mathf.Clamp(laneOffset * -8f, -20f, 20f);
        float pitch = 0f;
        float bob = 0f;
        float legA, legB, armA, armB;

        if (Flying)
        {
            pitch = 12f;
            legA = 8f; legB = 14f;
            armA = 20f; armB = 20f;
        }
        else if (!grounded)
        {
            legA = -40f; legB = 25f;
            armA = -70f; armB = 40f;
        }
        else if (Sliding)
        {
            pitch = -68f;
            legA = -15f; legB = -10f;
            armA = -40f; armB = -40f;
        }
        else
        {
            phase += dt * speed * 0.55f;
            float s = Mathf.Sin(phase);
            legA = s * 45f; legB = -s * 45f;
            armA = -s * 40f; armB = s * 40f;
            bob = Mathf.Abs(Mathf.Cos(phase)) * 0.08f;
        }

        float k = 1f - Mathf.Exp(-20f * dt);
        legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(legA, 0f, 0f), k);
        legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(legB, 0f, 0f), k);
        armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(armA, 0f, 6f), k);
        armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(armB, 0f, -6f), k);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(pitch, 0f, roll), k);
        visual.localPosition = new Vector3(0f, bob, 0f);

        var emL = flameL.emission; emL.enabled = Flying;
        var emR = flameR.emission; emR.enabled = Flying;

        // blikání během ochranné doby po přistání
        bool visible = grace <= 0f || Mathf.FloorToInt(Time.time * 14f) % 2 == 0;
        foreach (var r in meshRenderers) r.enabled = visible;
    }

    public void Crash()
    {
        crashed = true;
        visual.localRotation = Quaternion.Euler(-80f, 0f, 0f);
        visual.localPosition = new Vector3(0f, 0.25f, -0.4f);
        foreach (var r in meshRenderers) r.enabled = true;
        var emL = flameL.emission; emL.enabled = false;
        var emR = flameR.emission; emR.enabled = false;
        trail.emitting = false;
    }
}
