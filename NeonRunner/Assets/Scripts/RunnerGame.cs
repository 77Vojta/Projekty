using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hlavní řízení hry: generuje nekonečnou trať, překážky, mince a vylepšení
/// (jetpack, magnet), hlídá kolize, skóre a vykresluje HUD.
/// </summary>
public class RunnerGame : MonoBehaviour
{
    public enum State { Ready, Running, GameOver }
    public enum Kind { Low, High, Block, Coin, Jetpack, Magnet }

    class Item
    {
        public Kind kind;
        public GameObject go;
        public Bounds bounds;
        public bool pulled;      // mince přitahovaná magnetem
        public float baseY;      // pro houpání vylepšení
    }

    struct Segment
    {
        public GameObject go;
        public float endZ;
    }

    [Header("Materiály – trať a okolí")]
    public Material groundMat;
    public Material stripeMat;
    public Material railMat;
    public Material sleeperMat;
    public Material wallMat;
    public Material buildingMat;
    public Material windowMat;
    public Material windowMat2;
    public Material windowMat3;
    public Material lampMat;

    [Header("Materiály – překážky")]
    public Material lowMat;
    public Material lowStripeMat;
    public Material highMat;
    public Material highLightMat;
    public Material trainMat;
    public Material trainWindowMat;
    public Material trainLightMat;
    public Material trainStripeMat;

    [Header("Materiály – předměty")]
    public Material coinMat;
    public Material magnetMat;
    public Material magnetTipMat;
    public Material jetpackMat;
    public Material pickupRingMat;

    [Header("Materiály – hráč a efekty")]
    public Material suitMat;
    public Material helmetMat;
    public Material visorMat;
    public Material packMat;
    public Material particleMat;

    [Header("Ladění")]
    public float laneWidth = 2.5f;
    public float startSpeed = 12f;
    public float maxSpeed = 30f;
    public float acceleration = 0.3f;
    public float segmentLength = 20f;
    public float viewDistance = 140f;
    public float jetpackDuration = 7f;
    public float magnetDuration = 10f;
    public float magnetRadius = 9f;

    public State CurrentState { get; private set; } = State.Ready;
    public float Speed { get; private set; }
    public int Coins { get; private set; }
    public int Score => Mathf.FloorToInt(distance) + Coins * 10;

    PlayerController player;
    Camera cam;
    float baseFov;
    readonly List<Item> items = new List<Item>();
    readonly Queue<Segment> segments = new Queue<Segment>();
    float distance;
    float nextSegmentZ;
    float nextRowZ;
    float gameOverTime;
    float magnetLeft;
    float flightEndZ = float.NegativeInfinity;
    float lastPowerUpZ;
    int airLane = 1;
    float shake;
    int best;
    bool newBest;
    ParticleSystem sparkles, debris;

    GUIStyle hudStyle, hudRightStyle, titleStyle, textStyle, centerStyle, smallStyle;

    static readonly Color Gold = new Color(1f, 0.85f, 0.2f);
    static readonly Color JetColor = new Color(1f, 0.55f, 0.1f);
    static readonly Color MagnetColor = new Color(1f, 0.2f, 0.3f);

    void Start()
    {
        best = PlayerPrefs.GetInt("best", 0);
        cam = Camera.main;
        baseFov = cam ? cam.fieldOfView : 65f;

        Color bg = cam ? cam.backgroundColor : new Color(0.04f, 0.03f, 0.1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = bg;
        RenderSettings.fogStartDistance = viewDistance * 0.4f;
        RenderSettings.fogEndDistance = viewDistance;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.28f);

        player = PlayerController.Create(this);
        sparkles = Fx.Burst(particleMat, "Sparkles", 2f, 6f, 0.35f);
        debris = Fx.Burst(particleMat, "Debris", 4f, 11f, 0.6f);

        nextSegmentZ = -segmentLength * 2f;
        nextRowZ = 35f;
        lastPowerUpZ = 0f;
        while (nextSegmentZ < viewDistance) SpawnSegment();
        while (nextRowZ < viewDistance) SpawnRow();

        if (cam) SnapCamera();
    }

