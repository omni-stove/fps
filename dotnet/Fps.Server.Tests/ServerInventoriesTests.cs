using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // 移動・ルール・戦闘・持ち物を同じホストで動かし、出現で持たせる・頼みを確かめる・持ち主に送るを確かめる
    public sealed class ServerInventoriesTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _caseId = "2f4e6d8c-0b1a-4c3e-8d5f-7a9b1c3d5e7f";
        private static readonly DateTime _time = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        private static readonly ItemSpec _herb = new ItemSpec(_herbId, "item.herb", maxStack: 3, weight: 0.5f, useHealth: 30);
        private static readonly ItemSpec _case = new ItemSpec(_caseId, "item.case", weight: 4f, width: 2, height: 1);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerInventories _inventories;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
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
        public void SpawningGivesTheStartingItemsAndTellsOnlyTheOwner()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            var states = _transport.Read<InventoryStateMessage>(1, Copy);
            Assert.That(states, Is.Not.Empty, _log.ToString());
            var last = states.Last();
            Assert.That(last.GridWidth, Is.EqualTo(4));
            Assert.That(last.GridHeight, Is.EqualTo(2));
            Assert.That(Enumerable.Range(0, last.EntryCount).Sum(i => last.EntryAt(i).Count), Is.EqualTo(5));
            Assert.That(_inventories.InventoryOf(1).CountOf(_herbId), Is.EqualTo(4));
            Assert.That(_inventories.InventoryOf(1).CountOf(_caseId), Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] items=2 starting=2 maxStacks=0 maxWeight=0 grid=4x2 rotation=on carryLoad=on"));
        }

        [Test]
        public void TheCountedWeightIsCopiedToTheBody()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            // ケース 4 kg + ハーブ 4 個 × 0.5 kg
            Assert.That(Body(1).CarriedItemWeight, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void MovingAStackIsCheckedAndTheResultIsSentBack()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            var caseHandle = inventory.Stacks.Single(s => s.ItemId == _caseId).Handle;
            var sentBefore = _transport.Read<InventoryStateMessage>(1, Copy).Count;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Move, Handle = (ushort)caseHandle, X = 3, Y = 0 });
            Tick();

            Assert.That(_log.ToString(), Does.Contain("[Inventory] move owner=1 handle=" + caseHandle + " to=3,0 result=OutOfGrid"));
            Assert.That(_transport.Read<InventoryStateMessage>(1, Copy), Has.Count.EqualTo(sentBefore + 1), "a refused move still resends the state");

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Move, Handle = (ushort)caseHandle, X = 2, Y = 1 });
            Tick();

            Assert.That(inventory.TryGetStack(caseHandle, out var moved), Is.True);
            Assert.That(moved.X, Is.EqualTo(2));
            Assert.That(moved.Y, Is.EqualTo(1));
        }

        [Test]
        public void UsingAnItemHealsAndTakesOne()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            _rules.ApplyDamage(2, 1, 50);
            var inventory = _inventories.InventoryOf(1);
            var herbHandle = inventory.Stacks.First(s => s.ItemId == _herbId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Use, Handle = (ushort)herbHandle });
            Tick();

            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 20), _log.ToString());
            Assert.That(inventory.CountOf(_herbId), Is.EqualTo(3));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] use owner=1 item=" + _herbId + " health=+30"));
        }

        [Test]
        public void UsingAtFullHealthOrAnUnusableItemIsRefused()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            var herbHandle = inventory.Stacks.First(s => s.ItemId == _herbId).Handle;
            var caseHandle = inventory.Stacks.Single(s => s.ItemId == _caseId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Use, Handle = (ushort)herbHandle });
            Request(1, new InventoryRequestMessage { Action = InventoryAction.Use, Handle = (ushort)caseHandle });
            Tick();

            Assert.That(inventory.CountOf(_herbId), Is.EqualTo(4));
            Assert.That(inventory.CountOf(_caseId), Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] use owner=1 handle=" + herbHandle + " rejected=Full"));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] use owner=1 handle=" + caseHandle + " rejected=NotUsable"));
        }

        [Test]
        public void DroppingDiscardsTheCountAndLightensTheBody()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            var caseHandle = inventory.Stacks.Single(s => s.ItemId == _caseId).Handle;

            Request(1, new InventoryRequestMessage { Action = InventoryAction.Drop, Handle = (ushort)caseHandle, Count = 1 });
            Tick();

            Assert.That(inventory.CountOf(_caseId), Is.Zero);
            Assert.That(Body(1).CarriedItemWeight, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(_log.ToString(), Does.Contain("[Inventory] drop owner=1 item=" + _caseId + " count=1 discarded"));
        }

        [Test]
        public void DyingAndRespawningRefillsTheStartingItems()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            var inventory = _inventories.InventoryOf(1);
            inventory.Remove(_herbId, 4);

            _rules.ApplyDamage(2, 1, 1000);
            Step(NetMatchSettings.DefaultRespawnSeconds + 0.5f);

            Assert.That(_rules.Session.IsAlive(1), Is.True, _log.ToString());
            Assert.That(inventory.CountOf(_herbId), Is.EqualTo(4));
        }

        private static ItemCatalog Catalog()
        {
            return new ItemCatalog(
                new[] { _herb, _case },
                new InventoryRule(gridWidth: 4, gridHeight: 2, countWeightInCarryLoad: true),
                new[] { new ItemCount(_caseId, 1), new ItemCount(_herbId, 4) });
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void Start(ItemCatalog items)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, null);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(Rules()), PingSpec.Default, "memory", null, items: items);
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
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _inventories = new ServerInventories(_rules);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(new ServerCombat(_rules, movement));
            _host.Register(_inventories);
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Request(int id, InventoryRequestMessage request)
        {
            _host.OnMessage(new ConnectionId((ulong)id), request);
        }

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
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

        private static InventoryStateMessage Copy(InventoryStateMessage m)
        {
            var copy = new InventoryStateMessage
            {
                MaxStacks = m.MaxStacks,
                MaxWeight = m.MaxWeight,
                GridWidth = m.GridWidth,
                GridHeight = m.GridHeight,
                AllowRotation = m.AllowRotation,
            };
            for (var i = 0; i < m.ItemCount; i++)
            {
                copy.AddItem(m.ItemAt(i));
            }

            for (var i = 0; i < m.EntryCount; i++)
            {
                copy.AddEntry(m.EntryAt(i));
            }

            return copy;
        }
    }
}
