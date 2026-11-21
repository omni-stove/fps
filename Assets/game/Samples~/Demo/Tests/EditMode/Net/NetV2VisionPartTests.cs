using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    // 押したかは slotPressed、相手の体は remotes で渡し、入力の資産と場に残った体に左右されないようにする
    public sealed class NetV2VisionPartTests
    {
        private const int _localId = 1;
        private const int _enemyId = 2;
        private const int _allyId = 3;
        private const int _health = 100;
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _logs = new List<string>();
        private readonly List<NetV2RemotePlayer> _remotes = new List<NetV2RemotePlayer>();
        private readonly bool[] _pressed = new bool[2];
        private NetV2LocalPlayer _local;
        private VisionGogglesDefinition _goggles;
        private SightSpec _sight;
        private NetV2VisionPart _part;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _logs.Clear();
            _remotes.Clear();
            _pressed[0] = false;
            _pressed[1] = false;
            _sight = null;
            _local = NetV2LocalPlayer.Create(_stance, MotorSpec.Default, () => true, Vector3.zero, 0f);
            _created.Add(_local.gameObject);
            _goggles = Goggles("goggles-dual", 0f, VisionMode.Night, VisionMode.Thermal);
        }

        [TearDown]
        public void TearDown()
        {
            if (_part != null)
            {
                // Edit Mode では OnDestroy が走らないので、コードで作った光る材質はここで消す
                var code = _part.Thermal != null ? _part.Thermal.CodeMaterial : null;
                _part.Dispose();
                if (code != null)
                {
                    Object.DestroyImmediate(code);
                }

                _part = null;
            }

            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            GameSignals.ResetAll();
        }

        [Test]
        public void OnlyGogglesAreTakenFromTheAbilities()
        {
            var shield = Create<ShieldDeployDefinition>("Shield");

            var slots = NetV2VisionPart.SlotsOf(new AbilityDefinition[] { shield, _goggles, Goggles("extra", 0f, VisionMode.Night) });

            Assert.That(slots, Is.EqualTo(new VisionGogglesDefinition[] { null, _goggles }), "the third ability has no input");
            Assert.That(NetV2VisionPart.SlotsOf(null), Is.EqualTo(new VisionGogglesDefinition[2]));
        }

        [Test]
        public void AcceptedIdsAreLookedUpInTheCatalog()
        {
            var catalog = Create<AbilityCatalogAsset>("Catalog");
            catalog.SetAbilities(new AbilityDefinition[] { _goggles });

            var slots = NetV2VisionPart.SlotsOf(new[] { AbilitySelection.None, "goggles-dual" }, catalog);

            Assert.That(slots, Is.EqualTo(new VisionGogglesDefinition[] { null, _goggles }));
            Assert.That(NetV2VisionPart.SlotsOf(new[] { "goggles-dual" }, null), Is.EqualTo(new VisionGogglesDefinition[2]));
        }

        [Test]
        public void PressingPutsOnSwitchesAndTakesOffUsingOneCharge()
        {
            _part = Part(_goggles);
            _part.Apply(Snapshot());

            Press(0);
            Assert.That(_part.Controller, Is.Not.Null, "the vision parts are added to the local player");
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.Night));
            Assert.That(GameSignals.Vision.Value, Is.EqualTo((VisionStatus?)new VisionStatus(VisionMode.Night, VisionSource.Goggles)));

            Press(0);
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.Thermal), "switching works with no charge left");

            Press(0);
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.None));
            Assert.That(GameSignals.Vision.HasValue, Is.False);
            Assert.That(_logs, Does.Contain("[Ability] slot=1 id=goggles-dual follow-up"));

            Press(0);
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.None), "the only charge was used to put them on");
            Assert.That(_logs, Does.Contain("[Ability] slot=1 id=goggles-dual rejected reason=NoCharges wait=never"));
        }

        [Test]
        public void TheWindupDelaysPuttingThemOn()
        {
            var slow = Goggles("goggles-slow", 0.5f, VisionMode.Night);
            _part = Part(slow);
            _part.Apply(Snapshot());

            Press(0);
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.None));

            Press(0);
            Assert.That(_logs, Does.Contain("[Ability] slot=1 id=goggles-slow rejected reason=Running"));

            _part.Tick(0.5f);
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.Night));
            Assert.That(_logs, Does.Contain("[Ability] slot=1 id=goggles-slow effect"));
        }

        [Test]
        public void ThermalLightsUpTheEnemyButNotTheAlly()
        {
            var enemy = Remote(_enemyId, out var enemyRenderer);
            var ally = Remote(_allyId, out var allyRenderer);
            var enemyOriginal = enemyRenderer.sharedMaterial;
            var allyOriginal = allyRenderer.sharedMaterial;
            _part = Part(_goggles);
            _part.Apply(Snapshot());

            Assert.That(_part.IsThermalTarget(_enemyId), Is.True);
            Assert.That(_part.IsThermalTarget(_allyId), Is.False);
            Assert.That(_part.IsThermalTarget(_localId), Is.False);

            Press(0);
            Press(0);

            Assert.That(_part.Thermal.IsActive, Is.True);
            Assert.That(_part.Thermal.IsHighlighted(enemy.Body.gameObject), Is.True);
            Assert.That(enemyRenderer.sharedMaterial, Is.SameAs(_part.Thermal.CodeMaterial));
            Assert.That(_part.Thermal.IsHighlighted(ally.Body.gameObject), Is.False);
            Assert.That(allyRenderer.sharedMaterial, Is.SameAs(allyOriginal));

            Press(0);

            Assert.That(enemyRenderer.sharedMaterial, Is.SameAs(enemyOriginal));
        }

        [Test]
        public void AnEnemyWhoDiesIsPutBackOnTheNextRescan()
        {
            var enemy = Remote(_enemyId, out var renderer);
            var original = renderer.sharedMaterial;
            _part = Part(_goggles);
            _part.Apply(Snapshot());
            Press(0);
            Press(0);
            Assert.That(_part.Thermal.IsHighlighted(enemy.Body.gameObject), Is.True);

            _part.Apply(Snapshot(enemyHealth: 0));
            _part.Thermal.Rescan();

            Assert.That(_part.Thermal.IsHighlighted(enemy.Body.gameObject), Is.False);
            Assert.That(renderer.sharedMaterial, Is.SameAs(original));
        }

        [Test]
        public void DyingTakesTheGogglesOffAndRespawningGivesTheChargeBack()
        {
            _part = Part(_goggles);
            _part.Apply(Snapshot());
            Press(0);

            _part.Apply(Snapshot(ownHealth: 0));

            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.None));
            Assert.That(GameSignals.Vision.HasValue, Is.False);
            Assert.That(_logs, Does.Contain("[Vision] goggles owner=1 off reason=died"));

            Press(0);
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.None), "the fallen cannot put them on");
            Assert.That(_logs, Does.Contain("[Ability] slot=1 id=goggles-dual rejected reason=Incapacitated"));

            _part.Apply(Snapshot());
            Press(0);
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.Night));
        }

        [Test]
        public void WithAbilitiesOffPressingDoesNothingAndTurningThemOffTakesTheGogglesOff()
        {
            _part = Part(_goggles);
            _part.Apply(Snapshot(abilities: Abilities.Off));
            Press(0);
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.None));

            _part.Apply(Snapshot());
            Press(0);
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.Night));

            _part.Apply(Snapshot(abilities: Abilities.Off));

            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.None));
            Assert.That(_logs, Does.Contain("[Vision] goggles owner=1 off reason=abilities-off"));
        }

        [Test]
        public void SlotsWithoutTheWornModeTakeTheGogglesOff()
        {
            _part = Part(_goggles);
            _part.Apply(Snapshot());
            Press(0);

            _part.SetSlots(new VisionGogglesDefinition[] { null, Goggles("thermal-only", 0f, VisionMode.Thermal) });

            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.None));
            Assert.That(_logs, Does.Contain("[Vision] net goggles slots=-,thermal-only"));
        }

        [Test]
        public void BrokenGogglesLeaveTheSlotEmpty()
        {
            var broken = Goggles("broken", 0f, VisionMode.Night, VisionMode.Night);

            _part = Part(broken);

            Assert.That(_part.Slots, Is.EqualTo(new VisionGogglesDefinition[2]));
            Assert.That(_logs, Does.Contain("[Vision] net goggles slot=1 not equipped: vision goggles 'broken' is invalid: mode Night is listed twice"));
        }

        [Test]
        public void AThermalScopeShowsOnlyWhileAimingAndAlive()
        {
            var scope = new SightSpec(visionMode: VisionMode.Thermal);

            Assert.That(NetV2VisionPart.SightOf(scope, true, true).VisionMode, Is.EqualTo(VisionMode.Thermal));
            Assert.That(NetV2VisionPart.SightOf(scope, false, true).VisionMode, Is.EqualTo(VisionMode.None));
            Assert.That(NetV2VisionPart.SightOf(scope, true, false), Is.EqualTo(SightView.None));
            Assert.That(NetV2VisionPart.SightOf(null, true, true).VisionMode, Is.EqualTo(VisionMode.None));
        }

        [Test]
        public void ANewLocalPlayerAfterAMapChangeGetsTheVisionPartsAgain()
        {
            _part = Part(_goggles);
            _part.Apply(Snapshot());
            Press(0);
            var first = _part.Controller;

            Object.DestroyImmediate(_local.gameObject);
            _local = NetV2LocalPlayer.Create(_stance, MotorSpec.Default, () => true, Vector3.zero, 0f);
            _created.Add(_local.gameObject);
            _part.Tick(0.1f);

            Assert.That(_part.Controller, Is.Not.Null);
            Assert.That(_part.Controller, Is.Not.SameAs(first));
            Assert.That(_part.Controller.gameObject, Is.SameAs(_local.gameObject));
            Assert.That(_part.Controller.Goggles, Is.EqualTo(VisionMode.None));
        }

        [Test]
        public void DisposingRemovesTheVisionPartsAndClearsTheView()
        {
            _part = Part(_goggles);
            _part.Apply(Snapshot());
            Press(0);
            Assert.That(GameSignals.Vision.HasValue, Is.True);

            _part.Dispose();
            _part = null;

            Assert.That(GameSignals.Vision.HasValue, Is.False);
            Assert.That(_local.GetComponent<VisionModeController>(), Is.Null);
            Assert.That(_local.GetComponent<ThermalHighlighter>(), Is.Null);
        }

        [Test]
        public void TheRegisteredFactoryLeavesReplaysOut()
        {
            using (var client = new MatchClient(new NullTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add))
            {
                var context = new NetV2ClientPartsContext(client, _ => true, _logs.Add) { Replay = true };
                Assert.That(NetV2VisionPart.Create(context), Is.Null);

                context.Replay = false;
                context.Abilities = new AbilityDefinition[] { _goggles };
                var part = NetV2VisionPart.Create(context);
                try
                {
                    Assert.That(part, Is.InstanceOf<NetV2VisionPart>());
                    Assert.That(((NetV2VisionPart)part).Slots, Is.EqualTo(new VisionGogglesDefinition[] { _goggles, null }));
                }
                finally
                {
                    part.Dispose();
                }
            }
        }

        private NetV2VisionPart Part(VisionGogglesDefinition slot1)
        {
            return new NetV2VisionPart(
                () => _localId,
                new[] { slot1, null },
                () => _local,
                () => _sight,
                () => _remotes,
                slot => _pressed[slot],
                _logs.Add);
        }

        private void Press(int slot)
        {
            _pressed[slot] = true;
            _part.Tick(0f);
            _pressed[slot] = false;
        }

        private NetV2RemotePlayer Remote(int playerId, out Renderer renderer)
        {
            var remote = NetV2RemotePlayer.Create(playerId, _stance, MotorSpec.Default, null);
            _created.Add(remote.gameObject);
            _remotes.Add(remote);
            renderer = remote.Body.GetComponentInChildren<Renderer>();
            return remote;
        }

        private VisionGogglesDefinition Goggles(string id, float windup, params VisionMode[] modes)
        {
            var goggles = Create<VisionGogglesDefinition>(id);
            goggles.Id = id;
            goggles.Modes = modes;
            goggles.MaxCharges = 1;
            goggles.RechargeSeconds = 0f;
            goggles.CooldownSeconds = 0f;
            goggles.WindupSeconds = windup;
            return goggles;
        }

        private T Create<T>(string assetName)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            _created.Add(asset);
            return asset;
        }

        private static SnapshotMessage Snapshot(int ownHealth = _health, int enemyHealth = _health, Abilities abilities = Abilities.On)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            snapshot.Match.State = GameModeState.Playing;
            var rules = MatchRulesSync.From(GameRules.Default);
            rules.Abilities = abilities;
            snapshot.Match.Rules = rules;
            AddPlayer(snapshot, _localId, Team.Alpha, ownHealth);
            AddPlayer(snapshot, _enemyId, Team.Bravo, enemyHealth);
            AddPlayer(snapshot, _allyId, Team.Alpha, _health);
            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId, Team team, int health)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = team;
            row.Health = health;
        }

        private sealed class NullTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, System.ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return MessageWriter.DefaultMaxLength;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