    // ================================================================== smyčka

    void Update()
    {
        float dt = Time.deltaTime;

        switch (CurrentState)
        {
            case State.Ready:
                if (Keys.Start)
                {
                    CurrentState = State.Running;
                    Speed = startSpeed;
                }
                break;

            case State.Running:
                Speed = Mathf.Min(maxSpeed, Speed + acceleration * dt);
                player.Tick(dt, Speed);
                distance = player.transform.position.z;
                if (magnetLeft > 0f) magnetLeft = Mathf.Max(0f, magnetLeft - dt);
                PullCoins(dt);
                CheckCollisions();
                Recycle();
                break;

            case State.GameOver:
                if (Time.time - gameOverTime > 0.6f && Keys.Restart)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;
        }

        if (Keys.Quit) Application.Quit();

        AnimateItems(dt);
        UpdateCamera(dt);
    }

    void CheckCollisions()
    {
        Bounds pb = player.GetBounds();
        for (int i = items.Count - 1; i >= 0; i--)
        {
            Item it = items[i];
            if (it.bounds.max.z < pb.min.z - 1f || it.bounds.min.z > pb.max.z + 1f) continue;
            if (!it.bounds.Intersects(pb)) continue;

            switch (it.kind)
            {
                case Kind.Coin:
                    Coins++;
                    Fx.Emit(sparkles, it.bounds.center, Gold, 6);
                    RemoveAt(i);
                    break;

                case Kind.Jetpack:
                    Fx.Emit(sparkles, it.bounds.center, JetColor, 25);
                    RemoveAt(i);
                    StartJetpack();
                    return; // StartJetpack mění seznam předmětů

                case Kind.Magnet:
                    Fx.Emit(sparkles, it.bounds.center, MagnetColor, 25);
                    magnetLeft = magnetDuration;
                    RemoveAt(i);
                    break;

                default:
                    if (player.Invulnerable) break;
                    GameOver();
                    return;
            }
        }
    }

    void RemoveAt(int i)
    {
        Destroy(items[i].go);
        items.RemoveAt(i);
    }

    void StartJetpack()
    {
        float pz = player.transform.position.z;
        float avgSpeed = Mathf.Min(maxSpeed, Speed + acceleration * jetpackDuration * 0.5f);
        flightEndZ = pz + avgSpeed * (jetpackDuration + 0.8f); // včetně sestupu
        player.StartJetpack(jetpackDuration);

        // v místě přistání nesmí nic stát
        for (int i = items.Count - 1; i >= 0; i--)
        {
            Item it = items[i];
            bool obstacle = it.kind == Kind.Low || it.kind == Kind.High || it.kind == Kind.Block;
            if (obstacle && it.bounds.max.z > flightEndZ - 30f && it.bounds.min.z < flightEndZ + 15f)
                RemoveAt(i);
        }

        // mince ve vzduchu na už vygenerovaném kusu trati
        SpawnAirCoins(pz + 12f, Mathf.Min(nextRowZ, flightEndZ));
    }

    void GameOver()
    {
        CurrentState = State.GameOver;
        gameOverTime = Time.time;
        shake = 0.45f;
        Fx.Emit(debris, player.Center, new Color(0.3f, 1f, 0.9f), 40);
        Fx.Emit(debris, player.Center, new Color(1f, 0.3f, 0.8f), 30);
        player.Crash();
        if (Score > best)
        {
            best = Score;
            newBest = true;
            PlayerPrefs.SetInt("best", best);
            PlayerPrefs.Save();
        }
    }

    void Recycle()
    {
        float pz = player.transform.position.z;

        while (nextSegmentZ < pz + viewDistance) SpawnSegment();
        while (nextRowZ < pz + viewDistance) SpawnRow();

        while (segments.Count > 0 && segments.Peek().endZ < pz - 20f)
            Destroy(segments.Dequeue().go);

        for (int i = items.Count - 1; i >= 0; i--)
            if (items[i].bounds.max.z < pz - 10f) RemoveAt(i);
    }

