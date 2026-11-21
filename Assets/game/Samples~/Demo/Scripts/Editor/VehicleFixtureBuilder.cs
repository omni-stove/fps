using System;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 停めておく乗り物のほかに、試走場 (VehicleRangeSceneBuilder) のヘリとボートと馬と、その値のアセットもここで組む。
    public static class VehicleFixtureBuilder
    {
        public const string DefaultName = "ParkedVehicle";
        public const string BodyName = "Body";
        public const string DriverSeatName = "Seat_Driver";
        public const string PassengerSeatName = "Seat_Passenger";
        public const string GunnerSeatName = "Seat_Gunner";
        public const string PassengerViewName = "View";
        public const string GunName = "Gun";
        public const string MuzzleName = "Muzzle";
        public const string BarrelName = "Barrel";

        public const float PassengerArcDegrees = 180f;
        public const float GunPitchMinDegrees = -10f;
        public const float GunPitchMaxDegrees = 30f;
        public const float GunTurnSpeedDegreesPerSecond = 120f;

        public static readonly Vector3 BodySize = new Vector3(2f, 1.2f, 4f);

        public static readonly Vector3 DriverSeatLocal = new Vector3(-0.5f, 0.4f, 0.4f);
        public static readonly Vector3 PassengerSeatLocal = new Vector3(0.5f, 0.4f, 0.4f);
        public static readonly Vector3 GunnerSeatLocal = new Vector3(0f, 1.2f, -1.2f);

        public static readonly Vector3 DriverEnterLocal = new Vector3(-1.3f, 0f, 0.4f);
        public static readonly Vector3 PassengerEnterLocal = new Vector3(1.3f, 0f, 0.4f);
        public static readonly Vector3 GunnerEnterLocal = new Vector3(0f, 0f, -2.3f);

        public static readonly Vector3 LeftExitLocal = new Vector3(-2f, 0f, 0.4f);

        public static readonly Vector3 RightExitLocal = new Vector3(2f, 0f, 0.4f);

        public static readonly Vector3 RearExitLocal = new Vector3(0f, 0f, -3f);

        public static readonly Vector3 PassengerViewLocal = new Vector3(0f, 0.9f, 0f);

        public static readonly Vector3 GunLocal = new Vector3(0f, 2.2f, -0.6f);

        public static readonly Vector3 MuzzleLocal = new Vector3(0f, 0f, 1f);

        public const string LogTag = "[VehicleFixtureBuilder]";

        public const string HelicopterName = "Helicopter";
        public const string HelicopterVehicleId = "helicopter";
        public const string HelicopterFuselageName = "Fuselage";
        public const string HelicopterTailBoomName = "TailBoom";
        public const string HelicopterRotorName = "Rotor";
        public const string PilotSeatName = "Seat_Pilot";

        public const string BoatName = "Boat";
        public const string BoatVehicleId = "boat";
        public const string HullName = "Hull";

        public const float HelicopterChaseDistance = 12f;
        public const float HelicopterChaseHeight = 3f;
        public const float BoatChaseDistance = 8f;
        public const float BoatChaseHeight = 2f;

        public const float RotorSpan = 8f;

        // 胴の底を脚の上端に揃え、原点 (脚の底) を地面に置けば脚だけが地面に着くようにする。smoke と地面効果は原点からの高さで測るため。
        public static readonly Vector3 HelicopterFuselageSize = new Vector3(2f, 1.6f, 4.5f);
        public static readonly Vector3 HelicopterFuselageCenter = new Vector3(0f, 1.2f, 0f);
        public static readonly Vector3 HelicopterTailBoomSize = new Vector3(0.4f, 0.4f, 4f);
        public static readonly Vector3 HelicopterTailBoomCenter = new Vector3(0f, 1.5f, -4.2f);
        public static readonly Vector3 SkidSize = new Vector3(0.15f, 0.4f, 3.6f);
        public static readonly Vector3 LeftSkidCenter = new Vector3(-0.9f, 0.2f, 0f);
        public static readonly Vector3 RightSkidCenter = new Vector3(0.9f, 0.2f, 0f);
        public static readonly Vector3 RotorLocal = new Vector3(0f, 2.15f, 0f);

        // 重心を胴の真ん中より下に置くのは、脚で地面に立っているときや傾けて進むときに、上の重い胴へ引かれて転がり出さないようにするため。
        public static readonly Vector3 HelicopterCenterOfMass = new Vector3(0f, 1f, 0f);

        public static readonly Vector3 HelicopterPilotSeatLocal = new Vector3(-0.45f, 1.1f, 0.8f);
        public static readonly Vector3 HelicopterPassengerSeatLocal = new Vector3(0.45f, 1.1f, 0.8f);
        public static readonly Vector3 HelicopterPilotEnterLocal = new Vector3(-1.8f, 0f, 0.8f);
        public static readonly Vector3 HelicopterPassengerEnterLocal = new Vector3(1.8f, 0f, 0.8f);
        public static readonly Vector3 HelicopterLeftExitLocal = new Vector3(-2.2f, 0f, 0.8f);
        public static readonly Vector3 HelicopterRightExitLocal = new Vector3(2.2f, 0f, 0.8f);
        public static readonly Vector3 HelicopterRearExitLocal = new Vector3(0f, 0f, -7f);

        // 胴の後ろの端の下から撒くのは、撒いた直後のフレアが翼の当たりに重ならず、機体の見た目からも離れて見えるようにするため。
        public static readonly Vector3 AircraftFlareEjectLocal = new Vector3(0f, 0.6f, -4.2f);

        public const string HorseName = "Horse";
        public const string HorseVehicleId = "horse";
        public const string HorseModelName = "Model";
        public const string HorseShapeName = "Shape";
        public const string HorseTorsoName = "Torso";
        public const string HorseNeckName = "Neck";
        public const string HorseHeadName = "Head";
        public const string HorseLegNamePrefix = "Leg_";
        public const string RiderSeatName = "Seat_Rider";

        // 後ろの席は騎手の背に遮られる前だけを外し、横と後ろへ自分の武器を撃てるようにする。
        public const float HorsePassengerArcDegrees = 300f;

        // 原点を蹄の高さに置く (SimpleHorseMotor が原点の真下で接地を見るため)。脚の上端に胴の底を揃える。
        public const float HorseLegLength = 0.9f;
        public const float HorseLegThickness = 0.2f;
        public static readonly Vector3 HorseTorsoSize = new Vector3(0.7f, 0.7f, 1.8f);
        public static readonly Vector3 HorseTorsoCenter = new Vector3(0f, HorseLegLength + 0.35f, 0f);
        public static readonly Vector3 HorseNeckSize = new Vector3(0.3f, 0.8f, 0.35f);
        public static readonly Vector3 HorseNeckCenter = new Vector3(0f, 1.8f, 0.95f);
        public const float HorseNeckTiltDegrees = 30f;
        public static readonly Vector3 HorseHeadSize = new Vector3(0.28f, 0.3f, 0.6f);
        public static readonly Vector3 HorseHeadCenter = new Vector3(0f, 2.15f, 1.35f);

        public static readonly Vector3[] HorseLegFeet =
        {
            new Vector3(-0.22f, 0f, 0.7f),
            new Vector3(0.22f, 0f, 0.7f),
            new Vector3(-0.22f, 0f, -0.7f),
            new Vector3(0.22f, 0f, -0.7f),
        };

        // 当たりは形の部品ごとでなく、蹄から背までの箱 1 つにする。モデルを差し替えても、地面に立つ高さと撃たれる大きさが変わらないようにするため。
        public static readonly Vector3 HorseBodyColliderSize = new Vector3(HorseTorsoSize.x, HorseTorsoTop, HorseTorsoSize.z);
        public static readonly Vector3 HorseBodyColliderCenter = new Vector3(0f, HorseTorsoTop / 2f, 0f);

        // 座席は乗り手の足の位置なので、背より下の、胴の脇に足が垂れる高さに置く。
        public static readonly Vector3 HorseRiderSeatLocal = new Vector3(0f, 1f, 0.25f);
        public static readonly Vector3 HorsePassengerSeatLocal = new Vector3(0f, 1f, -0.5f);
        public static readonly Vector3 HorseRiderEnterLocal = new Vector3(-1.2f, 0f, 0.25f);
        public static readonly Vector3 HorsePassengerEnterLocal = new Vector3(1.2f, 0f, -0.5f);
        public static readonly Vector3 HorseLeftExitLocal = new Vector3(-1.6f, 0f, 0f);
        public static readonly Vector3 HorseRightExitLocal = new Vector3(1.6f, 0f, 0f);
        public static readonly Vector3 HorseRearExitLocal = new Vector3(0f, 0f, -2.2f);

        public static float HorseTorsoTop => HorseTorsoCenter.y + (HorseTorsoSize.y / 2f);

        public static string HelicopterDefinitionPath => ShooterKitRoot.OutputPath("Vehicles/" + HelicopterName + ".asset");

        public static string BoatDefinitionPath => ShooterKitRoot.OutputPath("Vehicles/" + BoatName + ".asset");

        public static string HorseDefinitionPath => ShooterKitRoot.OutputPath("Vehicles/" + HorseName + ".asset");

        public static VehicleSpec Spec(string gunnerWeaponId = null)
        {
            return new VehicleSpec(
                DefaultName,
                VehicleSpec.DefaultMaxHealth,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, PassengerArcDegrees),
                    new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing, VehicleCameraView.Chase),
                },
                gunnerWeaponId != null
                    ? new[]
                    {
                        new VehicleWeaponSpec(
                            gunnerWeaponId,
                            2,
                            VehicleWeaponSpec.DefaultYawMinDegrees,
                            VehicleWeaponSpec.DefaultYawMaxDegrees,
                            GunPitchMinDegrees,
                            GunPitchMaxDegrees,
                            GunTurnSpeedDegreesPerSecond),
                    }
                    : null);
        }

        public static Vehicle Create(
            Scene scene, Transform parent, string name, Vector3 position, Quaternion rotation, WeaponDefinition gunnerWeapon = null)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a loaded scene", nameof(scene));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
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

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = BodyName;
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, BodySize.y / 2f, 0f);
                body.transform.localScale = BodySize;

                var leftExit = CreatePoint(root.transform, "Exit_Left", LeftExitLocal);
                var rightExit = CreatePoint(root.transform, "Exit_Right", RightExitLocal);
                var rearExit = CreatePoint(root.transform, "Exit_Rear", RearExitLocal);

                // 子の並び順が座席の番号になるので、運転席・助手席・銃座の順に作る。
                var driver = CreateSeat(root.transform, DriverSeatName, DriverSeatLocal);
                driver.SetEnterPoints(CreatePoint(root.transform, "Enter_Driver", DriverEnterLocal));
                driver.SetExitPoints(leftExit, rightExit);

                var passenger = CreateSeat(root.transform, PassengerSeatName, PassengerSeatLocal);
                passenger.SetEnterPoints(CreatePoint(root.transform, "Enter_Passenger", PassengerEnterLocal));
                passenger.SetExitPoints(rightExit, leftExit);
                passenger.SetViewPoint(CreatePoint(passenger.transform, PassengerViewName, PassengerViewLocal));

                var gunner = CreateSeat(root.transform, GunnerSeatName, GunnerSeatLocal);
                gunner.SetEnterPoints(CreatePoint(root.transform, "Enter_Gunner", GunnerEnterLocal));

                if (gunnerWeapon != null)
                {
                    CreateGun(root.transform, gunnerWeapon);
                }

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.SetExitPoints(rearExit);
                vehicle.RefreshSeats();
                vehicle.Configure(Spec(gunnerWeapon != null ? gunnerWeapon.Id : null));
                return vehicle;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        public static VehicleSpec HelicopterVehicleSpec()
        {
            return new VehicleSpec(
                HelicopterVehicleId,
                VehicleSpec.DefaultMaxHealth,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, PassengerArcDegrees),
                },
                chaseDistance: HelicopterChaseDistance,
                chaseHeight: HelicopterChaseHeight);
        }

        public static VehicleSpec BoatVehicleSpec()
        {
            return new VehicleSpec(
                BoatVehicleId,
                VehicleSpec.DefaultMaxHealth,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                },
                chaseDistance: BoatChaseDistance,
                chaseHeight: BoatChaseHeight);
        }

        public static VehicleSpec HorseVehicleSpec()
        {
            return new VehicleSpec(
                HorseVehicleId,
                VehicleSpec.DefaultMaxHealth,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Chase, HorsePassengerArcDegrees),
                });
        }

        // VehicleSpec は値を変えられないので、Id や脱出の値だけを差し替えた写しを作る。同じ組み方の乗り物を、smoke が Id で見分けられるようにするため。
        public static VehicleSpec Respec(VehicleSpec spec, string id = null, BailOutSpec bailOut = null)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return new VehicleSpec(
                id ?? spec.Id,
                spec.MaxHealth,
                spec.Seats,
                spec.Weapons,
                spec.Parts,
                spec.EnterRange,
                spec.ChaseDistance,
                spec.ChaseHeight,
                spec.Durability,
                spec.SpeedFieldOfView,
                bailOut ?? spec.BailOut);
        }

        // ゲーム側で調整した値を残すよう、既にあれば値は変えずに検査だけする (AircraftFixtureBuilder.LoadOrBuildDefinition と同じ)。
        public static HelicopterDefinition LoadOrBuildHelicopterDefinition(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            var existing = AssetDatabase.LoadAssetAtPath<HelicopterDefinition>(path);
            if (existing != null)
            {
                existing.ToSpec();
                return existing;
            }

            var built = SceneBuildHelpers.CreateOrUpdateAsset<HelicopterDefinition>(path, ConfigureHelicopter);
            Debug.Log($"{LogTag} saved '{path}'");
            return built;
        }

        public static BoatDefinition LoadOrBuildBoatDefinition(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            var existing = AssetDatabase.LoadAssetAtPath<BoatDefinition>(path);
            if (existing != null)
            {
                existing.ToSpec();
                return existing;
            }

            var built = SceneBuildHelpers.CreateOrUpdateAsset<BoatDefinition>(path, ConfigureBoat);
            Debug.Log($"{LogTag} saved '{path}'");
            return built;
        }

        public static HorseDefinition LoadOrBuildHorseDefinition(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            var existing = AssetDatabase.LoadAssetAtPath<HorseDefinition>(path);
            if (existing != null)
            {
                existing.ToSpec();
                return existing;
            }

            var built = SceneBuildHelpers.CreateOrUpdateAsset<HorseDefinition>(path, ConfigureHorse);
            Debug.Log($"{LogTag} saved '{path}'");
            return built;
        }

        public static void ConfigureHorse(HorseDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.CopyFrom(HorseSpec.Default);
            definition.ToSpec();
        }

        public static void ConfigureHelicopter(HelicopterDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.CopyFrom(HelicopterSpec.Default);
            definition.ToSpec();
        }

        public static void ConfigureBoat(BoatDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.CopyFrom(BoatSpec.Default);
            definition.ToSpec();
        }

        // 船体の箱は浮力を測る点 (BoatSpec.HullPoint) と同じ大きさにする。箱と点がずれると、見た目より深く沈んだり浮いたりして見えるため。
        public static Vector3 HullSize(BoatSpec spec)
        {
            RequireBoatSpec(spec);
            return new Vector3(spec.Width, spec.HullHeight, spec.Length);
        }

        public static Vector3 HullCenter(BoatSpec spec)
        {
            RequireBoatSpec(spec);
            return new Vector3(0f, spec.HullBottom + (spec.HullHeight / 2f), 0f);
        }

        // 止まって浮いているときの、水面から見た船の原点の高さ。ここに置けば、始まった途端に上下へ揺れない。
        public static float RestHeightAboveSurface(BoatSpec spec)
        {
            RequireBoatSpec(spec);
            return -spec.HullBottom - spec.Draft;
        }

        public static Vector3 BoatDriverSeatLocal(BoatSpec spec)
        {
            RequireBoatSpec(spec);
            return new Vector3(0f, spec.HullBottom + spec.HullHeight - 0.3f, -spec.Length / 4f);
        }

        public static Vector3 BoatPassengerSeatLocal(BoatSpec spec)
        {
            RequireBoatSpec(spec);
            return new Vector3(0f, spec.HullBottom + spec.HullHeight - 0.3f, spec.Length / 4f);
        }

        // 乗る位置と降りる位置は船べりのすぐ外の水の中に置く。泳いで近づいた人が乗れ、降りた人は水に入る。
        public static Vector3 BoatDriverEnterLocal(BoatSpec spec)
        {
            RequireBoatSpec(spec);
            return new Vector3(-((spec.Width / 2f) + 0.6f), spec.HullBottom, -spec.Length / 4f);
        }

        public static Vector3 BoatPassengerEnterLocal(BoatSpec spec)
        {
            RequireBoatSpec(spec);
            return new Vector3((spec.Width / 2f) + 0.6f, spec.HullBottom, spec.Length / 4f);
        }

        public static Vehicle CreateHelicopter(
            Scene scene, Transform parent, string name, Vector3 position, Quaternion rotation, HelicopterDefinition definition = null)
        {
            RequireScene(scene, name);
            var flight = definition != null ? definition.ToSpec() : HelicopterSpec.Default;

            var root = new GameObject(name);
            try
            {
                Place(scene, parent, root, position, rotation);

                var body = root.AddComponent<Rigidbody>();
                body.mass = flight.Mass;
                body.automaticCenterOfMass = false;
                body.centerOfMass = HelicopterCenterOfMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                CreateBox(root.transform, HelicopterFuselageName, HelicopterFuselageCenter, HelicopterFuselageSize, true);
                CreateBox(root.transform, HelicopterTailBoomName, HelicopterTailBoomCenter, HelicopterTailBoomSize, true);
                CreateBox(root.transform, "Skid_Left", LeftSkidCenter, SkidSize, true);
                CreateBox(root.transform, "Skid_Right", RightSkidCenter, SkidSize, true);

                // ローターは見た目だけの目印なので当たりを外す。残すと、近くの壁や乗り降りする人に羽が当たって機体が弾かれるため。
                var rotor = CreatePoint(root.transform, HelicopterRotorName, RotorLocal);
                CreateBox(rotor, "Blade_A", Vector3.zero, new Vector3(RotorSpan, 0.05f, 0.3f), false);
                CreateBox(rotor, "Blade_B", Vector3.zero, new Vector3(0.3f, 0.05f, RotorSpan), false);

                var leftExit = CreatePoint(root.transform, "Exit_Left", HelicopterLeftExitLocal);
                var rightExit = CreatePoint(root.transform, "Exit_Right", HelicopterRightExitLocal);
                var rearExit = CreatePoint(root.transform, "Exit_Rear", HelicopterRearExitLocal);

                // 子の並び順が座席の番号になるので、操縦席・同乗席の順に作る。
                var pilot = CreateSeat(root.transform, PilotSeatName, HelicopterPilotSeatLocal);
                pilot.SetEnterPoints(CreatePoint(root.transform, "Enter_Pilot", HelicopterPilotEnterLocal));
                pilot.SetExitPoints(leftExit, rightExit);

                var passenger = CreateSeat(root.transform, PassengerSeatName, HelicopterPassengerSeatLocal);
                passenger.SetEnterPoints(CreatePoint(root.transform, "Enter_Passenger", HelicopterPassengerEnterLocal));
                passenger.SetExitPoints(rightExit, leftExit);
                passenger.SetViewPoint(CreatePoint(passenger.transform, PassengerViewName, PassengerViewLocal));

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.SetExitPoints(rearExit);
                vehicle.RefreshSeats();
                vehicle.Configure(HelicopterVehicleSpec());

                // 当て方 (HelicopterControlScheme) はモーターの RequireComponent で一緒に付く。
                var motor = root.AddComponent<SimpleHelicopterMotor>();
                motor.Definition = definition;
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

        public static Vehicle CreateBoat(
            Scene scene, Transform parent, string name, Vector3 position, Quaternion rotation, BoatDefinition definition = null)
        {
            RequireScene(scene, name);
            var hull = definition != null ? definition.ToSpec() : BoatSpec.Default;

            var root = new GameObject(name);
            try
            {
                Place(scene, parent, root, position, rotation);

                var body = root.AddComponent<Rigidbody>();
                body.mass = hull.Mass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                CreateBox(root.transform, HullName, HullCenter(hull), HullSize(hull), true);

                var leftExit = CreatePoint(root.transform, "Exit_Left", BoatDriverEnterLocal(hull) + new Vector3(-0.4f, 0f, 0f));
                var rightExit = CreatePoint(root.transform, "Exit_Right", BoatPassengerEnterLocal(hull) + new Vector3(0.4f, 0f, 0f));

                var driver = CreateSeat(root.transform, DriverSeatName, BoatDriverSeatLocal(hull));
                driver.SetEnterPoints(CreatePoint(root.transform, "Enter_Driver", BoatDriverEnterLocal(hull)));
                driver.SetExitPoints(leftExit, rightExit);

                var passenger = CreateSeat(root.transform, PassengerSeatName, BoatPassengerSeatLocal(hull));
                passenger.SetEnterPoints(CreatePoint(root.transform, "Enter_Passenger", BoatPassengerEnterLocal(hull)));
                passenger.SetExitPoints(rightExit, leftExit);

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.RefreshSeats();
                vehicle.Configure(BoatVehicleSpec());

                // 当て方を付けないのは、SimpleBoatMotor が車の当て方 (前後と左右) をそのまま読むため。
                root.AddComponent<SimpleBoatMotor>().Definition = definition;
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

        // model は見た目だけを差し替える。当たりと座席は model の有無で変えず、どの見た目でも同じ所に乗り降りできるようにする。
        public static Vehicle CreateHorse(
            Scene scene,
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            HorseDefinition definition = null,
            GameObject model = null)
        {
            RequireScene(scene, name);
            var gait = definition != null ? definition.ToSpec() : HorseSpec.Default;

            var root = new GameObject(name);
            try
            {
                Place(scene, parent, root, position, rotation);

                var body = root.AddComponent<Rigidbody>();
                body.mass = gait.Mass;
                // SimpleHorseMotor も Awake で掛けるが、シーンに残る値でも転ばないよう、組むときにも掛けておく。
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var collider = root.AddComponent<BoxCollider>();
                collider.center = HorseBodyColliderCenter;
                collider.size = HorseBodyColliderSize;

                var animator = model != null ? CreateHorseModel(root.transform, model) : CreateHorseShape(root.transform);

                var leftExit = CreatePoint(root.transform, "Exit_Left", HorseLeftExitLocal);
                var rightExit = CreatePoint(root.transform, "Exit_Right", HorseRightExitLocal);
                var rearExit = CreatePoint(root.transform, "Exit_Rear", HorseRearExitLocal);

                // 子の並び順が座席の番号になるので、騎手・後ろの席の順に作る。
                var rider = CreateSeat(root.transform, RiderSeatName, HorseRiderSeatLocal);
                rider.SetEnterPoints(CreatePoint(root.transform, "Enter_Rider", HorseRiderEnterLocal));
                rider.SetExitPoints(leftExit, rightExit);

                var passenger = CreateSeat(root.transform, PassengerSeatName, HorsePassengerSeatLocal);
                passenger.SetEnterPoints(CreatePoint(root.transform, "Enter_Passenger", HorsePassengerEnterLocal));
                passenger.SetExitPoints(rightExit, leftExit);

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.SetExitPoints(rearExit);
                vehicle.RefreshSeats();
                vehicle.Configure(HorseVehicleSpec());

                root.AddComponent<SimpleHorseMotor>().Definition = definition;
                root.AddComponent<HorseControlScheme>();
                root.AddComponent<HorseGaitAnimator>().Animator = animator;
                root.AddComponent<VehicleMotorLink>();
                return vehicle;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        // 飛行機にフレアと空中の脱出を持たせる。値は乗り物の部品に入れて残すので、シーンを開き直しても同じ値で動く。
        public static VehicleCountermeasures EquipAircraft(Vehicle aircraft, CountermeasureSpec countermeasures = null, BailOutSpec bailOut = null)
        {
            if (aircraft == null)
            {
                throw new ArgumentNullException(nameof(aircraft));
            }

            aircraft.Configure(Respec(aircraft.Spec, bailOut: bailOut ?? BailOutSpec.Default));

            var flares = aircraft.GetComponent<VehicleCountermeasures>();
            if (flares == null)
            {
                flares = aircraft.gameObject.AddComponent<VehicleCountermeasures>();
            }

            flares.Configure(countermeasures ?? CountermeasureSpec.Default);
            flares.Settings.EjectLocalOffset = AircraftFlareEjectLocal;
            return flares;
        }

        public static Bounds BodyBounds(Vector3 origin, Quaternion rotation)
        {
            var center = origin + (rotation * new Vector3(0f, BodySize.y / 2f, 0f));
            var corners = new Bounds(center, Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -BodySize.x / 2f : BodySize.x / 2f,
                    (i & 2) == 0 ? -BodySize.y / 2f : BodySize.y / 2f,
                    (i & 4) == 0 ? -BodySize.z / 2f : BodySize.z / 2f);
                corners.Encapsulate(center + (rotation * corner));
            }

            return corners;
        }

        // 銃の見た目は仮の乗り物の箱と同じく試すための目印なので、差し替える参照は持たない。弾を遮らないよう当たり判定は外す。
        private static VehicleWeaponMount CreateGun(Transform parent, WeaponDefinition weapon)
        {
            var gun = new GameObject(GunName).AddComponent<VehicleWeaponMount>();
            gun.transform.SetParent(parent, false);
            gun.transform.localPosition = GunLocal;
            gun.transform.localRotation = Quaternion.identity;
            gun.SetWeapon(weapon);
            gun.SetMuzzle(CreatePoint(gun.transform, MuzzleName, MuzzleLocal));

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = BarrelName;
            UnityEngine.Object.DestroyImmediate(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(gun.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0f, MuzzleLocal.z / 2f);
            barrel.transform.localScale = new Vector3(0.12f, 0.12f, MuzzleLocal.z);
            return gun;
        }

        // Prefab のアセットは繋がりを残して置き、Prefab を直せばシーンの馬にも映るようにする。
        private static Animator CreateHorseModel(Transform parent, GameObject model)
        {
            var instance = PrefabUtility.IsPartOfPrefabAsset(model)
                ? (GameObject)PrefabUtility.InstantiatePrefab(model, parent)
                : UnityEngine.Object.Instantiate(model, parent, false);
            instance.name = HorseModelName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance.GetComponentInChildren<Animator>();
        }

        // 形の部品は試すための目印なので、当たりは外して胴の箱 (CreateHorse の BoxCollider) に任せる。
        private static Animator CreateHorseShape(Transform parent)
        {
            var shape = CreatePoint(parent, HorseShapeName, Vector3.zero);
            CreateBox(shape, HorseTorsoName, HorseTorsoCenter, HorseTorsoSize, false);
            CreateBox(shape, HorseNeckName, HorseNeckCenter, HorseNeckSize, false).transform.localRotation =
                Quaternion.Euler(HorseNeckTiltDegrees, 0f, 0f);
            CreateBox(shape, HorseHeadName, HorseHeadCenter, HorseHeadSize, false);

            for (var i = 0; i < HorseLegFeet.Length; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.name = HorseLegNamePrefix + i;
                UnityEngine.Object.DestroyImmediate(leg.GetComponent<Collider>());
                leg.transform.SetParent(shape, false);
                leg.transform.localPosition = HorseLegFeet[i] + new Vector3(0f, HorseLegLength / 2f, 0f);
                leg.transform.localScale = new Vector3(HorseLegThickness, HorseLegLength / 2f, HorseLegThickness);
            }

            return null;
        }

        private static void RequireScene(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a loaded scene", nameof(scene));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
            }
        }

        private static void RequireBoatSpec(BoatSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }

        private static void Place(Scene scene, Transform parent, GameObject root, Vector3 position, Quaternion rotation)
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
        }

        // 胴や船体の箱は試すための目印なので、差し替える参照は持たない (AircraftFixtureBuilder の箱と同じ)。
        private static GameObject CreateBox(Transform parent, string name, Vector3 center, Vector3 size, bool solid)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            if (!solid)
            {
                UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            }

            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            return box;
        }

        private static VehicleSeat CreateSeat(Transform parent, string name, Vector3 localPosition)
        {
            var seat = new GameObject(name).AddComponent<VehicleSeat>();
            seat.transform.SetParent(parent, false);
            seat.transform.localPosition = localPosition;
            seat.transform.localRotation = Quaternion.identity;
            return seat;
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
