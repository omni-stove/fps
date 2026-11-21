using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・近接を同じホストで動かし、InputButtons.Melee で振った近接が届く相手にだけ当たることを確かめる
    public sealed class ServerMeleeTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _knifeId = "melee.knife";

        // +X を向く yaw (ラジアン)。Alpha の出現地点 (-5, *, 0) から Bravo の出現地点の側を向く
        private const float _facingEast = MathF.PI / 2f;

        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        // 当たりの瞬間 0.2 秒、振り終え 0.5 秒、そこから 0.4 秒待つ。踏み込みはサーバーで動かさないので 0 にする
        private static readonly MeleeSpec _spec = new MeleeSpec(
            damage: 30, reach: 2f, arcDegrees: 90f, swingSeconds: 0.5f, cooldownSeconds: 0.4f, impactSeconds: 0.2f, lungeDistance: 0f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerMelee _melee;
        private int _inputTick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _inputTick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void ATargetWithinReachInFrontTakesTheMeleeDamageAtTheImpact()
        {
            StartWithTwo();
            Place(2, -3.5f, 0f);

            Press(1);
            Hold(1, InputButtons.None, 5);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "no damage before the impact moment");
            Assert.That(_melee.IsSwinging(1), Is.True);

            Hold(1, InputButtons.None, 12);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), _log.ToString());
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Melee] swing id=1"));
            Assert.That(log, Does.Contain("[Melee] hit id=1 target=2 dmg=30 distance=1.00 kind=Melee"));
            Assert.That(log, Does.Contain("[Combat] dealt attacker=1 victim=2 weapon=" + TestRules.RifleId + " zone=Body dmg=30 outcome=Damaged"));
            var confirm = _transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Damage = m.Damage, Zone = m.Zone, Killed = m.Killed }).Single();
            Assert.That(confirm.Damage, Is.EqualTo(30));
            Assert.That(confirm.Zone, Is.EqualTo(HitZone.Body));
            Assert.That(confirm.Killed, Is.False);
            Assert.That(_transport.Read<DamageTakenMessage>(2, m => new DamageTakenMessage { Amount = m.Amount }).Single().Amount, Is.EqualTo(30));
        }

        [Test]
        public void ATargetOutOfReachTakesNoMeleeDamage()
        {
            StartWithTwo();

            // 中心どうし 3.5 m、体の表面まで 3 m で、届く 2 m より遠い
            Place(2, -1.5f, 0f);

            Swing(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Melee] miss id=1"));
            Assert.That(_log.ToString(), Does.Not.Contain("[Melee] hit"));
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => m), Is.Empty);
        }

        // 届く距離は体の表面 (半径 0.5 m) まで測る。表面まで 1.9 m なら届き、2.1 m なら届かない
        [TestCase(2.4f, NetMatchSettings.DefaultMaxHealth - 30)]
        [TestCase(2.6f, NetMatchSettings.DefaultMaxHealth)]
        public void ReachIsMeasuredToTheSurfaceOfTheBody(float centerDistance, int expectedHealth)
        {
            StartWithTwo();
            Place(2, _alphaSpawn.X + centerDistance, 0f);

            Swing(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(expectedHealth), _log.ToString());
        }

        [Test]
        public void ATargetBehindTheSwingTakesNoMeleeDamage()
        {
            StartWithTwo();
            Place(2, -6.5f, 0f);

            Swing(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Melee] miss id=1"));
        }

        [Test]
        public void AWallBetweenStopsTheMelee()
        {
            Start(MeleeRules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);
            Place(2, -3.5f, 0f);

            Swing(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Melee] miss id=1"));
        }

        [Test]
        public void TheNearestTargetInTheFanIsHit()
        {
            Start(MeleeRules());
            Join(1);
            Join(2);
            Join(3);
            Join(4);
            Step(0.2f);
            Assert.That(_rules.Session.GetTeam(4), Is.EqualTo(_rules.Session.GetTeam(2)));

            // 2 は表面まで約 1.03 m で 11 度横、4 は表面まで約 0.72 m で 35 度横。2 と 4 の体は重ならない
            Place(2, -3.5f, -0.3f);
            Place(4, -4f, 0.7f);
            Place(3, -5f, 6f);

            Swing(1);

            Assert.That(_rules.Session.GetHealth(4), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), _log.ToString());
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void AnotherPressIsRefusedUntilTheCooldownEnds()
        {
            StartWithTwo();
            Place(2, -3.5f, 0f);

            // 0.3 秒で当たり、振り終えは 0.5 秒、次に振れるのは 0.9 秒から
            Press(1);
            Hold(1, InputButtons.None, 17);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30));

            // 0.6 秒: 振り終えたが待っている
            Hold(1, InputButtons.None, 18);
            Assert.That(_melee.IsSwinging(1), Is.False);
            Assert.That(_melee.CooldownRemaining(1), Is.GreaterThan(0f));
            Press(1);
            Hold(1, InputButtons.None, 30);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Melee] refused id=1 reason=Cooldown"));

            // 1.1 秒: 待ち終えたので振れる
            Assert.That(_melee.CooldownRemaining(1), Is.Zero);
            Swing(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 60), _log.ToString());
            Assert.That(Count(_log.ToString(), "[Melee] swing id=1"), Is.EqualTo(2));
        }

        [Test]
        public void HoldingTheButtonSwingsOnlyOnce()
        {
            StartWithTwo();
            Place(2, -3.5f, 0f);

            Hold(1, InputButtons.Melee, (int)(1.5f / _tickSeconds));

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), _log.ToString());
            Assert.That(Count(_log.ToString(), "[Melee] swing id=1"), Is.EqualTo(1));
        }

        [Test]
        public void TheSamePressResentInALaterMessageIsNotASecondSwing()
        {
            StartWithTwo();
            Place(2, -3.5f, 0f);

            var tick = ++_inputTick;
            Send(1, new InputFrame(tick, Vector2.Zero, _facingEast, InputButtons.None));
            var input = new InputMessage();
            input.Add(new InputFrame(tick, Vector2.Zero, _facingEast, InputButtons.None));
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, _facingEast, InputButtons.Melee));
            _host.OnMessage(new ConnectionId(1), input);
            Tick();

            // 0.6 秒後: 振り終え、待っている間に同じ 2 つの tick がもう一度届く (落ちてもよい届け方の送り直し)
            Hold(1, InputButtons.None, 36);
            _host.OnMessage(new ConnectionId(1), input);
            Hold(1, InputButtons.None, 36);

            Assert.That(Count(_log.ToString(), "[Melee] swing id=1"), Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Not.Contain("[Melee] refused"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30));
        }

        [Test]
        public void MeleeIsRefusedWhenTheRulesTurnItOff()
        {
            Start(MeleeRules().WithActions(MeleeRules().Actions.WithMelee(Melee.Off)));
            Join(1);
            Join(2);
            Step(0.2f);
            Place(2, -3.5f, 0f);

            Swing(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Melee] refused id=1 reason=Off"));
            Assert.That(_log.ToString(), Does.Contain("[Melee] server rule=Off"));
        }

        [Test]
        public void ATeammateIsNotHurtWithFriendlyFireOff()
        {
            Start(MeleeRules());
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            Assert.That(_rules.Session.GetTeam(3), Is.EqualTo(_rules.Session.GetTeam(1)));
            Place(3, -3.5f, 0f);
            Place(2, 5f, 0f);

            Swing(1);

            Assert.That(_rules.Session.GetHealth(3), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Not.Contain("[Combat] dealt"));
        }

        [Test]
        public void AMeleeKillIsCountedWithTheGivenWeaponId()
        {
            var lethal = new MeleeSpec(
                damage: NetMatchSettings.DefaultMaxHealth, reach: 2f, arcDegrees: 90f, swingSeconds: 0.5f, cooldownSeconds: 0.4f, impactSeconds: 0.2f, lungeDistance: 0f);
            Start(MeleeRules(), lethal, _knifeId);
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;
            Join(1);
            Join(2);
            Step(0.2f);
            Place(2, -3.5f, 0f);

            Swing(1);

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            Assert.That(_rules.Session.GetKills(1), Is.EqualTo(1));
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].KillerId, Is.EqualTo(1));
            Assert.That(kills[0].VictimId, Is.EqualTo(2));
            Assert.That(kills[0].WeaponId, Is.EqualTo(_knifeId));
            Assert.That(kills[0].Headshot, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Melee] server rule=On damage=100 reach=2 arc=90 impact=0.2 swing=0.5 cooldown=0.4 weapon=melee.knife"));
        }

        [Test]
        public void ADeadPlayerCannotSwing()
        {
            var lethal = new MeleeSpec(
                damage: NetMatchSettings.DefaultMaxHealth, reach: 2f, arcDegrees: 90f, swingSeconds: 0.5f, cooldownSeconds: 0f, impactSeconds: 0.2f, lungeDistance: 0f);
            Start(MeleeRules(), lethal);
            Join(1);
            Join(2);
            Step(0.2f);
            Place(2, -3.5f, 0f);
            Place(1, -2f, 0f);

            // 2 が東の 1 を倒し、倒れた 1 は振れない
            Hold(2, InputButtons.Melee, 1);
            Hold(2, InputButtons.None, 30);
            Assert.That(_rules.Session.IsAlive(1), Is.False, _log.ToString());

            Press(1);

            Assert.That(_log.ToString(), Does.Contain("[Melee] refused id=1 reason=Busy"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void OnlyABotIsSwungWithoutAnInput()
        {
            StartWithTwo();
            Place(2, -3.5f, 0f);

            Assert.That(_melee.IsOn, Is.True);
            Assert.That(_melee.BotSwing(1), Is.False, "a human swings with InputButtons.Melee");
            Assert.That(_melee.IsSwinging(1), Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Melee] swing id=1"));
        }

        [Test]
        public void ABotReadsTheSameSwingTheModuleAdvances()
        {
            StartWithTwo();
            var botId = BotRoster.FirstBotPlayerId;

            var swing = _melee.SwingOf(botId);

            Assert.That(_melee.SwingOf(botId), Is.SameAs(swing));
            Assert.That(swing.Spec, Is.SameAs(_spec));
            Assert.That(_melee.BotSwing(botId), Is.False, "the bot is not in the match");
            Assert.That(swing.IsSwinging, Is.False);
        }

        [Test]
        public void TheMeleeRuleTellsWhetherBotsMaySwing()
        {
            Start(MeleeRules().WithActions(MeleeRules().Actions.WithMelee(Melee.Off)));

            Assert.That(_melee.IsOn, Is.False);
        }

        [Test]
        public void TheSpecIsRequired()
        {
            var rules = new MatchRulesModule();
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(TestRules.Data()));
            var combat = new ServerCombat(rules, movement);

            Assert.Throws<ArgumentNullException>(() => new ServerMelee(combat, movement, null));
            Assert.Throws<ArgumentNullException>(() => new ServerMelee(null, movement, MeleeSpec.Default));
            Assert.Throws<ArgumentNullException>(() => new ServerMelee(combat, null, MeleeSpec.Default));
        }

        [Test]
        public void SwingingCancelsTheReloadAndHoldsOffANewOneUntilTheSwingEnds()
        {
            StartWithTwo();
            FireEast(1);
            RequestReload(1);
            Assert.That(IsReloading(1), Is.True, _log.ToString());

            Press(1);

            Assert.That(IsReloading(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Combat] reload-cancelled player=1 by=Melee"));
            RequestReload(1);
            Assert.That(IsReloading(1), Is.False, "no reload while swinging");

            Hold(1, InputButtons.None, (int)(_spec.SwingSeconds / _tickSeconds) + 1);
            RequestReload(1);
            Assert.That(IsReloading(1), Is.True);
        }

        [Test]
        public void WithoutMeleeInReloadInterruptedByTheReloadGoesOnThroughTheSwing()
        {
            var rules = MeleeRules();
            Start(rules.WithActions(rules.Actions.WithReloadInterruptedBy(new[] { ReloadInterrupt.Grenade })));
            Join(1);
            Join(2);
            Step(0.2f);
            FireEast(1);
            RequestReload(1);

            Press(1);

            Assert.That(_melee.IsSwinging(1), Is.True);
            Assert.That(IsReloading(1), Is.True);
            Assert.That(_log.ToString(), Does.Not.Contain("reload-cancelled"));
        }

        private static GameRules MeleeRules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithMelee(Melee.On);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void StartWithTwo()
        {
            Start(MeleeRules());
            Join(1);
            Join(2);
            Step(0.2f);
        }

        private void Start(GameRules rules, MeleeSpec spec = null, string weaponId = null, bool wall = false)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            if (wall)
            {
                // Alpha の出現地点と 1.5 m 東の相手の間に立つ薄い壁。どちらの体 (半径 0.5 m) にも触れない
                colliders.Add(WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(-4.25f, 2.5f, 0f), new Vector3(0.05f, 2f, 1f), Quaternion.Identity));
            }

            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, _movement);
            _melee = new ServerMelee(_combat, _movement, spec ?? _spec, weaponId);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_melee);
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Place(int id, float x, float z)
        {
            _context.Bodies.Teleport(id, new Vector3(x, _alphaSpawn.Y, z), 0f, _host.TickCount);
            Tick();
        }

        // 押して離し、振り終えて待ち終えるまで (1 秒) 進める
        private void Swing(int id)
        {
            Press(id);
            Hold(id, InputButtons.None, (int)(1f / _tickSeconds));
        }

        private void FireEast(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            _host.OnMessage(new ConnectionId((ulong)id), new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = body.EyePosition,
                Direction = Vector3.UnitX,
                ClientTick = (int)_host.TickCount,
                InterpolationAlpha = 0f,
                ShotIndex = 0,
                ShotGeneration = 0,
            });
        }

        private void RequestReload(int id)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new WeaponActionRequestMessage { Action = WeaponAction.Reload });
        }

        private bool IsReloading(int id)
        {
            var snapshot = new SnapshotMessage();
            foreach (var player in _match.Players)
            {
                snapshot.Players.Add().PlayerId = player.PlayerId;
            }

            Assert.That(_match.TryGetPlayer(id, out var receiver), Is.True);
            _combat.Contribute(snapshot, receiver);
            return snapshot.Owner.Weapon.IsReloading;
        }

        private void Press(int id)
        {
            Hold(id, InputButtons.Melee, 1);
        }

        private void Hold(int id, InputButtons buttons, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                Send(id, new InputFrame(++_inputTick, Vector2.Zero, _facingEast, buttons));
                Tick();
            }
        }

        private void Send(int id, InputFrame frame)
        {
            var input = new InputMessage();
            input.Add(frame);
            _host.OnMessage(new ConnectionId((ulong)id), input);
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }

        private static int Count(string text, string part)
        {
            var count = 0;
            for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }
    }
}