    void PullCoins(float dt)
    {
        Vector3 target = player.Center;
        float r2 = magnetRadius * magnetRadius;
        foreach (Item it in items)
        {
            if (it.kind != Kind.Coin) continue;
            Vector3 pos = it.go.transform.position;
            if (!it.pulled)
            {
                if (magnetLeft <= 0f) continue;
                if ((pos - target).sqrMagnitude > r2 || pos.z < target.z - 1f) continue;
                it.pulled = true;
            }
            pos = Vector3.MoveTowards(pos, target, (Speed + 25f) * dt);
            it.go.transform.position = pos;
            it.bounds.center = pos;
        }
    }

    void AnimateItems(float dt)
    {
        float t = Time.time;
        foreach (Item it in items)
        {
            if (!it.go) continue;
            switch (it.kind)
            {
                case Kind.Coin:
                    it.go.transform.Rotate(0f, 180f * dt, 0f, Space.World);
                    break;
                case Kind.Jetpack:
                case Kind.Magnet:
                    it.go.transform.Rotate(0f, 120f * dt, 0f, Space.World);
                    Vector3 p = it.go.transform.position;
                    p.y = it.baseY + Mathf.Sin(t * 3f) * 0.15f;
                    it.go.transform.position = p;
                    break;
            }
        }
    }

    // ================================================================== kamera

    Vector3 CameraTarget()
    {
        Vector3 p = player.transform.position;
        float follow = p.y > 2f ? 0.75f : 0.4f;
        return new Vector3(p.x * 0.6f, 4.2f + p.y * follow, p.z - 7.5f);
    }

    void SnapCamera()
    {
        cam.transform.position = CameraTarget();
        LookAhead();
    }

    void UpdateCamera(float dt)
    {
        if (!cam || !player) return;
        Vector3 target = CameraTarget();
        Vector3 pos = cam.transform.position;
        float k = 1f - Mathf.Exp(-8f * dt);
        pos.x = Mathf.Lerp(pos.x, target.x, k);
        pos.y = Mathf.Lerp(pos.y, target.y, k);
        pos.z = target.z; // v ose Z kamera drží krok přesně
        cam.transform.position = pos;
        LookAhead();

        if (shake > 0f)
        {
            cam.transform.position += Random.insideUnitSphere * shake * 0.5f;
            shake -= dt;
        }

        float speed01 = Mathf.InverseLerp(startSpeed, maxSpeed, Speed);
        float fov = baseFov + speed01 * 10f + (player.Flying ? 4f : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, k);
    }

    void LookAhead()
    {
        Vector3 p = player.transform.position;
        cam.transform.LookAt(new Vector3(p.x * 0.3f, 1.2f + p.y * 0.6f, p.z + 8f));
    }

    // ================================================================== stavba světa

    float LaneX(int lane) => (lane - 1) * laneWidth;

