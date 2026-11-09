using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Rollzy
{
    public enum GameState { Menu, Ready, Playing, Paused, Revive, Failed, Complete }

    /// <summary>
    /// The whole game loop: builds levels, runs the ball in track space at a fixed 120 Hz,
    /// handles win/lose/revive, rewards and ad placements. The scene only needs this component.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RollzyGame : MonoBehaviour
    {
        [Header("Assigned by Rollzy 3D → 1. Create Gameplay Scene")]
        [SerializeField] Material litTemplate;
        [SerializeField] Material unlitTemplate;
        [SerializeField] Material particleTemplate;
        [SerializeField] Material skyboxTemplate;
        [SerializeField] UIDocument uiDocument;
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] Camera mainCamera;
        [SerializeField] Light sun;

        public SaveData Save { get; private set; }
        public GameState State { get; private set; } = GameState.Menu;
        public LevelData Level => level;
        public IAdProvider Ads { get; private set; }
        public Sfx Sfx { get; private set; }
        public Thumbnails Thumbs { get; private set; }
        public int LastReward { get; private set; }
        public int RunCoins => runCoins;
        public bool CanSkip => failsHere >= 2;

        const float R = Balance.BallRadius;
        const float Dt = 1f / 120f;

        LevelData level;
        Atmosphere atmosphere;
        Fx fx;
        GameUI ui;
        SwipeInput input;
        CameraRig rig;
        Transform ballRoot, ballMesh;
        MeshRenderer ballRenderer;
        float accumulator;

        // Ball state (track space)
        float bx, by, bz, vx, vy, vz, fallWall, lastG, jumpReq, invuln, squash, simT, trailClock;
        int lane, region, runCoins, failsHere, failsLevel = -1;
        bool running, dead, fallen, airborne, revived, rewardedUsedThisScreen;
        string trialSkin;
        GameState pausedFrom;
        Coroutine reviveRoutine;
        Vector3 ballWorld;

        // ------------------------------------------------------------ setup
        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Save = SaveData.Load();
            Haptics.Enabled = Save.vibration;
            Mats.Init(litTemplate, unlitTemplate, particleTemplate);

            if (mainCamera == null) mainCamera = Camera.main;
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.nearClipPlane = 0.3f;
            mainCamera.farClipPlane = 1200;
            mainCamera.cullingMask &= ~(1 << 31);
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.intensity = 1.05f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);

            atmosphere = new Atmosphere(skyboxTemplate, new Material(litTemplate) { name = "Ground" });
            fx = new GameObject("Fx").AddComponent<Fx>();
            fx.Init(Mats.Particles(Mats.SoftDot()));
            Sfx = gameObject.AddComponent<Sfx>();
            Sfx.enabledByPlayer = Save.sound;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);
            ballRoot = new GameObject("Ball").transform;
            ballMesh = new GameObject("BallMesh").transform;
            ballMesh.SetParent(ballRoot, false);
            ballMesh.gameObject.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
            ballRenderer = ballMesh.gameObject.AddComponent<MeshRenderer>();
            ballRenderer.shadowCastingMode = ShadowCastingMode.On;
            ballRenderer.sharedMaterial = Skins.Get(Save.ball);
            Thumbs = new Thumbnails(sphereMesh);

            rig = new CameraRig(mainCamera);
        }

        // UI, ads and input are set up in Start: the UIDocument builds its visual tree in OnEnable.
        void Start()
        {
            ui = new GameUI(this, uiDocument, styleSheet);

#if ROLLZY_ADMOB
            Ads = new AdMobProvider();
#else
            Ads = new TestAdProvider(ui.ShowTestAd);
#endif
            Ads.Init();

            input = GetComponent<SwipeInput>();
            if (input == null) input = gameObject.AddComponent<SwipeInput>();
            input.IsBlocked = ui.IsPointerOverUI;
            input.Swiped += OnSwipe;
            input.Tapped += OnTap;
            input.BackPressed += OnBack;
            GoHome();
        }

        void OnDestroy()
        {
            level?.Destroy();
            if (input != null) { input.Swiped -= OnSwipe; input.Tapped -= OnTap; input.BackPressed -= OnBack; }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            Save.Save();
            if (State == GameState.Playing) Pause();
        }

        // ------------------------------------------------------------ flow
        void LoadLevel(int n)
        {
            level?.Destroy();
            level = LevelBuilder.Build(n);
            bx = Balance.LaneX(1); by = R; bz = 0; vx = vy = vz = 0; lane = 1;
            running = dead = fallen = airborne = false;
            fallWall = float.PositiveInfinity; lastG = jumpReq = -1e9f; invuln = -1; squash = 0;
            runCoins = 0; region = 0; revived = false; trialSkin = null; rewardedUsedThisScreen = false;
            ballMesh.localRotation = Quaternion.identity;
            ballRenderer.sharedMaterial = Skins.Get(Save.ball);
            atmosphere.Apply(level, 0, Vector3.zero);
            UpdateBallTransform(0);
            rig.UpdateFov(0, false, 0);
            rig.Follow(level.path, bx, by, bz, 0, true);
        }

        public void GoHome()
        {
            StopRevive();
            if (level == null || State != GameState.Menu || level.level != Save.maxLevel) LoadLevel(Save.maxLevel);
            State = GameState.Menu;
            ui.ShowHome();
        }

        public void StartLevel(int n)
        {
            StopRevive();
            if (n != failsLevel) { failsLevel = n; failsHere = 0; }
            LoadLevel(n);
            State = GameState.Ready;
            ui.ShowReady();
        }

        public void Restart() => StartLevel(level.level);

        void Begin()
        {
            State = GameState.Playing;
            running = true;
            ui.ShowGameplay();
            ui.ShowLocation(level.biomes[0].name);
        }

        public void Pause()
        {
            if (State != GameState.Playing && State != GameState.Ready) return;
            pausedFrom = State;
            State = GameState.Paused;
            ui.ShowPause();
        }

        public void Resume()
        {
            State = pausedFrom;
            if (State == GameState.Ready) ui.ShowReady(); else ui.ShowGameplay();
        }

        void OnTap()
        {
            if (State == GameState.Ready) Begin();
        }

        void OnSwipe(SwipeDirection dir)
        {
            if (State == GameState.Ready) Begin();
            if (State != GameState.Playing || !running) return;
            if (dir == SwipeDirection.Left) lane = Mathf.Max(0, lane - 1);
            else if (dir == SwipeDirection.Right) lane = Mathf.Min(Balance.Lanes - 1, lane + 1);
            else if (dir == SwipeDirection.Up) jumpReq = simT;
        }

        void OnBack()
        {
            if (ui.AdOpen) return;
            switch (State)
            {
                case GameState.Playing:
                case GameState.Ready: Pause(); break;
                case GameState.Paused: Resume(); break;
                case GameState.Menu: ui.Back(); break;
                case GameState.Failed:
                case GameState.Complete: GoHome(); break;
            }
        }

        // ------------------------------------------------------------ death, revive, win
        void Die(bool hit)
        {
            if (dead) return;
            dead = true;
            running = false;
            if (hit) { vx = 0; vy = 4; vz = -5; } else vz *= 0.3f;
            Sfx.Play(Sfx.crash);
            Haptics.Pulse(90);
            rig.shake = 0.6f;
            fx.Crash(ballWorld, Skins.Tint(trialSkin ?? Save.ball));
            State = GameState.Revive;
            if (revived) { Invoke(nameof(FinalizeFail), 0.7f); return; }
            reviveRoutine = StartCoroutine(ReviveCountdown());
        }

        IEnumerator ReviveCountdown()
        {
            yield return new WaitForSeconds(0.45f);
            const float total = 5f;
            for (float t = 0; t < total; t += Time.unscaledDeltaTime)
            {
                if (State != GameState.Revive) yield break;
                ui.ShowRevive(Mathf.CeilToInt(total - t), 1 - t / total);
                yield return null;
            }
            FinalizeFail();
        }

        void StopRevive()
        {
            if (reviveRoutine != null) StopCoroutine(reviveRoutine);
            reviveRoutine = null;
            CancelInvoke(nameof(FinalizeFail));
        }

        public void AcceptRevive()
        {
            StopRevive();
            Ads.ShowRewarded(ok =>
            {
                if (ok) DoRevive();
                else { ui.Snack("The ad didn't finish, so no revive this time."); FinalizeFail(); }
            });
        }

        public void DeclineRevive()
        {
            StopRevive();
            FinalizeFail();
        }

        void FinalizeFail()
        {
            if (State != GameState.Revive) return;
            StopRevive();
            State = GameState.Failed;
            failsHere++;
            LastReward = runCoins;
            AddCoins(LastReward);
            ui.ShowFailed();
        }

        void DoRevive()
        {
            revived = true;
            if (fallen) { bz = fallWall + R + 1.2f; fallen = false; fallWall = float.PositiveInfinity; }
            foreach (var g in level.gaps) if (bz > g.x - 1 && bz < g.y + 0.5f) bz = g.y + 1.2f;
            foreach (var b in level.boxes) if (Mathf.Abs(b.z - bz) < b.hz + 1.2f) bz = Mathf.Max(bz, b.z + b.hz + R + 0.4f);
            by = R; vx = vy = vz = 0; bx = Balance.LaneX(lane);
            dead = false; running = true; invuln = simT + 2.2f;
            State = GameState.Playing;
            ui.ShowGameplay();
        }

        void Complete()
        {
            running = false;
            State = GameState.Complete;
            rewardedUsedThisScreen = false;
            float ratio = level.totalCoins > 0 ? runCoins / (float)level.totalCoins : 1;
            int stars = 1 + (ratio >= 0.5f ? 1 : 0) + (ratio >= 0.85f ? 1 : 0);
            LastReward = runCoins + Balance.LevelCompleteBonus;
            AddCoins(LastReward);
            Save.SetStars(level.level, stars);
            int previousMax = Save.maxLevel;
            Save.maxLevel = Mathf.Max(Save.maxLevel, level.level + 1);
            var unlocked = CheckUnlocks();
            if (Save.maxLevel > previousMax && Balance.WorldOf(Save.maxLevel) != Balance.WorldOf(previousMax) && Save.maxLevel <= Balance.MapLevels)
                unlocked.Insert(0, "New world: " + Catalog.Biomes[Balance.WorldOf(Save.maxLevel)].name + "!");
            Save.Save();
            Sfx.Play(Sfx.win);
            Haptics.Pulse(40);
            fx.Confetti(ballWorld);
            StartCoroutine(ShowCompleteSoon(stars, unlocked));
        }

        IEnumerator ShowCompleteSoon(int stars, List<string> unlocked)
        {
            yield return new WaitForSeconds(0.7f);
            if (State == GameState.Complete) ui.ShowComplete(stars, string.Join("  ·  ", unlocked));
        }

        public void TripleCoins()
        {
            Ads.ShowRewarded(ok =>
            {
                if (!ok) { ui.Snack("Watch the whole ad to triple your coins."); return; }
                rewardedUsedThisScreen = true;
                AddCoins(LastReward * 2);
                LastReward *= 3;
                Sfx.Play(Sfx.reward);
                ui.ShowComplete(-1, null);
            });
        }

        public void NextLevel()
        {
            Save.completions++;
            Save.Save();
            int next = level.level + 1;
            bool interstitial = Save.completions % Balance.InterstitialEveryNLevels == 0 && !rewardedUsedThisScreen;
            if (interstitial) Ads.ShowInterstitial(() => StartLevel(next));
            else StartLevel(next);
        }

        public void SkipLevel()
        {
            Ads.ShowRewarded(ok =>
            {
                if (!ok) { ui.Snack("Watch the whole ad to skip this level."); return; }
                Save.maxLevel = Mathf.Max(Save.maxLevel, level.level + 1);
                CheckUnlocks();
                Save.Save();
                StartLevel(level.level + 1);
            });
        }

        public void TrySkin(string id)
        {
            Ads.ShowRewarded(ok =>
            {
                if (!ok) return;
                trialSkin = id;
                ballRenderer.sharedMaterial = Skins.Get(id);
                ui.Snack("Trying " + Catalog.Ball(id).name + " for this run!");
                ui.ShowReady();
            });
        }

        public List<string> CheckUnlocks()
        {
            var names = new List<string>();
            foreach (var it in Catalog.Balls)
                if (it.unlock == UnlockType.Level && Save.maxLevel > it.amount && !Save.Owns(true, it.id)) { Save.Own(true, it.id); names.Add(it.name + " ball"); }
            foreach (var it in Catalog.Trails)
                if (it.unlock == UnlockType.Level && Save.maxLevel > it.amount && !Save.Owns(false, it.id)) { Save.Own(false, it.id); names.Add(it.name + " trail"); }
            return names;
        }

        public void AddCoins(int amount)
        {
            Save.coins = Mathf.Max(0, Save.coins + amount);
            Save.Save();
            ui.RefreshCoins();
        }

        public void EquipBall(string id)
        {
            Save.ball = id;
            Save.Save();
            if (trialSkin == null) ballRenderer.sharedMaterial = Skins.Get(id);
        }

        public void SetSound(bool on) { Save.sound = on; Sfx.enabledByPlayer = on; Save.Save(); }
        public void SetVibration(bool on) { Save.vibration = on; Haptics.Enabled = on; Save.Save(); }

        public void ResetProgress()
        {
            SaveData.Delete();
            Save = SaveData.Load();
            ballRenderer.sharedMaterial = Skins.Get(Save.ball);
            level?.Destroy();
            level = null;
            GoHome();
        }

        public string TryOffer()
        {
            if (trialSkin != null) return null;
            foreach (var b in Catalog.Balls)
                if (!Save.Owns(true, b.id) && b.unlock != UnlockType.Level && (b.id.GetHashCode() + level.level) % 3 == 0) return b.id;
            foreach (var b in Catalog.Balls)
                if (!Save.Owns(true, b.id) && b.unlock != UnlockType.Level) return b.id;
            return null;
        }

        public float SpeedKmh => (State == GameState.Playing ? vz : SpeedAt(bz)) * 3.6f;
        public float Progress => Mathf.Clamp01(bz / level.finishLine);

        // ------------------------------------------------------------ simulation (track space)
        float SpeedAt(float s) => level.baseSpeed * (1 + Balance.SpeedRamp * Mathf.Clamp01(s / level.finishLine));

        bool GapAt(float z, out Vector2 gap)
        {
            foreach (var g in level.gaps)
                if (z > g.x + 0.15f && z < g.y - 0.15f) { gap = g; return true; }
            gap = default;
            return false;
        }

        bool HitBox(ObstacleBox b)
        {
            float cx = Mathf.Clamp(bx, b.x - b.hx, b.x + b.hx);
            float cy = Mathf.Clamp(by, b.y - b.hy, b.y + b.hy);
            float cz = Mathf.Clamp(bz, b.z - b.hz, b.z + b.hz);
            float dx = bx - cx, dy = by - cy, dz = bz - cz;
            return dx * dx + dy * dy + dz * dz < (R * 0.85f) * (R * 0.85f);
        }

        void Step(float dt)
        {
            simT += dt;
            foreach (var m in level.movers)
            {
                float wave = Mathf.Sin(simT * m.speed + m.phase);
                if (m.vertical) m.box.y = m.baseY + (wave * 0.5f + 0.5f) * m.amp;
                else m.box.x = wave * m.amp;
                m.box.view.position = m.origin + m.rotation * new Vector3(m.box.x, m.box.y, 0);
            }
            foreach (var s in level.sweepers)
            {
                s.angle += s.speed * dt;
                s.spinner.localRotation = Quaternion.Euler(0, s.angle * Mathf.Rad2Deg, 0);
            }
            if (State != GameState.Playing && State != GameState.Revive && State != GameState.Failed && State != GameState.Complete) return;

            if (running)
            {
                vz = SpeedAt(bz);
                float laneSpeed = 14 + vz * 0.4f;
                vx = Mathf.Clamp((Balance.LaneX(lane) - bx) / dt, -laneSpeed, laneSpeed);
                if (simT - jumpReq <= Balance.JumpBuffer && simT - lastG <= Balance.CoyoteTime && vy <= 0.5f)
                {
                    vy = Balance.JumpVelocity;
                    jumpReq = lastG = -1e9f;
                    Sfx.Play(Sfx.jump);
                }
            }
            else if (!dead || by <= R + 1e-3f)
            {
                vx = Mathf.MoveTowards(vx, 0, 18 * dt);
                vz = Mathf.MoveTowards(vz, 0, 18 * dt);
            }

            vy -= Balance.Gravity * dt;
            bx += vx * dt; by += vy * dt; bz += vz * dt;

            bool overGap = GapAt(bz, out var gap);
            if (fallen) bz = Mathf.Min(bz, fallWall);
            else if (overGap)
            {
                if (by < R - 0.3f) { fallen = true; fallWall = gap.y - R; if (!dead) Die(false); }
            }
            else if (by < R)
            {
                if (airborne && vy < -3) squash = 1;
                by = R;
                vy = dead && vy < -3 ? -vy * 0.3f : 0;
            }
            bool grounded = !fallen && !overGap && by <= R + 1e-3f;
            if (grounded) lastG = simT;
            airborne = !grounded;

            if (running && simT > invuln)
            {
                foreach (var b in level.boxes)
                    if (Mathf.Abs(b.z - bz) < b.hz + 1 && HitBox(b)) { Die(true); break; }
            }
            if (running && simT > invuln)
            {
                foreach (var s in level.sweepers)
                {
                    if (Mathf.Abs(s.z - bz) > s.r + 1) continue;
                    float dx = bx, dz = bz - s.z;
                    bool hit = false;
                    if (by - R < 0.5f)
                    {
                        float ux = Mathf.Cos(s.angle), uz = -Mathf.Sin(s.angle);
                        float proj = Mathf.Clamp(dx * ux + dz * uz, -s.r, s.r);
                        float px = dx - ux * proj, pz = dz - uz * proj;
                        hit = px * px + pz * pz < Sq(0.15f + R * 0.8f);
                    }
                    if (!hit && by - R < 0.7f) hit = dx * dx + dz * dz < Sq(0.25f + R * 0.8f);
                    if (hit) { Die(true); break; }
                }
            }
            if (running)
            {
                foreach (var c in level.coins)
                {
                    if (c.alive && Mathf.Abs(c.z - bz) < 0.85f && Mathf.Abs(c.x - bx) < 0.85f && Mathf.Abs(c.y - by) < 0.85f)
                    {
                        c.alive = false;
                        c.root.gameObject.SetActive(false);
                        runCoins++;
                        Sfx.Play(Sfx.coin);
                        fx.Sparkle(level.path.ToWorld(c.x, c.y, c.z));
                    }
                }
                if (bz >= level.finishLine) Complete();
                int r = level.RegionAt(bz);
                if (r != region) { region = r; ui.ShowLocation(level.biomes[r].name); }
            }
            if (by < -40) { by = -40; vy = 0; }
            squash = Mathf.Max(0, squash - dt * 6);
        }

        static float Sq(float v) => v * v;

        // ------------------------------------------------------------ frame
        void Update()
        {
            if (level == null || ui == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool frozen = State == GameState.Paused || ui.AdOpen;
            if (!frozen)
            {
                accumulator += dt;
                while (accumulator >= Dt) { Step(Dt); accumulator -= Dt; }
            }
            float frameDt = frozen ? 0 : dt;
            UpdateBallTransform(frameDt);

            if (running && Save.trail != "none")
            {
                trailClock += frameDt;
                if (Random.value < 0.7f) fx.Trail(ballWorld, TrailColor(Save.trail, trailClock));
            }
            foreach (var c in level.coins)
                if (c.alive && Mathf.Abs(c.z - bz) < 90) c.spin.localRotation *= Quaternion.Euler(0, 170 * frameDt, 0);

            if (State == GameState.Menu) rig.Orbit(ballWorld, dt);
            else rig.Follow(level.path, bx, by, bz, frameDt, false);
            rig.UpdateFov(vz, State == GameState.Playing, dt);

            atmosphere.Apply(level, bz, ballWorld);
            if (State != GameState.Menu) ui.UpdateHud();
        }

        void UpdateBallTransform(float dt)
        {
            level.path.Frame(bz, out var p, out var q);
            ballWorld = p + q * new Vector3(bx, by, 0);
            Vector3 up = q * Vector3.up;
            ballRoot.SetPositionAndRotation(ballWorld - up * (0.14f * squash), q);
            ballRoot.localScale = new Vector3(1 + 0.22f * squash, 1 - 0.28f * squash, 1 + 0.22f * squash);
            float sp = Mathf.Sqrt(vx * vx + vz * vz);
            if (sp > 0.01f && dt > 0)
            {
                var axis = new Vector3(vz, 0, -vx).normalized;
                ballMesh.localRotation = Quaternion.AngleAxis(sp * dt / R * Mathf.Rad2Deg, axis) * ballMesh.localRotation;
            }
            if (State == GameState.Menu) ballMesh.Rotate(0, dt * 45, 0, Space.Self);
            ballRenderer.enabled = !(running && simT < invuln && Mathf.FloorToInt(simT * 12) % 2 == 0);
        }

        static Color TrailColor(string trail, float t)
        {
            switch (trail)
            {
                case "rainbow": return Color.HSVToRGB(t * 0.6f % 1, 0.9f, 1);
                case "fire": return new Color(1, 0.35f + Random.value * 0.45f, Random.value * 0.1f);
                case "stars": return Random.value < 0.5f ? new Color(1, 0.85f, 0.3f) : Color.white;
                case "neon": return Mathf.FloorToInt(t * 10) % 2 == 0 ? new Color(0, 0.9f, 1) : new Color(1, 0.24f, 0.6f);
                case "sparkle": return Color.HSVToRGB(Random.value, 0.8f, 1);
                default: return new Color(0.75f, 0.78f, 0.85f, 0.6f);
            }
        }
    }
}
