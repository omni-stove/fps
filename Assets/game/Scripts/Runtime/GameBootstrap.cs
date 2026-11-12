using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// シーンの起点。Awake で床・プレイヤー (CharacterController + 子カメラ + <see cref="FirstPersonLook"/>
    /// + <see cref="PlayerController"/> + カメラの <see cref="WeaponController"/> + 敵に撃たれる <see cref="Damageable"/> と胴の <see cref="Hitbox"/>)
    /// ・ライト・射撃確認用の的
    /// (<see cref="Damageable"/> + 頭と胴の <see cref="Hitbox"/>)・ソロの試合 (<see cref="SoloMatchRunner"/>)
    /// ・HUD (<see cref="UIDocument"/> + <see cref="PanelSettings"/> + <see cref="Hud"/>) を
    /// コードで生成する。参照はすべてここで解決するので、シーンにはこのコンポーネントを置いた GameObject だけあればよい。
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

        public const string TargetName = "Target";
        public const string TargetBodyName = "TargetBody";
        public const string TargetHeadName = "TargetHead";

        /// <summary>射撃の確認用の的の足元。プレイヤーの正面 (+Z) 10m。</summary>
        public static readonly Vector3 TargetPosition = new Vector3(0f, 0f, 10f);

        /// <summary>胴体のカプセル。高さ 1.7m (Capsule は高さ 2m なので y を 0.85 倍) で足元から立つ。</summary>
        public static readonly Vector3 TargetBodyLocalPosition = new Vector3(0f, 0.85f, 0f);
        public static readonly Vector3 TargetBodyScale = new Vector3(1f, 0.85f, 1f);

        /// <summary>頭の球。直径 0.5m で胴体のすぐ上 (1.7m〜2.2m)。正面を水平に撃つと胴体、少し上を向くと頭に当たる。</summary>
        public static readonly Vector3 TargetHeadLocalPosition = new Vector3(0f, 1.95f, 0f);
        public static readonly Vector3 TargetHeadScale = new Vector3(0.5f, 0.5f, 0.5f);

        public const string MatchName = "Match";
        public const string HudName = "HUD";
        public const string HudPanelSettingsName = "HudPanelSettings";

        /// <summary>HUD の基準解像度。これより大きい・小さい画面では幅と高さの中間で拡縮する。</summary>
        public static readonly Vector2Int HudReferenceResolution = new Vector2Int(1920, 1080);

        [SerializeField, Tooltip("床を生成するか。NavMesh を焼いた床を持つシーンでは切る")]
        private bool _createFloor = true;

        private BootstrapWorld _world;

        /// <summary>生成済みのオブジェクト。生成前や失敗時は null。</summary>
        public BootstrapWorld World => _world;

        /// <summary>
        /// <see cref="Build"/> で床を生成するか。シーン側に床 (NavMeshSurface を焼いたもの) があるときは false にする。
        /// </summary>
        public bool CreatesFloor
        {
            get => _createFloor;
            set => _createFloor = value;
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
        /// 床・プレイヤー・ライト・的・試合・HUD を生成する。2 回目以降は何もせず最初の結果を返す。
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
            GameObject target = null;
            GameObject match = null;
            GameObject hud = null;
            PanelSettings hudPanelSettings = null;
            try
            {
                floor = _createFloor ? CreateFloor() : null;
                player = CreatePlayer(out var controller, out var camera, out var look, out var mover, out var weapon, out var playerDamageable);
                light = CreateLight(out var lightComponent);
                target = CreateTarget(out var targetDamageable);
                match = CreateMatch(playerDamageable, targetDamageable, out var matchRunner);

                // 専用サーバーには画面が無いので HUD を作らない。
                Hud hudComponent = null;
                if (!NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
                {
                    hud = CreateHud(playerDamageable, weapon, matchRunner.Match, out hudComponent, out hudPanelSettings);
                }

                _world = new BootstrapWorld(
                    floor, player, controller, camera, look, mover, lightComponent, weapon, targetDamageable, playerDamageable,
                    matchRunner, hudComponent, hudPanelSettings);
            }
            catch
            {
                DestroySafely(hud);
                DestroySafely(hudPanelSettings);
                DestroySafely(match);
                DestroySafely(target);
                DestroySafely(light);
                DestroySafely(player);
                DestroySafely(floor);
                throw;
            }

            Debug.Log(ReadyLog, this);
            return _world;
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
            out CharacterController controller,
            out Camera camera,
            out FirstPersonLook look,
            out PlayerController mover,
            out WeaponController weapon,
            out Damageable damageable)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                ConfigurePlayer(player, out controller, out camera, out look, out mover, out weapon);

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
            out CharacterController controller,
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

            var cameraObject = new GameObject(CameraName);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = EyeOffset;
            cameraObject.transform.localRotation = Quaternion.identity;
            camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();

            // 自分の体が視界に入らないよう影だけ落とす。
            var renderer = player.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            // カメラを先に子に置いておくと、Play 中の AddComponent で走る Awake が自力で参照を解決できる。
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
            weapon = cameraObject.AddComponent<WeaponController>();
            if (Application.isPlaying && !weapon.IsInitialized)
            {
                throw new InvalidOperationException(
                    "WeaponController could not resolve its camera or Attack/Reload actions; see the error above");
            }
        }

        private static GameObject CreateTarget(out Damageable damageable)
        {
            var target = new GameObject(TargetName);
            try
            {
                target.transform.SetPositionAndRotation(TargetPosition, Quaternion.identity);
                damageable = target.AddComponent<Damageable>();

                CreateTargetPart(target.transform, PrimitiveType.Capsule, TargetBodyName, TargetBodyLocalPosition, TargetBodyScale, HitZone.Body, damageable);
                CreateTargetPart(target.transform, PrimitiveType.Sphere, TargetHeadName, TargetHeadLocalPosition, TargetHeadScale, HitZone.Head, damageable);
            }
            catch
            {
                DestroySafely(target);
                throw;
            }

            return target;
        }

        private static GameObject CreateMatch(Damageable player, Damageable target, out SoloMatchRunner runner)
        {
            var matchObject = new GameObject(MatchName);
            try
            {
                runner = matchObject.AddComponent<SoloMatchRunner>();
                runner.Initialize(player, target, SoloMatchRunner.DefaultTimeLimitSeconds, SoloMatchRunner.DefaultKillsToWin);
            }
            catch
            {
                DestroySafely(matchObject);
                throw;
            }

            return matchObject;
        }

        /// <summary>
        /// HUD 用の PanelSettings と UIDocument を作り、Resources の Hud.uxml とテーマを読み込んで <see cref="Hud"/> に源を渡す。
        /// UIDocument が有効になる前に参照をすべて設定するため、GameObject は無効で作ってから有効にする。
        /// </summary>
        private static GameObject CreateHud(
            Damageable player,
            WeaponController weapon,
            Match match,
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
                hud.Initialize(player, weapon, match, panelSettings);

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

        private static void CreateTargetPart(
            Transform parent,
            PrimitiveType shape,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            HitZone zone,
            Damageable owner)
        {
            // プリミティブ付属のコライダーをそのまま当たり判定に使う。
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            part.AddComponent<Hitbox>().Initialize(zone, owner);
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
        public FirstPersonLook Look { get; }
        public PlayerController Mover { get; }
        public Light Light { get; }
        public WeaponController Weapon { get; }

        /// <summary>射撃の確認用の的。</summary>
        public Damageable Target { get; }

        /// <summary>プレイヤーの体力。敵の射撃はここに届く。</summary>
        public Damageable PlayerDamageable { get; }

        /// <summary>残り時間と勝敗を持つソロの試合。</summary>
        public SoloMatchRunner Match { get; }

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
            Damageable target,
            Damageable playerDamageable,
            SoloMatchRunner match,
            Hud hud,
            PanelSettings hudPanelSettings)
        {
            Match = match;
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
            Target = target;
            PlayerDamageable = playerDamageable;
        }
    }
}
