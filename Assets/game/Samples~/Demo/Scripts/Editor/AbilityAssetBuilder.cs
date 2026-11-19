using System;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // 何もしない能力は、回数・回数の戻り・待ち時間・効果が出るまでの秒数がどれも見えるよう、どれも 0 でない値にする。
    // 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンは壊れない。
    public static class AbilityAssetBuilder
    {
        public const string MenuPath = "Shooter/Build Ability Assets";
        public const string LogTag = "[AbilityAssetBuilder]";

        public const string NullAbilityId = "null-ability";
        public const int NullAbilityMaxCharges = 2;
        public const float NullAbilityRechargeSeconds = 6f;
        public const float NullAbilityCooldownSeconds = 1.5f;
        public const float NullAbilityWindupSeconds = 0.25f;

        public const string ShieldAbilityId = "deploy-shield";
        public const int ShieldAbilityMaxCharges = 2;
        public const float ShieldAbilityRechargeSeconds = 15f;
        public const float ShieldAbilityCooldownSeconds = 1f;
        public const float ShieldAbilityWindupSeconds = 0f;

        public const string TurretAbilityId = "deploy-turret";
        public const int TurretAbilityMaxCharges = 1;
        public const float TurretAbilityRechargeSeconds = 30f;
        public const float TurretAbilityCooldownSeconds = 1f;
        public const float TurretAbilityWindupSeconds = 0f;

        public const string DecoyAbilityId = "deploy-decoy";
        public const int DecoyAbilityMaxCharges = 1;
        public const float DecoyAbilityRechargeSeconds = 20f;
        public const float DecoyAbilityCooldownSeconds = 1f;
        public const float DecoyAbilityWindupSeconds = 0f;

        public const string MineAbilityId = "deploy-mine";
        public const int MineAbilityMaxCharges = 2;
        public const float MineAbilityRechargeSeconds = 20f;
        public const float MineAbilityCooldownSeconds = 1f;
        public const float MineAbilityWindupSeconds = 0f;

        public const string RemoteChargeAbilityId = "deploy-remote-charge";
        public const int RemoteChargeAbilityMaxCharges = 2;
        public const float RemoteChargeAbilityRechargeSeconds = 20f;
        public const float RemoteChargeAbilityCooldownSeconds = 0.5f;
        public const float RemoteChargeAbilityWindupSeconds = 0f;

        public const string DistractionAbilityId = "distraction-throw";
        public const int DistractionMaxCharges = 3;
        public const float DistractionRechargeSeconds = 10f;
        public const float DistractionCooldownSeconds = 1f;

        public const float DistractionWindupSeconds = 0.15f;

        public const string VisionGogglesAbilityId = "vision-goggles";
        public const VisionMode VisionGogglesMode = VisionMode.Night;

        // 外すと使った 1 回が戻る (VisionGogglesAbility) ので、1 回あれば掛け外しを繰り返せる。
        public const int VisionGogglesMaxCharges = 1;
        public const float VisionGogglesRechargeSeconds = 5f;
        public const float VisionGogglesCooldownSeconds = 1f;

        // 掛ける動きの間だけ待たせる。外すときは待たない。
        public const float VisionGogglesWindupSeconds = 0.4f;

        public const string DroneAbilityId = "deploy-drone";
        public const int DroneAbilityMaxCharges = 1;
        public const float DroneAbilityRechargeSeconds = 30f;
        public const float DroneAbilityCooldownSeconds = 1f;
        public const float DroneAbilityWindupSeconds = 0f;

        public const string GrappleAbilityId = "grapple";
        public const int GrappleMaxCharges = 2;
        public const float GrappleRechargeSeconds = 8f;
        public const float GrappleCooldownSeconds = 1f;

        public const float GrappleWindupSeconds = 0.1f;

        // キットはプロジェクトのレイヤーやタグを足せないので、掴めるのは Default だけにし、
        // 掴めない面は組み込みの別のレイヤー (GrappleWallFixture.NotGrabbableLayer) に置いて試す。
        public const int GrappleGrabbableLayers = 1 << 0;

        public static string Folder => ShooterKitRoot.OutputPath("Abilities");

        public static string NullAbilityPath => Folder + "/NullAbility.asset";

        public static string ShieldAbilityPath => Folder + "/DeployShield.asset";
        public static string GrappleAbilityPath => Folder + "/GrappleAbility.asset";

        public static string TurretAbilityPath => Folder + "/DeployTurret.asset";

        public static string DecoyAbilityPath => Folder + "/DeployDecoy.asset";

        public static string MineAbilityPath => Folder + "/DeployMine.asset";

        public static string RemoteChargeAbilityPath => Folder + "/DeployRemoteCharge.asset";

        public static string DistractionAbilityPath => Folder + "/DistractionThrow.asset";

        public static string VisionGogglesAbilityPath => Folder + "/VisionGoggles.asset";

        public static string DroneAbilityPath => Folder + "/DeployDrone.asset";

        // タレットは武器のアセットを指すので、WeaponAssetBuilder.Build の後に走らせる (武器が無ければ例外で止まる)。
        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(NullAbilityPath);
            BuildShieldAt(ShieldAbilityPath);
            BuildGrappleAt(GrappleAbilityPath);
            BuildTurretAt(TurretAbilityPath);
            BuildDecoyAt(DecoyAbilityPath);
            BuildMineAt(MineAbilityPath);
            BuildRemoteChargeAt(RemoteChargeAbilityPath);
            BuildDistractionAt(DistractionAbilityPath);
            BuildVisionGogglesAt(VisionGogglesAbilityPath);
            BuildDroneAt(DroneAbilityPath);
        }

        public static VisionGogglesDefinition BuildVisionGogglesAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<VisionGogglesDefinition>(path, ConfigureVisionGoggles);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        // 手で調整した値を射撃場を作り直すたびに消さないよう、あれば中身は上書きしない。
        public static VisionGogglesDefinition LoadOrBuildVisionGogglesAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VisionGogglesDefinition>(VisionGogglesAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToMode();
                return existing;
            }

            Debug.Log($"{LogTag} '{VisionGogglesAbilityPath}' is missing; building it");
            return BuildVisionGogglesAt(VisionGogglesAbilityPath);
        }

        public static DroneDeployDefinition BuildDroneAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<DroneDeployDefinition>(path, ConfigureDrone);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        // 手で調整した値を射撃場を作り直すたびに消さないよう、あれば中身は上書きしない。
        public static DroneDeployDefinition LoadOrBuildDroneAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DroneDeployDefinition>(DroneAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToDeploySpec();
                existing.ToDroneSpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{DroneAbilityPath}' is missing; building it");
            return BuildDroneAt(DroneAbilityPath);
        }

        public static DecoyDeployDefinition BuildDecoyAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<DecoyDeployDefinition>(path, ConfigureDecoy);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        public static MineDeployDefinition BuildMineAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<MineDeployDefinition>(path, ConfigureMine);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        // 手で調整した値を射撃場を作り直すたびに消さないよう、あれば中身は上書きしない。
        public static MineDeployDefinition LoadOrBuildMineAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MineDeployDefinition>(MineAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToDeploySpec();
                existing.ToMineSpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{MineAbilityPath}' is missing; building it");
            return BuildMineAt(MineAbilityPath);
        }

        public static RemoteChargeDeployDefinition BuildRemoteChargeAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<RemoteChargeDeployDefinition>(path, ConfigureRemoteCharge);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        // 手で調整した値を射撃場を作り直すたびに消さないよう、あれば中身は上書きしない。
        public static RemoteChargeDeployDefinition LoadOrBuildRemoteChargeAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RemoteChargeDeployDefinition>(RemoteChargeAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToDeploySpec();
                existing.ToRemoteChargeSpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{RemoteChargeAbilityPath}' is missing; building it");
            return BuildRemoteChargeAt(RemoteChargeAbilityPath);
        }

        public static DistractionThrowDefinition BuildDistractionAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<DistractionThrowDefinition>(path, ConfigureDistraction);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        // 手で調整した値を射撃場を作り直すたびに消さないよう、あれば中身は上書きしない。
        public static DecoyDeployDefinition LoadOrBuildDecoyAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DecoyDeployDefinition>(DecoyAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToDeploySpec();
                existing.ToDecoySpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{DecoyAbilityPath}' is missing; building it");
            return BuildDecoyAt(DecoyAbilityPath);
        }

        // 手で調整した値を射撃場を作り直すたびに消さないよう、あれば中身は上書きしない。
        public static DistractionThrowDefinition LoadOrBuildDistractionAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DistractionThrowDefinition>(DistractionAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToDistractionSpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{DistractionAbilityPath}' is missing; building it");
            return BuildDistractionAt(DistractionAbilityPath);
        }

        public static TurretDeployDefinition BuildTurretAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            // 武器が無ければアセットを作る前に止める。
            var weapon = WeaponAssetBuilder.FindInCatalog(WeaponAssetBuilder.LoadCatalog(), TurretAbilityId, WeaponAssetBuilder.RifleId);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<TurretDeployDefinition>(path, a => ConfigureTurret(a, weapon));
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        // 手で調整した値を射撃場を作り直すたびに消さないよう、あれば中身は上書きしない。
        public static TurretDeployDefinition LoadOrBuildTurretAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TurretDeployDefinition>(TurretAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToDeploySpec();
                existing.ToTurretSpec();
                existing.ToWeaponSpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{TurretAbilityPath}' is missing; building it");
            return BuildTurretAt(TurretAbilityPath);
        }

        public static ShieldDeployDefinition BuildShieldAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<ShieldDeployDefinition>(path, ConfigureShield);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        // 手で調整した値を射撃場を作り直すたびに消さないよう、あれば中身は上書きしない。
        public static ShieldDeployDefinition LoadOrBuildShieldAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShieldDeployDefinition>(ShieldAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToDeploySpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{ShieldAbilityPath}' is missing; building it");
            return BuildShieldAt(ShieldAbilityPath);
        }

        public static NullAbilityDefinition BuildAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<NullAbilityDefinition>(path, Configure);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        public static GrappleAbilityDefinition BuildGrappleAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<GrappleAbilityDefinition>(path, ConfigureGrapple);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        public static NullAbilityDefinition LoadOrBuildNullAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<NullAbilityDefinition>(NullAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{NullAbilityPath}' is missing; building it");
            return BuildAt(NullAbilityPath);
        }

        // ゲームが中身を調整していても上書きしないよう、あれば読むだけにする。値が不正なら読んだ時点で投げる。
        public static GrappleAbilityDefinition LoadOrBuildGrappleAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GrappleAbilityDefinition>(GrappleAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToGrappleSpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{GrappleAbilityPath}' is missing; building it");
            return BuildGrappleAt(GrappleAbilityPath);
        }

        private static void Configure(NullAbilityDefinition ability)
        {
            ability.Id = NullAbilityId;
            ability.MaxCharges = NullAbilityMaxCharges;
            ability.RechargeSeconds = NullAbilityRechargeSeconds;
            ability.CooldownSeconds = NullAbilityCooldownSeconds;
            ability.WindupSeconds = NullAbilityWindupSeconds;
            ability.ToSpec();
        }

        // 届く距離や速さはキットの既定 (GrappleSpec.Default*) のまま。射撃場で変えるのは掴める面だけ。
        private static void ConfigureGrapple(GrappleAbilityDefinition ability)
        {
            ability.Id = GrappleAbilityId;
            ability.MaxCharges = GrappleMaxCharges;
            ability.RechargeSeconds = GrappleRechargeSeconds;
            ability.CooldownSeconds = GrappleCooldownSeconds;
            ability.WindupSeconds = GrappleWindupSeconds;
            ability.MinRange = GrappleSpec.DefaultMinRange;
            ability.MaxRange = GrappleSpec.DefaultMaxRange;
            ability.PullSpeed = GrappleSpec.DefaultPullSpeed;
            ability.PullAcceleration = GrappleSpec.DefaultPullAcceleration;
            ability.ArriveDistance = GrappleSpec.DefaultArriveDistance;
            ability.MaxPullSeconds = GrappleSpec.DefaultMaxPullSeconds;
            ability.HangHeight = GrappleSpec.DefaultHangHeight;
            ability.EdgeSnapDistance = GrappleSpec.DefaultEdgeSnapDistance;
            ability.HangSeconds = GrappleSpec.DefaultHangSeconds;
            ability.GrabbableLayers = GrappleGrabbableLayers;
            ability.GrabbableTags = Array.Empty<string>();
            ability.ToSpec();
            ability.ToGrappleSpec();
        }

        // 投げ方と音の届く距離はキットの既定 (DistractionSpec.Default*) のまま。見た目と音は空にして仮の球を投げる。
        private static void ConfigureDistraction(DistractionThrowDefinition ability)
        {
            ability.Id = DistractionAbilityId;
            ability.MaxCharges = DistractionMaxCharges;
            ability.RechargeSeconds = DistractionRechargeSeconds;
            ability.CooldownSeconds = DistractionCooldownSeconds;
            ability.WindupSeconds = DistractionWindupSeconds;
            ability.ThrowSpeed = DistractionSpec.DefaultThrowSpeed;
            ability.ThrowLiftSpeed = DistractionSpec.DefaultThrowLiftSpeed;
            ability.NoiseRadius = DistractionSpec.DefaultNoiseRadius;
            ability.MaxFlightSeconds = DistractionSpec.DefaultMaxFlightSeconds;
            ability.LingerSeconds = DistractionSpec.DefaultLingerSeconds;
            ability.Prefab = null;
            ability.LandingSound = null;
            ability.ToSpec();
            ability.ToDistractionSpec();
        }

        private static void ConfigureShield(ShieldDeployDefinition ability)
        {
            ability.Id = ShieldAbilityId;
            ability.MaxCharges = ShieldAbilityMaxCharges;
            ability.RechargeSeconds = ShieldAbilityRechargeSeconds;
            ability.CooldownSeconds = ShieldAbilityCooldownSeconds;
            ability.WindupSeconds = ShieldAbilityWindupSeconds;
            ability.MaxActive = Shooter.Core.DeploySpec.DefaultMaxActive;
            ability.Overflow = Shooter.Core.DeploySpec.DefaultOverflow;
            ability.MaxHealth = Shooter.Core.DeploySpec.DefaultMaxHealth;
            ability.MaxRange = Shooter.Core.DeploySpec.DefaultMaxRange;
            ability.MaxSlopeDegrees = Shooter.Core.DeploySpec.DefaultMaxSlopeDegrees;
            ability.MaxStepHeight = Shooter.Core.DeploySpec.DefaultMaxStepHeight;
            ability.Size = new Vector3(Shooter.Core.DeploySpec.DefaultWidth, Shooter.Core.DeploySpec.DefaultHeight, Shooter.Core.DeploySpec.DefaultDepth);
            ability.Prefab = null;
            ability.PreviewPrefab = null;
            ability.ToSpec();
            ability.ToDeploySpec();
        }

        private static void ConfigureTurret(TurretDeployDefinition ability, WeaponDefinition weapon)
        {
            ability.Id = TurretAbilityId;
            ability.MaxCharges = TurretAbilityMaxCharges;
            ability.RechargeSeconds = TurretAbilityRechargeSeconds;
            ability.CooldownSeconds = TurretAbilityCooldownSeconds;
            ability.WindupSeconds = TurretAbilityWindupSeconds;
            ability.MaxActive = TurretSpec.DefaultMaxActive;
            ability.Overflow = Shooter.Core.DeploySpec.DefaultOverflow;
            ability.MaxHealth = TurretSpec.DefaultMaxHealth;
            ability.MaxRange = Shooter.Core.DeploySpec.DefaultMaxRange;
            ability.MaxSlopeDegrees = Shooter.Core.DeploySpec.DefaultMaxSlopeDegrees;
            ability.MaxStepHeight = Shooter.Core.DeploySpec.DefaultMaxStepHeight;
            ability.Size = new Vector3(TurretSpec.DefaultWidth, TurretSpec.DefaultHeight, TurretSpec.DefaultDepth);
            ability.Prefab = null;
            ability.PreviewPrefab = null;
            ability.Weapon = weapon;
            ability.Rounds = TurretSpec.DefaultRounds;
            ability.LifetimeSeconds = TurretSpec.DefaultLifetimeSeconds;
            ability.SightRange = TurretSpec.DefaultSightRange;
            ability.TurnDegreesPerSecond = TurretSpec.DefaultTurnDegreesPerSecond;
            ability.AimToleranceDegrees = TurretSpec.DefaultAimToleranceDegrees;
            ability.ToSpec();
            ability.ToDeploySpec();
            ability.ToTurretSpec();
            ability.ToWeaponSpec();
        }

        // 呼び声の音はキットに無いので空のまま (敵には聞こえる)。ゲームが音を差し込む。
        private static void ConfigureDecoy(DecoyDeployDefinition ability)
        {
            ability.Id = DecoyAbilityId;
            ability.MaxCharges = DecoyAbilityMaxCharges;
            ability.RechargeSeconds = DecoyAbilityRechargeSeconds;
            ability.CooldownSeconds = DecoyAbilityCooldownSeconds;
            ability.WindupSeconds = DecoyAbilityWindupSeconds;
            ability.MaxActive = DecoySpec.DefaultMaxActive;
            ability.Overflow = Shooter.Core.DeploySpec.DefaultOverflow;
            ability.MaxHealth = DecoySpec.DefaultMaxHealth;
            ability.MaxRange = Shooter.Core.DeploySpec.DefaultMaxRange;
            ability.MaxSlopeDegrees = Shooter.Core.DeploySpec.DefaultMaxSlopeDegrees;
            ability.MaxStepHeight = Shooter.Core.DeploySpec.DefaultMaxStepHeight;
            ability.Size = new Vector3(DecoySpec.DefaultWidth, DecoySpec.DefaultHeight, DecoySpec.DefaultDepth);
            ability.Prefab = null;
            ability.PreviewPrefab = null;
            ability.LifetimeSeconds = DecoySpec.DefaultLifetimeSeconds;
            ability.CallIntervalSeconds = DecoySpec.DefaultCallIntervalSeconds;
            ability.CallRadius = DecoySpec.DefaultCallRadius;
            ability.CallSound = null;
            ability.CallVolume = 1f;
            ability.ToSpec();
            ability.ToDeploySpec();
            ability.ToDecoySpec();
        }

        // 有効になったときの音はキットに無いので空のまま。ゲームが音と見た目 (Prefab) を差し込む。
        private static void ConfigureMine(MineDeployDefinition ability)
        {
            ability.Id = MineAbilityId;
            ability.MaxCharges = MineAbilityMaxCharges;
            ability.RechargeSeconds = MineAbilityRechargeSeconds;
            ability.CooldownSeconds = MineAbilityCooldownSeconds;
            ability.WindupSeconds = MineAbilityWindupSeconds;
            ability.MaxActive = MineSpec.DefaultMaxActive;
            ability.Overflow = Shooter.Core.DeploySpec.DefaultOverflow;
            ability.MaxHealth = MineSpec.DefaultMaxHealth;
            ability.MaxRange = Shooter.Core.DeploySpec.DefaultMaxRange;
            ability.MaxSlopeDegrees = Shooter.Core.DeploySpec.DefaultMaxSlopeDegrees;
            ability.MaxStepHeight = Shooter.Core.DeploySpec.DefaultMaxStepHeight;
            ability.Size = new Vector3(MineSpec.DefaultWidth, MineSpec.DefaultHeight, MineSpec.DefaultDepth);
            ability.Prefab = null;
            ability.PreviewPrefab = null;
            ability.NameKey = MineSpec.DefaultNameKey;
            ability.ArmSeconds = MineSpec.DefaultArmSeconds;
            ability.TriggerRadius = MineSpec.DefaultTriggerRadius;
            ability.MaxDamage = MineSpec.DefaultMaxDamage;
            ability.DamageRadius = MineSpec.DefaultDamageRadius;
            ability.FullDamageRadius = MineSpec.DefaultFullDamageRadius;
            ability.StructureDamageAtCenter = MineSpec.DefaultStructureDamageAtCenter;
            ability.ArmedSound = null;
            ability.ArmedVolume = 1f;
            ability.ToSpec();
            ability.ToDeploySpec();
            ability.ToMineSpec();
        }

        // 見た目と音は差し替えのカタログ (VisionVisualCatalog) から取るので、ここで決めるのは見え方と回数だけ。
        private static void ConfigureVisionGoggles(VisionGogglesDefinition ability)
        {
            ability.Id = VisionGogglesAbilityId;
            ability.MaxCharges = VisionGogglesMaxCharges;
            ability.RechargeSeconds = VisionGogglesRechargeSeconds;
            ability.CooldownSeconds = VisionGogglesCooldownSeconds;
            ability.WindupSeconds = VisionGogglesWindupSeconds;
            ability.Mode = VisionGogglesMode;
            ability.ToSpec();
            ability.ToMode();
        }

        // 飛び方と見回し方はキットの既定 (DroneSpec.Default*) のまま。見た目 (Prefab) は空にし、胴と羽の仮の形をコードで作る。
        private static void ConfigureDrone(DroneDeployDefinition ability)
        {
            ability.Id = DroneAbilityId;
            ability.MaxCharges = DroneAbilityMaxCharges;
            ability.RechargeSeconds = DroneAbilityRechargeSeconds;
            ability.CooldownSeconds = DroneAbilityCooldownSeconds;
            ability.WindupSeconds = DroneAbilityWindupSeconds;
            ability.MaxActive = DroneSpec.DefaultMaxActive;
            ability.Overflow = Shooter.Core.DeploySpec.DefaultOverflow;
            ability.MaxHealth = DroneSpec.DefaultMaxHealth;
            ability.MaxRange = Shooter.Core.DeploySpec.DefaultMaxRange;
            ability.MaxSlopeDegrees = Shooter.Core.DeploySpec.DefaultMaxSlopeDegrees;
            ability.MaxStepHeight = Shooter.Core.DeploySpec.DefaultMaxStepHeight;
            ability.Size = new Vector3(DroneSpec.DefaultWidth, DroneSpec.DefaultHeight, DroneSpec.DefaultDepth);
            ability.Prefab = null;
            ability.PreviewPrefab = null;
            ability.NameKey = DroneSpec.DefaultNameKey;
            ability.RecallTapSeconds = DroneSpec.DefaultRecallTapSeconds;
            ability.Speed = DroneSpec.DefaultSpeed;
            ability.ControlRange = DroneSpec.DefaultControlRange;
            ability.BatterySeconds = DroneSpec.DefaultBatterySeconds;
            ability.SpotRadius = DroneSpec.DefaultSpotRadius;
            ability.SpotIntervalSeconds = DroneSpec.DefaultSpotIntervalSeconds;
            ability.RevealSeconds = DroneSpec.DefaultRevealSeconds;
            ability.ToSpec();
            ability.ToDeploySpec();
            ability.ToDroneSpec();
        }

        // 貼ったときの音はキットに無いので空のまま。ゲームが音と見た目 (Prefab) を差し込む。
        private static void ConfigureRemoteCharge(RemoteChargeDeployDefinition ability)
        {
            ability.Id = RemoteChargeAbilityId;
            ability.MaxCharges = RemoteChargeAbilityMaxCharges;
            ability.RechargeSeconds = RemoteChargeAbilityRechargeSeconds;
            ability.CooldownSeconds = RemoteChargeAbilityCooldownSeconds;
            ability.WindupSeconds = RemoteChargeAbilityWindupSeconds;
            ability.MaxActive = RemoteChargeSpec.DefaultMaxActive;
            ability.Overflow = RemoteChargeSpec.DefaultOverflow;
            ability.MaxHealth = RemoteChargeSpec.DefaultMaxHealth;
            ability.MaxRange = RemoteChargeSpec.DefaultMaxRange;
            ability.MaxSlopeDegrees = Shooter.Core.DeploySpec.DefaultMaxSlopeDegrees;
            ability.MaxStepHeight = Shooter.Core.DeploySpec.DefaultMaxStepHeight;
            ability.Size = new Vector3(RemoteChargeSpec.DefaultWidth, RemoteChargeSpec.DefaultHeight, RemoteChargeSpec.DefaultDepth);
            ability.Prefab = null;
            ability.PreviewPrefab = null;
            ability.NameKey = RemoteChargeSpec.DefaultNameKey;
            ability.TapSeconds = RemoteChargeSpec.DefaultDetonateTapSeconds;
            ability.MaxDamage = RemoteChargeSpec.DefaultMaxDamage;
            ability.DamageRadius = RemoteChargeSpec.DefaultDamageRadius;
            ability.FullDamageRadius = RemoteChargeSpec.DefaultFullDamageRadius;
            ability.StructureDamageAtCenter = RemoteChargeSpec.DefaultStructureDamageAtCenter;
            ability.AttachedSound = null;
            ability.AttachedVolume = 1f;
            ability.ToSpec();
            ability.ToDeploySpec();
            ability.ToRemoteChargeSpec();
        }
    }
}
