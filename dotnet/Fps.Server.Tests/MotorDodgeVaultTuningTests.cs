using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    /// <summary>
    /// 回避の待ち時間と乗り越えの速さ・届く距離・角度・最低の高さ・奥行きが、<see cref="MotorSpec"/> から
    /// ルールのファイル (<see cref="NetMatchFile"/>)・承認の電文 (<see cref="ConnectAcceptedMessage"/>)・サーバーとクライアントが共に使う
    /// <see cref="CharacterMotorSimulation"/> まで届くこと。
    /// </summary>
    public sealed class MotorDodgeVaultTuningTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;
        private const int _settleTicks = 30;
        private const float _radius = 0.5f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, _radius);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void DefaultsAreTheFormerConstants()
        {
            var spec = new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f);

            Assert.That(spec.DodgeCooldownSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.VaultSpeed, Is.EqualTo(6f));
            Assert.That(spec.VaultReach, Is.EqualTo(1f));
            Assert.That(spec.VaultMaxAngleDegrees, Is.EqualTo(45f));
            Assert.That(spec.VaultMinHeight, Is.EqualTo(0.3f));
            Assert.That(spec.VaultMaxDepth, Is.EqualTo(1f));
            Assert.That(MotorSpec.Default.DodgeCooldownSeconds, Is.EqualTo(MotorSpec.DefaultDodgeCooldownSeconds));
            Assert.That(MotorSpec.Default.VaultSpeed, Is.EqualTo(MotorSpec.DefaultVaultSpeed));
        }

        [TestCase(-0.1f, 6f, 1f, 45f, 0.3f, 1f, "dodgeCooldownSeconds")]
        [TestCase(float.PositiveInfinity, 6f, 1f, 45f, 0.3f, 1f, "dodgeCooldownSeconds")]
        [TestCase(0.5f, 0f, 1f, 45f, 0.3f, 1f, "vaultSpeed")]
        [TestCase(0.5f, float.NaN, 1f, 45f, 0.3f, 1f, "vaultSpeed")]
        [TestCase(0.5f, 6f, -1f, 45f, 0.3f, 1f, "vaultReach")]
        [TestCase(0.5f, 6f, 1f, -1f, 0.3f, 1f, "vaultMaxAngleDegrees")]
        [TestCase(0.5f, 6f, 1f, 181f, 0.3f, 1f, "vaultMaxAngleDegrees")]
        [TestCase(0.5f, 6f, 1f, 45f, -0.1f, 1f, "vaultMinHeight")]
        [TestCase(0.5f, 6f, 1f, 45f, 0.3f, float.PositiveInfinity, "vaultMaxDepth")]
        public void OutOfRangeValuesNameTheParameter(
            float cooldown, float speed, float reach, float angle, float minHeight, float depth, string name)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Spec(cooldown, speed, reach, angle, minHeight, depth));

            Assert.That(e.ParamName, Is.EqualTo(name));
        }

        [Test]
        public void TheRulesFileReadsBackTheValues()
        {
            var motor = new NetMotorSettings(Spec(2f, 3f, 1.4f, 30f, 0.5f, 0.6f), NetMotorSettings.Default.Stance, CapsuleMoverSpec.Default);
            var text = NetMatchFile.Write(FileWith(motor));

            var read = NetMatchFile.Parse(text, "motor.json").Motor.Motor;

            Assert.That(text, Does.Contain("\"dodgeCooldownSeconds\": 2"));
            Assert.That(text, Does.Contain("\"vaultSpeed\": 3"));
            Assert.That(read.DodgeCooldownSeconds, Is.EqualTo(2f));
            Assert.That(read.VaultSpeed, Is.EqualTo(3f));
            Assert.That(read.VaultReach, Is.EqualTo(1.4f));
            Assert.That(read.VaultMaxAngleDegrees, Is.EqualTo(30f));
            Assert.That(read.VaultMinHeight, Is.EqualTo(0.5f));
            Assert.That(read.VaultMaxDepth, Is.EqualTo(0.6f));
        }

        [Test]
        public void TheRulesFileDefaultsTheValuesLeftOut()
        {
            var text = "{\"motor\": {\"walkSpeed\": 6}, \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + TestRules.HandgunId
                + "\"]}, \"weapons\": [{\"id\": \"" + TestRules.HandgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\","
                + " \"magazineSize\": 12, \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";

            var read = NetMatchFile.Parse(text, "partial.json").Motor.Motor;

            Assert.That(read.WalkSpeed, Is.EqualTo(6f));
            Assert.That(read.DodgeCooldownSeconds, Is.EqualTo(MotorSpec.DefaultDodgeCooldownSeconds));
            Assert.That(read.VaultSpeed, Is.EqualTo(MotorSpec.DefaultVaultSpeed));
            Assert.That(read.VaultReach, Is.EqualTo(MotorSpec.DefaultVaultReach));
            Assert.That(read.VaultMaxAngleDegrees, Is.EqualTo(MotorSpec.DefaultVaultMaxAngleDegrees));
            Assert.That(read.VaultMinHeight, Is.EqualTo(MotorSpec.DefaultVaultMinHeight));
            Assert.That(read.VaultMaxDepth, Is.EqualTo(MotorSpec.DefaultVaultMaxDepth));
        }

        [TestCase("{\"motor\": {\"dodgeCooldownSeconds\": -1}}", "motor.dodgeCooldownSeconds")]
        [TestCase("{\"motor\": {\"vaultSpeed\": 0}}", "motor.vaultSpeed")]
        [TestCase("{\"motor\": {\"vaultReach\": -1}}", "motor.vaultReach")]
        [TestCase("{\"motor\": {\"vaultMaxAngleDegrees\": 200}}", "motor.vaultMaxAngleDegrees")]
        [TestCase("{\"motor\": {\"vaultMinHeight\": -1}}", "motor.vaultMinHeight")]
        [TestCase("{\"motor\": {\"vaultMaxDepth\": -1}}", "motor.vaultMaxDepth")]
        public void BadValuesInTheRulesFileNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void ConnectAcceptedCarriesTheValues()
        {
            var motor = new NetMotorSettings(Spec(2f, 3f, 1.4f, 30f, 0.5f, 0.6f), NetMotorSettings.Default.Stance, CapsuleMoverSpec.Default);
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100, Motor = motor });

            var got = (ConnectAcceptedMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment());

            Assert.That(got.Motor.Motor.DodgeCooldownSeconds, Is.EqualTo(2f));
            Assert.That(got.Motor.Motor.VaultSpeed, Is.EqualTo(3f));
            Assert.That(got.Motor.Motor.VaultReach, Is.EqualTo(1.4f));
            Assert.That(got.Motor.Motor.VaultMaxAngleDegrees, Is.EqualTo(30f));
            Assert.That(got.Motor.Motor.VaultMinHeight, Is.EqualTo(0.5f));
            Assert.That(got.Motor.Motor.VaultMaxDepth, Is.EqualTo(0.6f));
            Assert.That(got.Motor.Motor.MantleSpeed, Is.EqualTo(MotorSpec.DefaultMantleSpeed), "the values after the new ones still line up");
            Assert.That(got.Motor.Prone.DiveSeconds, Is.EqualTo(ProneSpec.DefaultDiveSeconds));
        }

        [Test]
        public void ConnectAcceptedWithANonPositiveVaultSpeedIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100 });
            var bytes = writer.AsSegment().ToArray();

            // 落ちる高さ (4 バイト) の前に、伏せ 6・カプセル 3 + 層 1・寸法 6 個、持ち物の値、向き 6 個、移動の値 19 個が並ぶ。乗り越えの速さは移動の 15 番目
            var carryLoadBytes = 1 + 1 + (3 * 4) + 1;
            var motorStart = bytes.Length - 4 - ((19 + 6 + 6 + 3 + 1 + 6) * 4) - carryLoadBytes;
            var vaultSpeed = motorStart + (14 * 4);
            Assert.That(BitConverter.ToSingle(bytes, vaultSpeed), Is.EqualTo(MotorSpec.DefaultVaultSpeed), "the offset points at the vault speed");
            BitConverter.TryWriteBytes(new Span<byte>(bytes, vaultSpeed, 4), 0f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void TheServerMovementUsesTheValuesOfTheRulesFile()
        {
            var motor = new NetMotorSettings(Spec(2f, 3f, 1.4f, 30f, 0.5f, 0.6f), NetMotorSettings.Default.Stance, CapsuleMoverSpec.Default);
            var data = new ServerRulesData(
                "TeamDeathmatch", NetMatchFile.Parse(NetMatchFile.Write(FileWith(motor)), "rules.json"),
                PingSpec.Default, "memory", null);

            var settings = ServerModules.MotorSettingsFrom(data);

            Assert.That(settings.Motor.DodgeCooldownSeconds, Is.EqualTo(2f));
            Assert.That(settings.Motor.VaultSpeed, Is.EqualTo(3f));
            Assert.That(settings.Motor.VaultReach, Is.EqualTo(1.4f));
        }

        [Test]
        public void TheDodgeWaitsForTheConfiguredCooldown()
        {
            AddFloor();
            var simulation = NewSimulation(Spec(cooldown: 2f));
            var state = Settle(simulation);

            state = simulation.Step(state, Input(new Vector2(1f, 0f), dodge: true));
            Assert.That(state.IsDodging, Is.True);
            while (state.IsDodging)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            Assert.That(state.Dodge.CooldownRemaining, Is.EqualTo(2f).Within(_tick));

            // 1.9 秒待ってもまだ転がれない
            for (var i = 0; i < 114; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            state = simulation.Step(state, Input(new Vector2(1f, 0f), dodge: true));
            Assert.That(state.IsDodging, Is.False, "1.9 s is shorter than the 2 s cooldown");

            for (var i = 0; i < 12; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            state = simulation.Step(state, Input(new Vector2(1f, 0f), dodge: true));
            Assert.That(state.IsDodging, Is.True, "after 2 s the next roll starts");
        }

        [Test]
        public void TheVaultFollowsThePathAtTheConfiguredSpeed()
        {
            var normal = VaultTicks(Spec());
            var slow = VaultTicks(Spec(speed: 3f));

            Assert.That(slow, Is.EqualTo(normal * 2).Within(2), "half the speed takes twice as long");
        }

        [Test]
        public void AWallBeyondTheDefaultReachIsVaultedWithALongerReach()
        {
            AddFloor();
            AddBox(new Vector3(0f, 0.4f, 1.4f), new Vector3(2f, 0.4f, 0.2f));

            Assert.That(TryVault(Spec()).Verdict, Is.EqualTo(VaultVerdict.NoObstacle), "the face 1.2 m away is out of the default 1 m reach");
            Assert.That(TryVault(Spec(reach: 1.5f)).Verdict, Is.EqualTo(VaultVerdict.Vault));
        }

        [Test]
        public void TheProbeUsesTheConfiguredAngleMinHeightAndDepth()
        {
            var sample = new VaultObstacleSample(0.8f, -Vector3.UnitZ, 0.4f, 0.8f);
            var turned = new Vector3(MathF.Sin(0.7f), 0f, MathF.Cos(0.7f));

            Assert.That(Evaluate(sample, Vector3.UnitZ, Spec()).Verdict, Is.EqualTo(VaultVerdict.Vault));
            Assert.That(Evaluate(sample, turned, Spec()).Verdict, Is.EqualTo(VaultVerdict.Vault));
            Assert.That(Evaluate(sample, turned, Spec(angle: 30f)).Verdict, Is.EqualTo(VaultVerdict.NotFacing));
            Assert.That(Evaluate(sample, Vector3.UnitZ, Spec(minHeight: 0.5f)).Verdict, Is.EqualTo(VaultVerdict.TooLow));
            Assert.That(Evaluate(sample, Vector3.UnitZ, Spec(depth: 0.6f)).Verdict, Is.EqualTo(VaultVerdict.TooDeep));
            Assert.That(Evaluate(sample, Vector3.UnitZ, Spec(speed: 3f)).Path.Speed, Is.EqualTo(3f));
        }

        [Test]
        public void ARestoredVaultKeepsTheConfiguredSpeed()
        {
            var state = CharacterVaultState.FromPoints(new Vector3(0f, 1f, 0.2f), new Vector3(0f, 1f, 1.5f), new Vector3(0f, 0f, 1.5f), 0f);

            Assert.That(state.ToPath(3f).Speed, Is.EqualTo(3f));
            Assert.That(state.ToPath(MotorSpec.DefaultVaultSpeed).Duration, Is.EqualTo(state.ToPath(3f).Duration / 2f).Within(1e-5f));
        }

        private static MotorSpec Spec(
            float cooldown = MotorSpec.DefaultDodgeCooldownSeconds,
            float speed = MotorSpec.DefaultVaultSpeed,
            float reach = MotorSpec.DefaultVaultReach,
            float angle = MotorSpec.DefaultVaultMaxAngleDegrees,
            float minHeight = MotorSpec.DefaultVaultMinHeight,
            float depth = MotorSpec.DefaultVaultMaxDepth)
        {
            return new MotorSpec(
                MotorSpec.DefaultWalkSpeed,
                MotorSpec.DefaultSprintSpeed,
                MotorSpec.DefaultJumpHeight,
                MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl,
                MotorSpec.DefaultCrouchSpeed,
                dodgeCooldownSeconds: cooldown,
                vaultSpeed: speed,
                vaultReach: reach,
                vaultMaxAngleDegrees: angle,
                vaultMinHeight: minHeight,
                vaultMaxDepth: depth);
        }

        private static NetMatchFile FileWith(NetMotorSettings motor)
        {
            var file = TestRules.File();
            return new NetMatchFile(
                file.Name, file.Rules, file.Settings, file.LoadoutName, file.LoadoutRule, file.LoadoutWeapons, file.SwitchSeconds,
                file.CatalogName, file.Catalog, motor);
        }

        private static VaultProbeResult Evaluate(VaultObstacleSample sample, Vector3 facing, MotorSpec spec)
        {
            return VaultProbe.Evaluate(sample, facing, spec, _radius, _ => true);
        }

        private static CharacterMotorInput Input(Vector2 move, bool jump = false, bool dodge = false, bool vaultAllowed = false)
        {
            return new CharacterMotorInput(move, 0f, false, jump, false, false, false, false, dodge, vaultAllowed);
        }

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation)
        {
            var state = new CharacterMotorState(_start, Vector3.Zero, false, false);
            for (var i = 0; i < _settleTicks; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            Assert.That(state.IsGrounded, Is.True, "the body should stand on the floor before the test");
            return state;
        }

        private int VaultTicks(MotorSpec spec)
        {
            _colliders.Clear();
            AddFloor();
            AddBox(new Vector3(0f, 0.4f, 1.2f), new Vector3(2f, 0.4f, 0.2f));
            var simulation = NewSimulation(spec);
            var state = simulation.Step(Settle(simulation), Input(Vector2.Zero, jump: true, vaultAllowed: true));
            Assert.That(state.IsVaulting, Is.True, simulation.LastVaultProbe.Verdict.ToString());

            var ticks = 1;
            while (state.IsVaulting && ticks < 600)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
                ticks++;
            }

            Assert.That(state.IsVaulting, Is.False);
            return ticks;
        }

        private VaultProbeResult TryVault(MotorSpec spec)
        {
            var simulation = NewSimulation(spec);
            simulation.Step(Settle(simulation), Input(Vector2.Zero, jump: true, vaultAllowed: true));
            return simulation.LastVaultProbe;
        }

        private CharacterMotorSimulation NewSimulation(MotorSpec spec)
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(spec), _tick, _killPlaneY) { SpawnPosition = _start };
        }

        private void AddFloor()
        {
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}
