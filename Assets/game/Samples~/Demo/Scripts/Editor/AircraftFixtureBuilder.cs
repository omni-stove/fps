using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public static class AircraftFixtureBuilder
    {
        public const string LogTag = "[AircraftFixtureBuilder]";

        public const string DefaultName = "Aircraft";
        public const string VehicleId = "aircraft";
        public const string FuselageName = "Fuselage";
        public const string LeftWingName = "Wing_Left";
        public const string RightWingName = "Wing_Right";
        public const string TailName = "Tail";
        public const string TailplaneName = "Tailplane";
        public const string FinName = "Fin";
        public const string EngineName = "Engine";
        public const string LeftAileronName = "Aileron_Left";
        public const string RightAileronName = "Aileron_Right";
        public const string ElevatorName = "Elevator";
        public const string RudderName = "Rudder";
        public const string LeftWingTipName = "WingTip_Left";
        public const string RightWingTipName = "WingTip_Right";
        public const string PilotSeatName = "Seat_Pilot";

        public const string EngineSound = "AircraftEngine";

        public const float ChaseDistance = 14f;
        public const float ChaseHeight = 4f;

        // 部位の倍率を 1 にするのは、どこを撃っても機体の体力の減り方は変えず、違いを部位の壊れ方 (AircraftPartDamage) だけに出すため。
        public const float PartDamageMultiplier = 1f;

        public const float WheelRadius = 0.4f;
        public const float WheelMass = 40f;
        public const float SuspensionDistance = 0.3f;

        // 既定のばねは車の重さ向けで、飛行機の重さでは縮み切って胴が地面に着くので、重さに合わせて固くする。
        public const float SuspensionSpring = 200000f;
        public const float SuspensionDamper = 20000f;

        public const float WingSpan = 9f;

        public static readonly Vector3 FuselageSize = new Vector3(1.2f, 1.2f, 8f);
        public static readonly Vector3 FuselageCenter = new Vector3(0f, 1.3f, 0f);

        // 左右の翼を胴の脇で分けるのは、片翼だけを撃ち抜けるよう、翼ごとに別の部位 (VehiclePart) を持たせるため。
        public static readonly Vector3 WingSize = new Vector3((WingSpan - 1.2f) / 2f, 0.2f, 2f);
        public static readonly Vector3 LeftWingCenter = new Vector3(-(0.6f + ((WingSpan - 1.2f) / 4f)), 1.3f, 0.3f);
        public static readonly Vector3 RightWingCenter = new Vector3(0.6f + ((WingSpan - 1.2f) / 4f), 1.3f, 0.3f);
        public static readonly Vector3 TailplaneSize = new Vector3(3f, 0.15f, 1f);
        public static readonly Vector3 TailplaneCenter = new Vector3(0f, 1.6f, -3.5f);
        public static readonly Vector3 FinSize = new Vector3(0.2f, 1.4f, 1.2f);
        public static readonly Vector3 FinCenter = new Vector3(0f, 2.5f, -3.4f);

        // 胴より一回り太く、機首より少し前へ出すのは、機首へ当たった弾が胴でなくエンジンの部位に入るようにするため。
        public static readonly Vector3 EngineSize = new Vector3(1.3f, 1.3f, 1f);
        public static readonly Vector3 EngineCenter = new Vector3(0f, 1.3f, 3.6f);

        // 舵面は蝶番 (翼の後ろの縁) を原点に置き、板をその後ろへずらす。原点で回せば板が縁を軸に振れる。
        public static readonly Vector3 AileronSize = new Vector3(1.8f, 0.08f, 0.4f);
        public static readonly Vector3 LeftAileronHinge = new Vector3(-3.3f, 1.3f, -0.7f);
        public static readonly Vector3 RightAileronHinge = new Vector3(3.3f, 1.3f, -0.7f);
        public static readonly Vector3 ElevatorSize = new Vector3(3f, 0.08f, 0.4f);
        public static readonly Vector3 ElevatorHinge = new Vector3(0f, 1.6f, -4f);
        public static readonly Vector3 RudderSize = new Vector3(0.08f, 1.4f, 0.4f);
        public static readonly Vector3 RudderHinge = new Vector3(0f, 2.5f, -4f);

        public static readonly Vector3 LeftWingTip = new Vector3(-4.5f, 1.3f, 0.3f);
        public static readonly Vector3 RightWingTip = new Vector3(4.5f, 1.3f, 0.3f);

        // 重心を翼の位置に置くのは、機首と尾のどちらかへ傾いて、舵を当てなくても回り出さないようにするため。
        public static readonly Vector3 CenterOfMass = new Vector3(0f, 1.2f, 0.3f);

        public static readonly Vector3 NoseWheel = new Vector3(0f, 0.6f, 3f);
        public static readonly Vector3 LeftMainWheel = new Vector3(-1.2f, 0.6f, -0.5f);
        public static readonly Vector3 RightMainWheel = new Vector3(1.2f, 0.6f, -0.5f);

        public static readonly Vector3 PilotSeatLocal = new Vector3(0f, 1.6f, 1.6f);
        public static readonly Vector3 PilotEnterLocal = new Vector3(-1.5f, 0f, 3f);
        public static readonly Vector3 LeftExitLocal = new Vector3(-2f, 0f, 3.2f);
        public static readonly Vector3 RightExitLocal = new Vector3(2f, 0f, 3.2f);
        public static readonly Vector3 RearExitLocal = new Vector3(0f, 0f, -6f);

        public static string DefinitionPath => ShooterKitRoot.OutputPath("Vehicles/" + DefaultName + ".asset");

        public static string EngineSoundPath => PresentationAssetBuilder.AudioFolder + "/" + EngineSound + ".wav";

        // 音程の成分は 1 秒にちょうど収まる周波数にし、ループの継ぎ目で波形がずれないようにする。
        public static SoundSpec EngineSoundSpec()
        {
            return new SoundSpec(EngineSound, 4101u, 1f, 0.5f, new[]
            {
                new SoundLayer(0f, 1f, 0f, 1000f, 0.6f, 700f, 0.25f, 90f, 90f, ToneWaveform.Saw),
                new SoundLayer(0f, 1f, 0f, 1000f, 0f, 0f, 0.12f, 180f, 180f),
            });
        }

        public static VehicleSpec Spec(AircraftDamageSpec damage = null)
        {
            var parts = new List<VehiclePartSpec>();
            foreach (var part in (damage ?? AircraftDamageSpec.Default).Parts)
            {
                parts.Add(new VehiclePartSpec(part.Id, PartDamageMultiplier));
            }

            return new VehicleSpec(
                VehicleId,
                VehicleSpec.DefaultMaxHealth,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase) },
                parts: parts,
                chaseDistance: ChaseDistance,
                chaseHeight: ChaseHeight);
        }

        // ゲーム側で調整した値を残すよう、既にあれば値は変えずに検査だけする (TraversalFeatureSceneBuilder.LoadOrBuildMotorSettings と同じ)。
        public static AircraftDefinition LoadOrBuildDefinition(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            var existing = AssetDatabase.LoadAssetAtPath<AircraftDefinition>(path);
            if (existing != null)
            {
                Validate(existing);
                return existing;
            }

            var built = SceneBuildHelpers.CreateOrUpdateAsset<AircraftDefinition>(path, Configure);
            Debug.Log($"{LogTag} saved '{path}'");
            return built;
        }

        public static void Configure(AircraftDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.CopyFrom(AircraftSpec.Default);
            definition.Damage.CopyFrom(AircraftDamageSpec.Default);
            Validate(definition);
        }

        // 部品が値を読むのは遊んでいる最中なので、誤ったアセットを配線しないよう、組む前に全部の欄を読んでみる。
        public static void Validate(AircraftDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.ToSpec();
            definition.ToAimSpec();
            definition.ToStickSpec();
            definition.ToGroundSpec();
            definition.ToDamageSpec();
            definition.ToEffectsSpec();
            definition.ToWarningSpec();
        }

        public static AudioClip BuildEngineSound(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path, ".wav");
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            return PresentationAssetBuilder.WriteSound(path, EngineSoundSpec());
        }

        public static Vehicle Create(
            Scene scene,
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            AircraftDefinition definition = null,
            AudioClip engineClip = null)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a loaded scene", nameof(scene));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
            }

            AircraftDamageSpec damage;
            if (definition != null)
            {
                Validate(definition);
                damage = definition.ToDamageSpec();
            }
            else
            {
                damage = AircraftDamageSpec.Default;
            }

            var root = new GameObject(name);
            try
            {
                if (parent != null)
                {
                    root.transform.SetParent(parent, false);
                }
                else
                {
                    SceneManager.MoveGameObjectToScene(root, scene);
                }

                root.transform.SetPositionAndRotation(position, rotation);

                // WheelCollider は親の Rigidbody に付くので、車輪より先に作る。
                var body = root.AddComponent<Rigidbody>();
                body.mass = definition != null ? definition.Mass : AircraftSpec.DefaultMass;
                body.automaticCenterOfMass = false;
                body.centerOfMass = CenterOfMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // 部位は Vehicle.Configure が集めて当たりの倍率を入れるので、Vehicle より先に作る。
                CreateBox(root.transform, FuselageName, FuselageCenter, FuselageSize);
                MarkPart(CreateBox(root.transform, LeftWingName, LeftWingCenter, WingSize), damage, AircraftDamageEffect.LeftLift);
                MarkPart(CreateBox(root.transform, RightWingName, RightWingCenter, WingSize), damage, AircraftDamageEffect.RightLift);
                var tail = CreatePoint(root.transform, TailName, Vector3.zero);
                CreateBox(tail, TailplaneName, TailplaneCenter, TailplaneSize);
                CreateBox(tail, FinName, FinCenter, FinSize);
                MarkPart(tail.gameObject, damage, AircraftDamageEffect.Control);
                MarkPart(CreateBox(root.transform, EngineName, EngineCenter, EngineSize), damage, AircraftDamageEffect.Thrust);

                var surfaces = new[]
                {
                    // 左の補助翼を逆向きにするのは、右へ傾ける入力で右の後ろの縁が上がり、左の縁が下がるようにするため。
                    new AircraftControlSurface(
                        CreateSurface(root.transform, LeftAileronName, LeftAileronHinge, AileronSize),
                        AircraftControlSurfaceRole.Aileron,
                        Vector3.right,
                        reversed: true),
                    new AircraftControlSurface(
                        CreateSurface(root.transform, RightAileronName, RightAileronHinge, AileronSize),
                        AircraftControlSurfaceRole.Aileron,
                        Vector3.right),
                    new AircraftControlSurface(
                        CreateSurface(root.transform, ElevatorName, ElevatorHinge, ElevatorSize),
                        AircraftControlSurfaceRole.Elevator,
                        Vector3.right),

                    // 軸を下向きにするのは、右へ向ける入力で方向舵の後ろの縁が右へ振れるようにするため。
                    new AircraftControlSurface(
                        CreateSurface(root.transform, RudderName, RudderHinge, RudderSize),
                        AircraftControlSurfaceRole.Rudder,
                        Vector3.down),
                };
                var wingTips = new[]
                {
                    CreatePoint(root.transform, LeftWingTipName, LeftWingTip),
                    CreatePoint(root.transform, RightWingTipName, RightWingTip),
                };

                CreateWheel(root.transform, "Wheel_Nose", NoseWheel);
                CreateWheel(root.transform, "Wheel_Left", LeftMainWheel);
                CreateWheel(root.transform, "Wheel_Right", RightMainWheel);

                var leftExit = CreatePoint(root.transform, "Exit_Left", LeftExitLocal);
                var rightExit = CreatePoint(root.transform, "Exit_Right", RightExitLocal);
                var rearExit = CreatePoint(root.transform, "Exit_Rear", RearExitLocal);

                var pilot = new GameObject(PilotSeatName).AddComponent<VehicleSeat>();
                pilot.transform.SetParent(root.transform, false);
                pilot.transform.localPosition = PilotSeatLocal;
                pilot.transform.localRotation = Quaternion.identity;
                pilot.SetEnterPoints(CreatePoint(root.transform, "Enter_Pilot", PilotEnterLocal));
                pilot.SetExitPoints(leftExit, rightExit);

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.SetExitPoints(rearExit);
                vehicle.RefreshSeats();
                vehicle.Configure(Spec(damage));

                var motor = root.AddComponent<SimpleAircraftMotor>();
                motor.Definition = definition;
                root.AddComponent<AircraftControlScheme>().Definition = definition;

                var wheels = root.AddComponent<AircraftWheels>();
                wheels.Definition = definition;
                wheels.CollectWheels();

                var partDamage = root.AddComponent<AircraftPartDamage>();
                partDamage.Definition = definition;
                partDamage.Attach(vehicle);

                // 演出の Definition を空にするのは、機体のアセットを差し替えたときに、モーターの欄だけ替えれば演出の値も付いてくるようにするため。
                var effects = root.AddComponent<AircraftEffects>();
                effects.Bind(motor);
                effects.SetSurfaces(surfaces);
                effects.SetVaporAnchors(wingTips);
                effects.EngineClip = engineClip;

                root.AddComponent<AircraftStatusPublisher>();
                root.AddComponent<VehicleMotorLink>();
                root.AddComponent<VehicleImpactDamage>().Configure(VehicleImpactSpec.Default);
                return vehicle;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        // 部位を名前でなく効き先で引くのは、ゲームがアセットで部位の Id を付け替えても、同じ箱に同じ役割の部位が付くようにするため。
        public static AircraftDamagePartSpec PartFor(AircraftDamageSpec damage, AircraftDamageEffect effect)
        {
            if (damage == null)
            {
                throw new ArgumentNullException(nameof(damage));
            }

            foreach (var part in damage.Parts)
            {
                if (part.Effect == effect)
                {
                    return part;
                }
            }

            return null;
        }

        // 効き先の部位がアセットに無くても例外にしないのは、部位を減らした機体のアセットでも組め、その箱への命中を胴 (Vehicle の Hitbox) に受けさせるため。
        private static void MarkPart(GameObject target, AircraftDamageSpec damage, AircraftDamageEffect effect)
        {
            var part = PartFor(damage, effect);
            if (part == null)
            {
                return;
            }

            target.AddComponent<VehiclePart>().PartId = part.Id;
        }

        // 胴・翼・尾の箱は試すための目印なので、差し替える参照は持たない。当たり判定は機体の当たりとして残す。
        private static GameObject CreateBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            return box;
        }

        // 舵面は見た目だけの目印なので、当たり判定を外す。残すと回るたびに機体の当たりの形が変わり、翼の部位への命中を遮るため。
        private static Transform CreateSurface(Transform parent, string name, Vector3 hinge, Vector3 size)
        {
            var pivot = CreatePoint(parent, name, hinge);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = name + "_Plate";
            UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.SetParent(pivot, false);
            plate.transform.localPosition = new Vector3(0f, 0f, -size.z / 2f);
            plate.transform.localScale = size;
            return pivot;
        }

        // 車輪は回さず転がすだけなので、見た目は付けない。押す力は SimpleAircraftMotor が機体へ掛ける。
        private static WheelCollider CreateWheel(Transform parent, string name, Vector3 localPosition)
        {
            var wheel = new GameObject(name);
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPosition;
            var collider = wheel.AddComponent<WheelCollider>();
            collider.radius = WheelRadius;
            collider.mass = WheelMass;
            collider.suspensionDistance = SuspensionDistance;
            collider.suspensionSpring = new JointSpring { spring = SuspensionSpring, damper = SuspensionDamper, targetPosition = 0.5f };
            return collider;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 localPosition)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            point.localRotation = Quaternion.identity;
            return point;
        }
    }
}