    void SpawnSegment()
    {
        var seg = new GameObject("Segment").transform;
        float z0 = nextSegmentZ;
        float zc = z0 + segmentLength * 0.5f;
        float width = laneWidth * 3f + 1f;

        // podklad a svítící dělicí čáry
        Shapes.Box(seg, new Vector3(0f, -0.25f, zc), new Vector3(width, 0.5f, segmentLength), groundMat);
        for (int i = 0; i < 2; i++)
        {
            float x = (i == 0 ? -0.5f : 0.5f) * laneWidth;
            Shapes.Box(seg, new Vector3(x, 0.01f, zc), new Vector3(0.07f, 0.02f, segmentLength), stripeMat, false);
        }

        // koleje: dvě kolejnice a pražce v každém pruhu
        for (int lane = 0; lane < 3; lane++)
        {
            float x = LaneX(lane);
            Shapes.Box(seg, new Vector3(x - 0.55f, 0.07f, zc), new Vector3(0.09f, 0.12f, segmentLength), railMat, false);
            Shapes.Box(seg, new Vector3(x + 0.55f, 0.07f, zc), new Vector3(0.09f, 0.12f, segmentLength), railMat, false);
            for (float s = 1.25f; s < segmentLength; s += 2.5f)
                Shapes.Box(seg, new Vector3(x, 0.02f, z0 + s), new Vector3(1.6f, 0.05f, 0.35f), sleeperMat, false);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            float edge = side * (width * 0.5f + 0.25f);

            // zídka se svítící hranou
            Shapes.Box(seg, new Vector3(edge, 0.4f, zc), new Vector3(0.5f, 0.8f, segmentLength), wallMat);
            Shapes.Box(seg, new Vector3(edge - side * 0.22f, 0.82f, zc), new Vector3(0.06f, 0.04f, segmentLength), stripeMat, false);

            // lampa
            float lx = side * (width * 0.5f + 0.9f);
            float lz = z0 + 10f;
            Shapes.Box(seg, new Vector3(lx, 2.1f, lz), new Vector3(0.14f, 4.2f, 0.14f), wallMat);
            Shapes.Box(seg, new Vector3(lx - side * 0.55f, 4.15f, lz), new Vector3(1.1f, 0.1f, 0.12f), wallMat);
            Shapes.Box(seg, new Vector3(lx - side * 1.0f, 4.05f, lz), new Vector3(0.5f, 0.08f, 0.3f), lampMat, false);

            // budovy s okny
            for (int b = 0; b < 2; b++)
            {
                float h = Random.Range(6f, 22f);
                float w = Random.Range(3f, 6f);
                float d = Random.Range(6f, 9f);
                float x = side * (width * 0.5f + 3f + w * 0.5f + Random.Range(0f, 4f));
                float z = z0 + 5f + b * 10f;
                Material win = PickWindow();
                Shapes.Box(seg, new Vector3(x, h * 0.5f, z), new Vector3(w, h, d), buildingMat);
                Shapes.Box(seg, new Vector3(x, h + 0.05f, z), new Vector3(w + 0.1f, 0.1f, d + 0.1f), win, false);
                for (float wy = 2f; wy < h - 1f; wy += 2.2f)
                {
                    if (Random.value < 0.4f) continue;
                    Shapes.Box(seg, new Vector3(x - side * (w * 0.5f + 0.01f), wy, z),
                        new Vector3(0.05f, 0.45f, d * Random.Range(0.3f, 0.8f)), win, false);
                }
            }
        }

        segments.Enqueue(new Segment { go = seg.gameObject, endZ = z0 + segmentLength });
        nextSegmentZ += segmentLength;
    }

    void SpawnRow()
    {
        float z = nextRowZ;
        float gap = Mathf.Max(14f, Mathf.Max(Speed, startSpeed) * 1.1f) + Random.Range(0f, 6f);
        nextRowZ += gap;

        // během letu jetpackem jen mince ve vzduchu
        if (z < flightEndZ + 15f)
        {
            SpawnAirCoins(z, Mathf.Min(z + gap, flightEndZ));
            return;
        }

        float w = laneWidth * 0.8f;
        var kinds = new Kind?[3];
        int blocks = 0;

        for (int lane = 0; lane < 3; lane++)
        {
            float r = Random.value;
            if (r < 0.35f) kinds[lane] = null;
            else if (r < 0.55f) kinds[lane] = Kind.Low;
            else if (r < 0.75f) kinds[lane] = Kind.High;
            else { kinds[lane] = Kind.Block; blocks++; }
        }
        if (blocks == 3) kinds[Random.Range(0, 3)] = null; // vždy musí existovat cesta

        bool powerUpPlaced = false;
        for (int lane = 0; lane < 3; lane++)
        {
            float x = LaneX(lane);
            switch (kinds[lane])
            {
                case null:
                    if (!powerUpPlaced && CanSpawnPowerUp(z) && Random.value < 0.12f)
                    {
                        SpawnPowerUp(Random.value < 0.5f ? Kind.Jetpack : Kind.Magnet, x, z);
                        powerUpPlaced = true;
                    }
                    else if (Random.value < 0.6f) SpawnCoinLine(x, z - 4f, 5, 1f);
                    break;

                case Kind.Low:
                    SpawnLowBarrier(x, z, w);
                    if (Random.value < 0.5f) SpawnCoinLine(x, z - 0.5f, 1, 2.3f);
                    break;

                case Kind.High:
                    SpawnHighGate(x, z, w);
                    break;

                case Kind.Block:
                    SpawnTrain(x, z, w, Random.Range(6f, 10f));
                    break;
            }
        }
    }

