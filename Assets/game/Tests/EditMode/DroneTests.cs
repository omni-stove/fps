using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // ドローンは原点に +Z 向きで置き、持ち主は Alpha の陣営に入れる。Edit Mode では Update が走らないので、
    // ドローンは ReconDrone.Tick、操縦は DroneControl.Step で進める。
    public sealed class DroneTests
    {
        private const string Kind = "drone";
        private const string OwnerId = "Owner";

        private static readonly GameRules _abilitiesOn = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On));

        private readonly List<Object> _created = new List<Object>();
        private DroneDeployDefinition _definition;
        private GameObject _owner;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            MinimapReveals.ResetAll();
            _now = 0f;
            MinimapReveals.Clock = () => _now;

            _definition = ScriptableObject.CreateInstance<DroneDeployDefinition>();
            _definition.name = "DeployDrone";
            _definition.Id = Kind;
            _created.Add(_definition);

            _owner = new GameObject(OwnerId);
            _owner.transform.position = new Vector3(0f, 1f, -3f);
            _owner.AddComponent<SideMember>().Side = Side.Alpha;
            _owner.AddComponent<Damageable>().Initialize(OwnerId, Damageable.DefaultMaxHealth);
            _created.Add(_owner);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var deployable in Object.FindObjectsByType<Deployable>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(deployable.gameObject);
            }

            foreach (var preview in Object.FindObjectsByType<DeployPreview>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(preview.gameObject);
            }

            // Edit Mode では DroneControl の OnDestroy が呼ばれず、操縦のカメラが残るので、ここで消す。
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (camera.name == DroneControl.ViewObjectName)
                {
                    Object.DestroyImmediate(camera.gameObject);
                }
            }

            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            GameSignals.ResetAll();
            MinimapReveals.ResetAll();
        }

        [Test]
        public void ThePlaceholderIsABodyWithRotorsThatCanBeShotAndFliesOverTheNavMesh()
        {
            LogAssert.Expect(LogType.Log, DroneLog.Armed(OwnerId, Kind, 1, DroneSpec.Default));
            var drone = Place();
            var spec = _definition.ToDeploySpec();

            Assert.That(drone.transform.Find(DroneDeployDefinition.BodyName), Is.Not.Null);
            Assert.That(drone.transform.Find(DroneDeployDefinition.RotorName + "0"), Is.Not.Null);
            Assert.That(drone.transform.Find(DroneDeployDefinition.CameraAnchorName), Is.Not.Null);
            Assert.That(drone.Damageable.TryGetBounds(out var bounds), Is.True, "it can be shot");
            Assert.That(bounds.max.y, Is.EqualTo(spec.Height).Within(1e-3f));
            Assert.That(bounds.size.x, Is.LessThanOrEqualTo(spec.Width + 1e-3f));
            Assert.That(bounds.size.z, Is.LessThanOrEqualTo(spec.Depth + 1e-3f));
            Assert.That(drone.GetComponent<NavMeshObstacle>(), Is.Null, "it flies away, so it does not carve the NavMesh");
            Assert.That(drone.GetComponentInChildren<Shooter.Runtime.CoverPoint>(), Is.Null);
            Assert.That(drone.Damageable.Health.Current, Is.EqualTo(DroneSpec.DefaultMaxHealth));
            Assert.That(spec.MaxActive, Is.EqualTo(DroneSpec.DefaultMaxActive));
            Assert.That(drone.IsArmed, Is.True);
            Assert.That(ReconDrone.Armed, Has.Member(drone));
            Assert.That(ReconDrone.TryFindFor(_owner, out var found), Is.True);
            Assert.That(found, Is.SameAs(drone));
            Assert.Throws<System.InvalidOperationException>(() => drone.Arm(DroneSpec.Default, 0.1f));
        }

        [Test]
        public void ThePrefabReplacesThePlaceholder()
        {
            var holder = new GameObject("ArtHolder");
            holder.transform.position = new Vector3(0f, -100f, 0f);
            _created.Add(holder);
            var art = GameObject.CreatePrimitive(PrimitiveType.Cube);
            art.name = "DroneArt";
            art.transform.SetParent(holder.transform, false);
            _definition.Prefab = art;

            var drone = Place();

            Assert.That(drone.transform.Find(DroneDeployDefinition.BodyName), Is.Null);
            Assert.That(drone.IsArmed, Is.True);
        }

        [Test]
        public void PilotingTakesTheBodyWeaponAndLookAndInteractHandsThemBack()
        {
            var drone = Place();
            var control = _owner.AddComponent<DroneControl>();
            var mover = new ControlHandoff();
            var weapon = new ControlHandoff();
            var look = _owner.AddComponent<FirstPersonLook>();

            LogAssert.Expect(LogType.Log, DroneLog.PilotStarted(OwnerId, Kind, 1));
            Assert.That(control.Begin(drone, new[] { mover, weapon }, look), Is.True);

            Assert.That(control.IsPiloting, Is.True);
            Assert.That(drone.IsPiloted, Is.True);
            Assert.That(mover.IsHeldBy(control), Is.True);
            Assert.That(weapon.IsHeldBy(control), Is.True);
            Assert.That(look.InputSuspended, Is.True);
            Assert.That(control.ViewCamera, Is.Not.Null);
            Assert.That(control.ViewCamera.gameObject.activeSelf, Is.True);
            Assert.That(control.ViewCamera.depth, Is.EqualTo(DroneControl.ViewDepth), "the drone's view is drawn over the player's camera");
            Assert.That(GameSignals.Drone.Value.HasValue, Is.True);
            Assert.That(GameSignals.Drone.Value.Value.Piloting, Is.True);
            Assert.That(control.Begin(drone, new[] { mover }, look), Is.False, "already piloting");

            LogAssert.Expect(LogType.Log, DroneLog.PilotEnded(OwnerId, Kind, 1, DronePilotEnd.Interact));
            control.Step(new DroneInput(Vector2.up, Vector2.zero, 0f, leave: true), 0.1f);

            Assert.That(control.IsPiloting, Is.False);
            Assert.That(drone.IsPiloted, Is.False);
            Assert.That(mover.IsHandedOff, Is.False);
            Assert.That(weapon.IsHandedOff, Is.False);
            Assert.That(look.InputSuspended, Is.False);
            Assert.That(control.ViewCamera.gameObject.activeSelf, Is.False);
            Assert.That(drone.IsArmed, Is.True, "getting out leaves the drone in the air");
            Assert.That(drone.transform.position, Is.EqualTo(Vector3.zero), "the press that gets out does not also fly");
            Assert.That(GameSignals.Drone.Value.HasValue, Is.True, "the drone is still up, so its battery is still shown");
            Assert.That(GameSignals.Drone.Value.Value.Piloting, Is.False);
        }

        [Test]
        public void TheStickFliesTheDroneTheWayItFacesAndClimbs()
        {
            _definition.Speed = 4f;
            var drone = Place(rotation: Quaternion.Euler(0f, 90f, 0f));
            var control = Pilot(drone);

            control.Step(new DroneInput(Vector2.up, Vector2.zero, 0f, false), 0.5f);
            Assert.That(Vector3.Distance(drone.transform.position, new Vector3(2f, 0f, 0f)), Is.LessThan(1e-3f), "forward is the way the drone faces (+X)");

            control.Step(new DroneInput(Vector2.zero, Vector2.zero, 1f, false), 0.25f);
            Assert.That(drone.transform.position.y, Is.EqualTo(1f).Within(1e-3f));

            control.Step(new DroneInput(Vector2.zero, new Vector2(-90f, 30f), 0f, false), 0.1f);
            Assert.That(Vector3.Angle(drone.transform.forward, Vector3.forward), Is.LessThan(0.01f), "turning left by 90 faces +Z");
            Assert.That(control.Pitch, Is.EqualTo(-30f).Within(1e-3f), "looking up tilts the view up");
            Assert.That(Vector3.Angle(control.ViewCamera.transform.forward, Quaternion.Euler(-30f, 0f, 0f) * Vector3.forward), Is.LessThan(0.01f));

            control.Step(new DroneInput(Vector2.zero, new Vector2(0f, 500f), 0f, false), 0.1f);
            Assert.That(control.Pitch, Is.EqualTo(-DroneControl.MaxPitchDegrees).Within(1e-3f));
        }

        [Test]
        public void AWallStopsTheDrone()
        {
            _definition.Speed = 6f;
            var drone = Place();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = new Vector3(0f, 0f, 3f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            _created.Add(wall);
            Physics.SyncTransforms();
            var control = Pilot(drone);

            control.Step(new DroneInput(Vector2.up, Vector2.zero, 0f, false), 1f);

            Assert.That(drone.transform.position.z, Is.LessThan(2.75f), "it stops in front of the wall's face");
            Assert.That(drone.transform.position.z, Is.GreaterThan(2.4f), "it still flies up to the wall");
        }

        [Test]
        public void ASweepRevealsAndMarksOnlyNearbyLivingEnemies()
        {
            _definition.SpotRadius = 8f;
            _definition.RevealSeconds = 4f;
            var pings = Pings();
            var near = Body("Near", new Vector3(0f, 1f, 5f), Side.Bravo);
            var far = Body("Far", new Vector3(0f, 1f, 20f), Side.Bravo);
            var ally = Body("Ally", new Vector3(3f, 1f, 0f), Side.Alpha);
            var dead = Body("Dead", new Vector3(-3f, 1f, 0f), Side.Bravo);
            dead.Kill();
            var drone = Place();
            var control = Pilot(drone);

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(DroneLog.Prefix(OwnerId, Kind, 1) + " spotted target=Near at=") + ".* marked=yes$"));
            drone.Tick(0f);

            Assert.That(MinimapReveals.RemainingSeconds(near), Is.EqualTo(4f));
            Assert.That(MinimapReveals.IsRevealed(far), Is.False);
            Assert.That(MinimapReveals.IsRevealed(ally), Is.False);
            Assert.That(MinimapReveals.IsRevealed(dead), Is.False);
            Assert.That(MinimapReveals.IsRevealed(_owner.GetComponent<Damageable>()), Is.False, "the owner is not revealed");
            Assert.That(MinimapReveals.IsRevealed(drone.Damageable), Is.False, "the drone does not spot itself");
            Assert.That(pings.IsMarked(near), Is.True);
            Assert.That(pings.IsMarked(far), Is.False);
            Assert.That(drone.HasSpotted(near), Is.True);
            Assert.That(drone.SpottedCount, Is.EqualTo(1));
            Assert.That(GameSignals.Drone.Value.Value.SpottedCount, Is.EqualTo(1));
            Assert.That(control.Published.Value.SpottedCount, Is.EqualTo(1));

            far.transform.position = new Vector3(0f, 1f, 6f);
            Physics.SyncTransforms();
            drone.Tick(0.5f);
            Assert.That(MinimapReveals.IsRevealed(far), Is.False, "nothing is looked at between sweeps");

            _now = 1f;
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(DroneLog.Prefix(OwnerId, Kind, 1) + " spotted target=Far ")));
            drone.Tick(0.5f);
            Assert.That(MinimapReveals.RemainingSeconds(far), Is.EqualTo(4f));
            Assert.That(MinimapReveals.RemainingSeconds(near), Is.EqualTo(4f), "a still spotted enemy is kept on the map");
            Assert.That(drone.SpottedCount, Is.EqualTo(2), "an enemy spotted again is not counted twice");
        }

        [Test]
        public void ASweepStillRevealsOnTheMinimapWhenThePingRuleIsOff()
        {
            var pings = Pings();
            pings.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Shooter.Core.Ping.Off)));
            var near = Body("Near", new Vector3(0f, 1f, 5f), Side.Bravo);
            var drone = Place();

            Assert.That(drone.Sweep(), Is.EqualTo(1));

            Assert.That(MinimapReveals.IsRevealed(near), Is.True);
            Assert.That(pings.Markers, Is.Empty);
        }

        [Test]
        public void ARunOutBatteryDropsTheDroneAndGivesControlBack()
        {
            _definition.BatterySeconds = 2f;
            var drone = Place();
            var deployable = drone.Deployable;
            var mover = new ControlHandoff();
            var control = _owner.AddComponent<DroneControl>();
            control.Begin(drone, new[] { mover }, null);

            drone.Tick(1f);
            Assert.That(GameSignals.Drone.Value.Value.BatteryFraction, Is.EqualTo(0.5f).Within(1e-4f));

            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Expired));
            LogAssert.Expect(LogType.Log, DroneLog.PilotEnded(OwnerId, Kind, 1, DronePilotEnd.DroneGone));
            drone.Tick(1.5f);

            Assert.That(deployable == null, Is.True);
            Assert.That(ReconDrone.Armed, Has.No.Member(drone));
            Assert.That(control.IsPiloting, Is.False);
            Assert.That(control.Drone, Is.Null);
            Assert.That(mover.IsHandedOff, Is.False);
            Assert.That(control.ViewCamera.gameObject.activeSelf, Is.False);
            Assert.That(GameSignals.Drone.Value.HasValue, Is.False, "nothing is shown once the drone is gone");
        }

        [Test]
        public void FlyingPastTheControlRangeDropsTheDrone()
        {
            _definition.Speed = 5f;
            _definition.ControlRange = 4f;
            var drone = Place();
            var deployable = drone.Deployable;
            var control = Pilot(drone);

            control.Step(new DroneInput(Vector2.up, Vector2.zero, 0f, false), 0.6f);
            drone.Tick(0.1f);
            Assert.That(GameSignals.Drone.Value.Value.RangeFraction, Is.EqualTo(0.25f).Within(1e-3f));

            control.Step(new DroneInput(Vector2.up, Vector2.zero, 0f, false), 0.4f);
            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.OutOfRange));
            LogAssert.Expect(LogType.Log, DroneLog.PilotEnded(OwnerId, Kind, 1, DronePilotEnd.DroneGone));
            drone.Tick(0.1f);

            Assert.That(deployable == null, Is.True);
            Assert.That(control.IsPiloting, Is.False);
        }

        [Test]
        public void EnemyBulletsShootItDownAndTheOwnersPassThrough()
        {
            var drone = Place();
            var deployable = drone.Deployable;
            var control = Pilot(drone);
            var aim = drone.Damageable.AimPoint;

            var ownShot = Hitscan.Fire(aim + new Vector3(0f, 0f, -3f), Vector3.forward, 6f, 50, DamageFalloff.None, _owner.transform);
            Assert.That(ownShot.Damage, Is.Zero);
            Assert.That(drone.Damageable.Health.Current, Is.EqualTo(DroneSpec.DefaultMaxHealth), "the owner's bullets go through");

            var shooter = new GameObject("Shooter");
            shooter.AddComponent<SideMember>().Side = Side.Bravo;
            shooter.transform.position = aim + new Vector3(0f, 0f, 5f);
            _created.Add(shooter);
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Destroyed));
            LogAssert.Expect(LogType.Log, DroneLog.PilotEnded(OwnerId, Kind, 1, DronePilotEnd.DroneGone));
            for (var i = 0; i < 20 && deployable != null && !deployable.IsRemoved; i++)
            {
                Hitscan.Fire(shooter.transform.position, Vector3.back, 20f, 30, DamageFalloff.None, shooter.transform);
            }

            Assert.That(deployable == null, Is.True);
            Assert.That(ReconDrone.TryFindFor(_owner, out _), Is.False);
            Assert.That(control.IsPiloting, Is.False);
            Assert.That(GameSignals.Drone.Value.HasValue, Is.False);
        }

        [Test]
        public void FallingWhilePilotingGivesControlBackAndLeavesTheDrone()
        {
            var drone = Place();
            var mover = new ControlHandoff();
            var control = _owner.AddComponent<DroneControl>();
            control.Begin(drone, new[] { mover }, null);

            LogAssert.Expect(LogType.Log, DroneLog.PilotEnded(OwnerId, Kind, 1, DronePilotEnd.Incapacitated));
            _owner.GetComponent<Damageable>().Kill();

            Assert.That(control.IsPiloting, Is.False);
            Assert.That(mover.IsHandedOff, Is.False);
            Assert.That(drone.IsArmed, Is.True);
            Assert.That(control.Begin(drone, new[] { mover }, null), Is.False, "a dead owner cannot pilot");
        }

        [Test]
        public void RecallingRemovesTheDroneAndEndsPiloting()
        {
            var drone = Place();
            var deployable = drone.Deployable;
            var control = Pilot(drone);

            LogAssert.Expect(LogType.Log, DroneLog.Recalled(OwnerId, Kind, 1, System.Numerics.Vector3.Zero));
            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Recalled));
            LogAssert.Expect(LogType.Log, DroneLog.PilotEnded(OwnerId, Kind, 1, DronePilotEnd.DroneGone));
            Assert.That(ReconDrone.RecallAll(new[] { deployable }), Is.EqualTo(1));

            Assert.That(deployable == null, Is.True);
            Assert.That(control.IsPiloting, Is.False);
            Assert.That(GameSignals.Drone.Value.HasValue, Is.False);
        }

        [Test]
        public void PlacingWithTheAbilityArmsTheDroneAndATapRecallsIt()
        {
            _definition.CooldownSeconds = 0f;
            _definition.WindupSeconds = 0f;
            _definition.RechargeSeconds = 0f;
            _definition.MaxCharges = 2;
            _definition.RecallTapSeconds = 0.25f;
            var host = Floor();

            Assert.That(host.TryUse(0), Is.True);
            LogAssert.Expect(LogType.Log, DroneLog.Armed(OwnerId, Kind, 1, DroneSpec.Default));
            LogAssert.Expect(LogType.Log, DroneLog.PilotRefused(OwnerId, Kind, 1, nameof(PlayerController)));
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(DeployLog.Prefix(OwnerId, Kind) + " placed serial=1 ") + ".* active=1/1$"));
            host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            var active = DeployOwner.GetOrAdd(_owner).Active(Kind);
            Assert.That(active.Count, Is.EqualTo(1));
            var drone = active[0].GetComponent<ReconDrone>();
            Assert.That(drone.IsArmed, Is.True);
            Assert.That(drone.Deployable.Side, Is.EqualTo(Side.Alpha));
            Assert.That(_owner.GetComponent<DroneControl>(), Is.Null, "only a player-driven owner pilots");
            Assert.That(_definition.Detonates, Is.True);
            Assert.That(_definition.DetonateTapSeconds, Is.EqualTo(0.25f));
            var charges = host.Slot(0).Charges;

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(DroneLog.Prefix(OwnerId, Kind, 1) + " recalled at=")));
            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Recalled));
            Assert.That(host.TryUse(0), Is.True);
            host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.Zero);
            Assert.That(host.Slot(0).Charges, Is.EqualTo(charges), "recalling does not use a charge");
        }

        [Test]
        public void InvalidValuesAreRejectedWhenEquipping()
        {
            var host = _owner.AddComponent<AbilityHost>();

            _definition.BatterySeconds = 0f;
            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));

            _definition.BatterySeconds = 10f;
            _definition.SpotRadius = -1f;
            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));

            _definition.SpotRadius = 5f;
            _definition.RecallTapSeconds = -1f;
            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));
            Assert.That(host.Definition(0), Is.Null);
        }

        private ReconDrone Place(int serial = 1, Vector3? position = null, Quaternion? rotation = null)
        {
            var spec = _definition.ToDeploySpec();
            var deployable = _definition.CreateDeployed(new Pose(position ?? Vector3.zero, rotation ?? Quaternion.identity), spec);
            deployable.Initialize(_owner, OwnerId, Kind, serial, Side.Alpha, spec.MaxHealth);
            var drone = deployable.GetComponent<ReconDrone>();
            drone.Arm(_definition.ToDroneSpec(), DroneDeployDefinition.CollisionRadiusOf(spec));
            Physics.SyncTransforms();
            return drone;
        }

        private DroneControl Pilot(ReconDrone drone)
        {
            var control = _owner.GetComponent<DroneControl>();
            if (control == null)
            {
                control = _owner.AddComponent<DroneControl>();
            }

            Assert.That(control.Begin(drone, new[] { new ControlHandoff() }, null), Is.True);
            return control;
        }

        private PingController Pings()
        {
            var pings = _owner.AddComponent<PingController>();
            pings.Clock = () => _now;
            pings.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Shooter.Core.Ping.On)));
            return pings;
        }

        private AbilityHost Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            _created.Add(floor);
            _owner.transform.position = new Vector3(0f, 1f, 0f);
            var host = _owner.AddComponent<AbilityHost>();
            host.Configure(_abilitiesOn);
            host.Equip(0, _definition);
            Physics.SyncTransforms();
            return host;
        }

        private Damageable Body(string name, Vector3 position, Side side)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(name, Damageable.DefaultMaxHealth);
            go.AddComponent<SideMember>().Side = side;
            _created.Add(go);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}
