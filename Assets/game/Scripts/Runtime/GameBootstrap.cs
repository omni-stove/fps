using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// シーンの起点。Awake で床・プレイヤー (CharacterController + 目 + その子のカメラ + <see cref="ViewRig"/> + <see cref="FirstPersonLook"/>
    /// + <see cref="PlayerController"/> + カメラの <see cref="WeaponController"/> + 敵に撃たれる <see cref="Damageable"/> と胴の <see cref="Hitbox"/>)
    /// ・ライト・プレイヤーの前方に並べた <see cref="TargetDummyCount"/> 体の <see cref="TargetDummy"/>
    /// (<see cref="Damageable"/> + 頭と胴の <see cref="Hitbox"/>。倒すと 3 秒後に復活する)
    /// ・HUD (<see cref="UIDocument"/> + <see cref="PanelSettings"/> + <see cref="Hud"/>) を
    /// コードで生成する。参照はすべてここで解決するので、シーンにはこのコンポーネントを置いた GameObject だけあればよい。
    /// 一人称か三人称かは <see cref="ViewMode"/> で選ぶ (狙いと当たり判定はどちらも目から)。
    /// 生成物は <see cref="World"/> で読め、生成し終えたことは <see cref="Built"/> で受け取れる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string ReadyLog = "[Bootstrap] ready";

        public const string FloorName = "Floor";
        public const string PlayerName = "Player";

        /// <summary>プレイヤーに付ける Unity 組み込みのタグ。ミッションの範囲トリガーはこれでプレイヤーを見分ける。</summary>
        public const string PlayerTag = ObjectiveAreaBase.DefaultPlayerTag;
        public const string CameraName = "PlayerCamera";

        /// <summary>pitch を回し射撃の始点になる、プレイヤーの目の子 GameObject の名前。カメラはこの子。</summary>
        public const string EyeName = "PlayerEye";
        public const string LightName = "Directional Light";

        /// <summary>Plane は 10m 四方なので、5 倍で 50m 四方。</summary>
        public static readonly Vector3 FloorScale = new Vector3(5f, 1f, 5f);

        public const float PlayerHeight = 2f;
        public const float PlayerRadius = 0.5f;

        /// <summary>足元が床 (y = 0) に接する位置。CharacterController の中心はカプセルの中心。</summary>
        public static readonly Vector3 PlayerSpawn = new Vector3(0f, PlayerHeight / 2f, 0f);

        /// <summary>カプセル中心からの目の高さ。足元から 1.6m。</summary>
        public static readonly Vector3 EyeOffset = new Vector3(0f, 0.6f, 0f);

        public static readonly Vector3 LightEuler = new Vector3(50f, -30f, 0f);

        /// <summary>プレイヤーの最大体力。敵 (<see cref="EnemyAgent"/>) の射撃はプレイヤーの <see cref="Damageable"/> に届く。</summary>
        public const int PlayerMaxHealth = 100;

        /// <summary>標的ダミーをまとめる親の名前。</summary>
        public const string TargetsName = "TargetDummies";

        /// <summary>標的ダミーの名前 (ログの識別子) の頭。左から 1, 2, ... を付ける。</summary>
        public const string TargetDummyNamePrefix = "TargetDummy";

        /// <summary>プレイヤーの前方に並べる標的ダミーの数。</summary>
        public const int TargetDummyCount = 5;

        /// <summary>プレイヤーの足元から標的ダミーの列までの前方の距離 (m)。</summary>
        public const float TargetDummyDistance = 10f;

        /// <summary>隣り合う標的ダミーの間隔 (m)。胴の直径 1m に対して 1m の隙間を空ける。</summary>
        public const float TargetDummySpacing = 2f;

        public const int TargetDummyMaxHealth = TargetDummy.DefaultMaxHealth;
        public const float TargetDummyRespawnDelay = TargetDummy.DefaultRespawnDelay;

        public const string HudName = "HUD";
        public const string HudPanelSettingsName = "HudPanelSettings";

        /// <summary>HUD の基準解像度。これより大きい・小さい画面では幅と高さの中間で拡縮する。</summary>
        public static readonly Vector2Int HudReferenceResolution = new Vector2Int(1920, 1080);

        [SerializeField, Tooltip("床を生成するか。NavMesh を焼いた床を持つシーンでは切る")]
        private bool _createFloor = true;

        [SerializeField, Tooltip("プレイヤーの視点。一人称か三人称 (肩越し)")]
        private ViewMode _viewMode = ViewMode.FirstPerson;

        [SerializeField, Tooltip("プレイヤーの WeaponController に渡す武器の定義。シーン生成スクリプトが rifle を配線する")]
        private WeaponDefinition _weaponDefinition;

        private BootstrapWorld _world;

        /// <summary>生成済みのオブジェクト。生成前や失敗時は null。</summary>
        public BootstrapWorld World => _world;

        /// <summary>
        /// <see cref="Build"/> が生成し終えたときに、生成物を渡して 1 度だけ呼ばれる。生成に失敗したときは呼ばれない。
        /// 生成は Awake で済むので、購読する側は先に <see cref="World"/> を見て、null のときだけ購読して待つ。
        /// 購読者が例外を投げても生成は失敗にせず、例外をログに出して残りの購読者を呼ぶ。
        /// </summary>
        public event Action<BootstrapWorld> Built;

        /// <summary>
        /// <see cref="Build"/> で床を生成するか。シーン側に床 (NavMeshSurface を焼いたもの) があるときは false にする。
        /// </summary>
        public bool CreatesFloor
        {
            get => _createFloor;
            set => _createFloor = value;
        }

        /// <summary><see cref="Build"/> で作るプレイヤーの視点。生成後に変えるなら <see cref="BootstrapWorld.Rig"/> の Mode を変える。</summary>
        public ViewMode ViewMode
        {
            get => _viewMode;
            set => _viewMode = value;
        }

        /// <summary>
        /// <see cref="Build"/> で作るプレイヤーの <see cref="WeaponController.Definition"/>。
        /// Play 中に未設定なら WeaponController が初期化できず、<see cref="Build"/> は失敗する。
        /// </summary>
        public WeaponDefinition WeaponDefinition
        {
            get => _weaponDefinition;
            set => _weaponDefinition = value;
        }

        private void Awake()
        {
            try
            {
                Build();
            }
            catch (Exception e)
            {
                Debug.LogError("[Bootstrap] failed to build the scene", this);
                Debug.LogException(e, this);
                enabled = false;
            }
        }

        /// <summary>
        /// 床・プレイヤー・ライト・標的ダミー・HUD を生成する。2 回目以降は何もせず最初の結果を返す。
        /// 途中で失敗したら作りかけのオブジェクトを片付けてから例外を投げ直す。
        /// </summary>
        public BootstrapWorld Build()
        {
            if (_world != null)
            {
                return _world;
            }

            GameObject floor = null;
            GameObject player = null;
            GameObject light = null;
            GameObject targets = null;
            GameObject hud = null;
            PanelSettings hudPanelSettings = null;
            try
            {
                floor = _createFloor ? CreateFloor() : null;
                player = CreatePlayer(
                    _viewMode, _weaponDefinition, out var controller, out var rig, out var camera, out var look, out var mover, out var weapon, out var playerDamageable);
                light = CreateLight(out var lightComponent);
                targets = CreateTargetDummies(player.transform, out var dummies);
                // 専用サーバーには画面が無いので HUD を作らない。残り時間は MatchRunner が試合を始めたときに繋ぐ。
                Hud hudComponent = null;
                if (!NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
                {
                    hud = CreateHud(playerDamageable, weapon, out hudComponent, out hudPanelSettings);
                }

                _world = new BootstrapWorld(
                    floor, player, controller, camera, look, mover, lightComponent, weapon, targets, dummies, playerDamageable,
                    hudComponent, hudPanelSettings, rig);
            }
            catch
            {
                DestroySafely(hud);
                DestroySafely(hudPanelSettings);
                DestroySafely(targets);
                DestroySafely(light);
                DestroySafely(player);
                DestroySafely(floor);
                throw;
            }

            Debug.Log(ReadyLog, this);
            NotifyBuilt(_world);
            return _world;
        }

        private void NotifyBuilt(BootstrapWorld world)
        {
            var handlers = Built;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<BootstrapWorld>)handler)(world);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        private static GameObject CreateFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = FloorName;
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = FloorScale;
            return floor;
        }

        private static GameObject CreatePlayer(
            ViewMode viewMode,
            WeaponDefinition weaponDefinition,
            out CharacterController controller,
            out ViewRig rig,
            out Camera camera,
            out FirstPersonLook look,
            out PlayerController mover,
            out WeaponController weapon,
            out Damageable damageable)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                ConfigurePlayer(player, viewMode, weaponDefinition, out controller, out rig, out camera, out look, out mover, out weapon);

                // 撃たれる側の受け皿。CharacterController のカプセルをそのまま胴の当たり判定にする。
                damageable = player.AddComponent<Damageable>();
                damageable.Initialize(PlayerName, PlayerMaxHealth);
                player.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            }
            catch
            {
                DestroySafely(player);
                throw;
            }

            return player;
        }

        private static void ConfigurePlayer(
            GameObject player,
            ViewMode viewMode,
            WeaponDefinition weaponDefinition,
            out CharacterController controller,
            out ViewRig rig,
            out Camera camera,
            out FirstPersonLook look,
            out PlayerController mover,
            out WeaponController weapon)
        {
            mover = null;
            player.name = PlayerName;
            player.tag = PlayerTag;
            player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.identity);

            // CharacterController 自体がカプセルのコライダーなので、プリミティブ付属のコライダーは二重になる。
            DestroySafely(player.GetComponent<CapsuleCollider>());

            controller = player.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = PlayerRadius;
            controller.center = Vector3.zero;

            // 本体 (yaw) > 目 (pitch、射撃の始点) > カメラ (目からのオフセットは ViewRig が視点ごとに決める)
            var eye = new GameObject(EyeName).transform;
            eye.SetParent(player.transform, false);
            eye.localPosition = EyeOffset;
            eye.localRotation = Quaternion.identity;

            var cameraObject = new GameObject(CameraName);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(eye, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();

            // 視点を先に決めておくと、Play 中の AddComponent で走る OnEnable が最初からその視点でカメラを置く。
            // Edit Mode では Awake / OnEnable が走らないので、参照と見た目はここで直接整える。
            rig = player.AddComponent<ViewRig>();
            rig.Mode = viewMode;
            if (!rig.IsInitialized)
            {
                rig.Initialize(eye, camera);
            }

            // 一人称なら自分の体が視界に入らないよう影だけ落とし、三人称なら描く。
            rig.ApplyBodyVisibility(true);
            rig.UpdateCameraPosition();

            // カメラと ViewRig を先に置いておくと、Play 中の AddComponent で走る Awake が自力で pitch の軸 (目) を解決できる。
            // Awake が走らない Edit Mode (シーン生成やテスト) では入力の解決を実行時の Awake に任せる。
            look = player.AddComponent<FirstPersonLook>();
            // 専用サーバーでは FirstPersonLook が自分を止めて初期化しないので、未初期化を失敗にしない。
            if (Application.isPlaying && !look.IsInitialized && !NetStartup.IsDedicatedServerBuild)
            {
                throw new InvalidOperationException(
                    "FirstPersonLook could not resolve its camera or Look action; see the error above");
            }

            // FirstPersonLook と CharacterController を先に付けておくと、PlayerController の Awake が自力で参照を解決できる。
            mover = player.AddComponent<PlayerController>();
            if (Application.isPlaying && !mover.IsInitialized)
            {
                throw new InvalidOperationException(
                    "PlayerController could not resolve its references or Move/Sprint/Jump actions; see the error above");
            }

            // 射撃は視点の向きで撃つのでカメラに付ける。参照の解決は FirstPersonLook と同じく Play 中の Awake に任せる。
            // Awake は定義から武器を作るので、カメラを一度無効にして付け、定義を渡してから有効にする (そこで Awake が走る)。
            var cameraWasActive = cameraObject.activeSelf;
            cameraObject.SetActive(false);
            try
            {
                weapon = cameraObject.AddComponent<WeaponController>();
                weapon.Definition = weaponDefinition;
            }
            finally
            {
                cameraObject.SetActive(cameraWasActive);
            }

            if (Application.isPlaying && !weapon.IsInitialized)
            {
                throw new InvalidOperationException(
                    "WeaponController could not resolve its WeaponDefinition, camera or Attack/Reload actions; see the error above");
            }
        }

        /// <summary>index 番目 (0 始まり、左から) の標的ダミーの名前。</summary>
        public static string TargetDummyName(int index)
        {
            return TargetDummyNamePrefix + (index + 1);
        }

        /// <summary>
        /// 標的ダミーの足元の位置。プレイヤーの足元から前方へ <see cref="TargetDummyDistance"/> 進んだ所に、
        /// 横方向へ <see cref="TargetDummySpacing"/> 間隔で、プレイヤーから見て左から順に <see cref="TargetDummyCount"/> 個並べる。
        /// </summary>
        public static Vector3[] TargetDummyPositions(Vector3 playerFeet, Quaternion playerRotation)
        {
            var forward = HorizontalForward(playerRotation);
            var right = Vector3.Cross(Vector3.up, forward);

            var offsets = LineLayout.CenteredOffsets(TargetDummyCount, TargetDummySpacing);
            var positions = new Vector3[offsets.Length];
            for (var i = 0; i < offsets.Length; i++)
            {
                positions[i] = playerFeet + (forward * TargetDummyDistance) + (right * offsets[i]);
            }

            return positions;
        }

        /// <summary>向きの前方を水平面に落とした単位ベクトル。真上か真下を向いていれば +Z。</summary>
        private static Vector3 HorizontalForward(Quaternion rotation)
        {
            // 水平に取るのは、プレイヤーが傾いていても標的が宙に浮いたり埋まったりしないようにするため。
            var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        private static GameObject CreateTargetDummies(Transform player, out TargetDummy[] dummies)
        {
            var feet = player.position + (Vector3.down * (PlayerHeight / 2f));
            var positions = TargetDummyPositions(feet, player.rotation);

            // 標的はプレイヤーの方を向かせる (並べた方向の逆)。
            var facing = Quaternion.LookRotation(-HorizontalForward(player.rotation), Vector3.up);

            var root = new GameObject(TargetsName);
            try
            {
                dummies = new TargetDummy[positions.Length];
                for (var i = 0; i < positions.Length; i++)
                {
                    dummies[i] = TargetDummy.Create(
                        TargetDummyName(i), positions[i], facing, root.transform, TargetDummyMaxHealth, TargetDummyRespawnDelay);
                }
            }
            catch
            {
                DestroySafely(root);
                throw;
            }

            return root;
        }

        /// <summary>
        /// HUD 用の PanelSettings と UIDocument を作り、Resources の Hud.uxml とテーマを読み込んで <see cref="Hud"/> に源を渡す。
        /// UIDocument が有効になる前に参照をすべて設定するため、GameObject は無効で作ってから有効にする。
        /// </summary>
        private static GameObject CreateHud(
            Damageable player,
            WeaponController weapon,
            out Hud hud,
            out PanelSettings panelSettings)
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            if (layout == null)
            {
                throw new InvalidOperationException(
                    $"HUD layout 'Resources/{Hud.LayoutResourcePath}.uxml' was not found (expected under Assets/game/UI/Resources)");
            }

            var theme = Resources.Load<ThemeStyleSheet>(Hud.ThemeResourcePath);
            if (theme == null)
            {
                throw new InvalidOperationException(
                    $"HUD theme 'Resources/{Hud.ThemeResourcePath}.tss' was not found (expected under Assets/game/UI/Resources)");
            }

            panelSettings = null;
            GameObject hudObject = null;
            try
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = HudPanelSettingsName;
                panelSettings.themeStyleSheet = theme;
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.referenceResolution = HudReferenceResolution;
                panelSettings.match = 0.5f;

                hudObject = new GameObject(HudName);
                hudObject.SetActive(false);
                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;

                hud = hudObject.AddComponent<Hud>();
                hud.Initialize(player, weapon, null, panelSettings);

                // 有効にすると UIDocument が木を作り、Play 中は Hud の OnEnable が Label を探す。
                // Edit Mode では Hud の OnEnable が走らないので、木ができていればここで探す。
                hudObject.SetActive(true);
                if (!Application.isPlaying)
                {
                    hud.RebuildView();
                }
            }
            catch
            {
                DestroySafely(hudObject);
                DestroySafely(panelSettings);
                throw;
            }

            return hudObject;
        }

        private static GameObject CreateLight(out Light light)
        {
            var lightObject = new GameObject(LightName);
            lightObject.transform.rotation = Quaternion.Euler(LightEuler);
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            return lightObject;
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }

    /// <summary><see cref="GameBootstrap.Build"/> が生成したオブジェクト。</summary>
    public sealed class BootstrapWorld
    {
        /// <summary>生成した床。<see cref="GameBootstrap.CreatesFloor"/> が false なら null。</summary>
        public GameObject Floor { get; }
        public GameObject Player { get; }
        public CharacterController Controller { get; }
        public Camera Camera { get; }

        /// <summary>プレイヤーの視点 (一人称・三人称) と目。</summary>
        public ViewRig Rig { get; }

        public FirstPersonLook Look { get; }
        public PlayerController Mover { get; }
        public Light Light { get; }
        public WeaponController Weapon { get; }

        /// <summary>標的ダミーをまとめた親。</summary>
        public GameObject TargetRoot { get; }

        /// <summary>プレイヤーの前方に並べた標的ダミー。プレイヤーから見て左から順。</summary>
        public IReadOnlyList<TargetDummy> Targets { get; }

        /// <summary>プレイヤーの体力。敵の射撃はここに届く。</summary>
        public Damageable PlayerDamageable { get; }

        /// <summary>HUD。専用サーバーでは作らないので null。</summary>
        public Hud Hud { get; }

        /// <summary>HUD 用に作った PanelSettings。<see cref="Hud"/> が破棄されるときに一緒に消える。専用サーバーでは null。</summary>
        public PanelSettings HudPanelSettings { get; }

        public BootstrapWorld(
            GameObject floor,
            GameObject player,
            CharacterController controller,
            Camera camera,
            FirstPersonLook look,
            PlayerController mover,
            Light light,
            WeaponController weapon,
            GameObject targetRoot,
            IReadOnlyList<TargetDummy> targets,
            Damageable playerDamageable,
            Hud hud,
            PanelSettings hudPanelSettings,
            ViewRig rig = null)
        {
            Rig = rig;
            Hud = hud;
            HudPanelSettings = hudPanelSettings;
            Floor = floor;
            Player = player;
            Controller = controller;
            Camera = camera;
            Look = look;
            Mover = mover;
            Light = light;
            Weapon = weapon;
            TargetRoot = targetRoot;
            Targets = targets ?? throw new ArgumentNullException(nameof(targets));
            PlayerDamageable = playerDamageable;
        }
    }
}