    Material PickWindow()
    {
        float r = Random.value;
        if (r < 0.45f || !windowMat2) return windowMat;
        if (r < 0.8f || !windowMat3) return windowMat2;
        return windowMat3;
    }

    bool CanSpawnPowerUp(float z)
    {
        return z > 120f && z - lastPowerUpZ > 220f && magnetLeft <= 0f && !player.Flying;
    }

    Item AddItem(Kind kind, Transform root, Vector3 size)
    {
        var it = new Item { kind = kind, go = root.gameObject, bounds = new Bounds(root.position, size), baseY = root.position.y };
        items.Add(it);
        return it;
    }

    void SpawnLowBarrier(float x, float z, float w)
    {
        Transform root = Shapes.Root("LowBarrier", new Vector3(x, 0.4f, z));
        Shapes.Box(root, new Vector3(-w * 0.5f + 0.1f, 0f, 0f), new Vector3(0.14f, 0.8f, 0.3f), lowMat);
        Shapes.Box(root, new Vector3(w * 0.5f - 0.1f, 0f, 0f), new Vector3(0.14f, 0.8f, 0.3f), lowMat);
        Shapes.Box(root, new Vector3(0f, 0.15f, 0f), new Vector3(w, 0.36f, 0.14f), lowStripeMat, false);
        for (int i = -1; i <= 1; i++)
            Shapes.Box(root, new Vector3(i * w * 0.3f, 0.15f, 0f), new Vector3(0.22f, 0.37f, 0.15f), lowMat, false);
        AddItem(Kind.Low, root, new Vector3(w, 0.8f, 0.5f));
    }

    void SpawnHighGate(float x, float z, float w)
    {
        // hitbox: výška 1,1 až 2,4 m – podklouznutím se dá projet
        Transform root = Shapes.Root("HighGate", new Vector3(x, 1.75f, z));
        Shapes.Box(root, new Vector3(-w * 0.5f, -0.5f, 0f), new Vector3(0.14f, 2.5f, 0.3f), highMat);
        Shapes.Box(root, new Vector3(w * 0.5f, -0.5f, 0f), new Vector3(0.14f, 2.5f, 0.3f), highMat);
        Shapes.Box(root, new Vector3(0f, 0.4f, 0f), new Vector3(w + 0.1f, 0.35f, 0.3f), highLightMat, false);
        Shapes.Box(root, new Vector3(0f, -0.15f, 0f), new Vector3(w * 0.92f, 0.65f, 0.08f), highMat);
        Shapes.Box(root, new Vector3(0f, -0.15f, -0.05f), new Vector3(w * 0.7f, 0.12f, 0.02f), highLightMat, false);
        Shapes.Box(root, new Vector3(-w * 0.35f, 0.62f, -0.1f), new Vector3(0.15f, 0.12f, 0.1f), trainLightMat, false);
        Shapes.Box(root, new Vector3(w * 0.35f, 0.62f, -0.1f), new Vector3(0.15f, 0.12f, 0.1f), trainLightMat, false);
        AddItem(Kind.High, root, new Vector3(w, 1.3f, 0.5f));
    }

    void SpawnTrain(float x, float z, float w, float len)
    {
        Transform root = Shapes.Root("Train", new Vector3(x, 1.5f, z + len * 0.5f));
        float front = -len * 0.5f;
        Shapes.Box(root, new Vector3(0f, -0.1f, 0f), new Vector3(w, 2.6f, len), trainMat);
        Shapes.Box(root, new Vector3(0f, 1.32f, 0f), new Vector3(w * 0.85f, 0.25f, len * 0.95f), railMat);
        Shapes.Box(root, new Vector3(0f, -1.25f, 0f), new Vector3(w * 0.9f, 0.3f, len * 0.9f), sleeperMat);
        for (int side = -1; side <= 1; side += 2)
        {
            Shapes.Box(root, new Vector3(side * (w * 0.5f + 0.01f), 0.45f, 0f), new Vector3(0.04f, 0.55f, len * 0.85f), trainWindowMat, false);
            Shapes.Box(root, new Vector3(side * (w * 0.5f + 0.01f), -0.75f, 0f), new Vector3(0.04f, 0.14f, len * 0.95f), trainStripeMat, false);
        }
        Shapes.Box(root, new Vector3(0f, 0.45f, front - 0.01f), new Vector3(w * 0.75f, 0.6f, 0.04f), trainWindowMat, false);
        Shapes.Box(root, new Vector3(-0.55f, -0.6f, front - 0.02f), new Vector3(0.32f, 0.2f, 0.04f), trainLightMat, false);
        Shapes.Box(root, new Vector3(0.55f, -0.6f, front - 0.02f), new Vector3(0.32f, 0.2f, 0.04f), trainLightMat, false);
        AddItem(Kind.Block, root, new Vector3(w, 3f, len));
    }

    void SpawnCoin(Vector3 c)
    {
        GameObject g = Shapes.Make(PrimitiveType.Cylinder, null, c, new Vector3(0.7f, 0.06f, 0.7f), coinMat, false);
        g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        items.Add(new Item { kind = Kind.Coin, go = g, bounds = new Bounds(c, new Vector3(0.9f, 0.9f, 0.9f)), baseY = c.y });
    }

    void SpawnCoinLine(float x, float z, int count, float y)
    {
        for (int i = 0; i < count; i++) SpawnCoin(new Vector3(x, y, z + i * 2f));
    }

    void SpawnAirCoins(float fromZ, float toZ)
    {
        for (float z = fromZ; z < toZ; z += 2.5f)
        {
            if (Random.value < 0.06f) airLane = Mathf.Clamp(airLane + (Random.value < 0.5f ? -1 : 1), 0, 2);
            SpawnCoin(new Vector3(LaneX(airLane), PlayerController.FlyHeight + 1f, z));
        }
    }

    void SpawnPowerUp(Kind kind, float x, float z)
    {
        lastPowerUpZ = z;
        Transform root = Shapes.Root(kind.ToString(), new Vector3(x, 1.2f, z));

        if (kind == Kind.Jetpack)
        {
            Shapes.Box(root, new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.6f, 0.25f), packMat);
            Shapes.Make(PrimitiveType.Capsule, root, new Vector3(-0.2f, 0f, -0.18f), new Vector3(0.28f, 0.38f, 0.28f), jetpackMat);
            Shapes.Make(PrimitiveType.Capsule, root, new Vector3(0.2f, 0f, -0.18f), new Vector3(0.28f, 0.38f, 0.28f), jetpackMat);
            Shapes.Box(root, new Vector3(0f, 0.1f, 0.13f), new Vector3(0.3f, 0.08f, 0.02f), visorMat, false);
        }
        else
        {
            Shapes.Box(root, new Vector3(-0.35f, 0.05f, 0f), new Vector3(0.25f, 0.7f, 0.25f), magnetMat);
            Shapes.Box(root, new Vector3(0.35f, 0.05f, 0f), new Vector3(0.25f, 0.7f, 0.25f), magnetMat);
            Shapes.Box(root, new Vector3(0f, -0.3f, 0f), new Vector3(0.95f, 0.25f, 0.25f), magnetMat);
            Shapes.Box(root, new Vector3(-0.35f, 0.48f, 0f), new Vector3(0.26f, 0.18f, 0.26f), magnetTipMat);
            Shapes.Box(root, new Vector3(0.35f, 0.48f, 0f), new Vector3(0.26f, 0.18f, 0.26f), magnetTipMat);
        }

        // svítící kruh pod předmětem
        Shapes.Make(PrimitiveType.Cylinder, root, new Vector3(0f, -1.1f, 0f), new Vector3(1.3f, 0.01f, 1.3f), pickupRingMat, false);
        AddItem(kind, root, new Vector3(1.3f, 1.4f, 1.3f));
    }

    // ================================================================== HUD

    void OnGUI()
    {
        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            hudStyle.normal.textColor = Color.white;
            hudRightStyle = new GUIStyle(hudStyle) { alignment = TextAnchor.UpperRight };
            centerStyle = new GUIStyle(hudStyle) { alignment = TextAnchor.MiddleCenter };
            titleStyle = new GUIStyle(hudStyle) { fontSize = 72, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(0.3f, 1f, 0.9f);
            textStyle = new GUIStyle(hudStyle) { fontSize = 26, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(hudStyle) { fontSize = 20 };
        }

        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;
        const float h = 720f;

        GUI.Label(new Rect(24, 16, 500, 40), $"Skóre: {Score}", hudStyle);
        GUI.Label(new Rect(24, 52, 500, 40), $"Mince: {Coins}", hudStyle);
        GUI.Label(new Rect(w - 524, 16, 500, 40), $"Rekord: {best}", hudRightStyle);
        if (CurrentState == State.Running)
            GUI.Label(new Rect(w - 524, 52, 500, 40), $"{Speed * 3.6f:0} km/h", hudRightStyle);

        float barY = 100f;
        if (player && player.Flying)
        {
            Bar(24, barY, "Jetpack", player.JetpackLeft / jetpackDuration, JetColor);
            barY += 44f;
        }
        if (magnetLeft > 0f)
            Bar(24, barY, "Magnet", magnetLeft / magnetDuration, MagnetColor);

        if (CurrentState == State.Ready)
        {
            Dim(w, h, 0.45f);
            GUI.Label(new Rect(0, h * 0.2f, w, 100), "NEON RUNNER", titleStyle);
            GUI.Label(new Rect(0, h * 0.4f, w, 40), "← → / A D  změna pruhu", textStyle);
            GUI.Label(new Rect(0, h * 0.4f + 38, w, 40), "↑ / W / mezerník  skok", textStyle);
            GUI.Label(new Rect(0, h * 0.4f + 76, w, 40), "↓ / S  podklouznutí", textStyle);
            GUI.Label(new Rect(0, h * 0.4f + 126, w, 40), "Seber jetpack a vzlétni nad trať, magnet přitáhne mince.", textStyle);
            GUI.Label(new Rect(0, h * 0.75f, w, 40), "Stiskni MEZERNÍK pro start", centerStyle);
        }
        else if (CurrentState == State.GameOver)
        {
            Dim(w, h, 0.55f);
            GUI.Label(new Rect(0, h * 0.25f, w, 100), "KONEC HRY", titleStyle);
            GUI.Label(new Rect(0, h * 0.45f, w, 40), $"Skóre: {Score}   (vzdálenost {Mathf.FloorToInt(distance)} m, mince {Coins})", textStyle);
            if (newBest) GUI.Label(new Rect(0, h * 0.45f + 44, w, 40), "Nový rekord!", centerStyle);
            GUI.Label(new Rect(0, h * 0.7f, w, 40), "R nebo mezerník = hrát znovu     Esc = konec", textStyle);
        }
    }

    void Bar(float x, float y, string label, float fill, Color color)
    {
        GUI.Label(new Rect(x, y, 140, 32), label, smallStyle);
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.2f);
        GUI.DrawTexture(new Rect(x + 110, y + 8, 200, 16), Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(x + 110, y + 8, 200 * Mathf.Clamp01(fill), 16), Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Dim(float w, float h, float alpha)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = old;
    }
}
