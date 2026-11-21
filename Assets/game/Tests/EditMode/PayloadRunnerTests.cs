using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 道筋は X 軸に沿った 20 m の直線で、10 m の点が中継点。台車は 1 m/秒、2 人目からは 2 倍、制限時間 60 秒・中継点で 30 秒延びる。
    // bot (EnemyAgent) は NavMesh が要るので、参加者は SideMember と Damageable だけで作る。
    public sealed class PayloadRunnerTests
    {
        private const float _timeLimit = 60f;
        private const float _bonus = 30f;
        private const float _respawnSeconds = 1f;
        private const float _zoneRadius = 3f;
        private const float _tolerance = 1e-4f;

        // 乗る体のテストは PlayerControllerPlatformTests と同じ板と体の大きさにする。
        private const float _rideTick = 0.02f;
        private const float _halfThickness = 0.25f;
        private const float _skinWidth = 0.08f;

        private static readonly Vector3 _nearStart = new Vector3(0f, 0f, 1f);
        private static readonly Vector3 _far = new Vector3(0f, 0f, 30f);
        private static readonly float[] _multipliers = { 1f, 2f };
        private static readonly int[] _middleCheckpoint = { 1 };

        private readonly List<Object> _objects = new List<Object>();
        private readonly List<InputAction> _actions = new List<InputAction>();
        private PayloadRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _runner = Track(new GameObject("Runner")).AddComponent<PayloadRunner>();
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            foreach (var action in _actions)
            {
                action.Dispose();
            }

            _actions.Clear();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void BeginPutsTheCartAtTheStartAndPublishesTheStatus()
        {
            var cart = Cart();
            var player = Member("Player", 0, _far);
            Begin(cart, player);

            Assert.That(_runner.Cart, Is.SameAs(cart));
            Assert.That(Vector3.Distance(cart.transform.position, Vector3.zero), Is.LessThan(_tolerance));
            Assert.That(Mathf.DeltaAngle(cart.transform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(0.01f), "the cart faces along the route (+X)");
            Assert.That(cart.Zone.Highlight, Is.EqualTo((bool?)true));

            var status = Detail();
            Assert.That(status.Attackers, Is.EqualTo(Side.Alpha));
            Assert.That(status.Progress, Is.Zero);
            Assert.That(status.CheckpointCount, Is.EqualTo(1));
            Assert.That(status.Pushers, Is.Zero);
            Assert.That(_runner.Status.RemainingSeconds, Is.EqualTo(_timeLimit));
        }

        [Test]
        public void AnAttackerNearTheCartPushesItAlongTheRoute()
        {
            var cart = Cart();
            var player = Member("Player", 0, _nearStart);
            Begin(cart, player);

            _runner.Tick(2f);

            Assert.That(_runner.Payload.Distance, Is.EqualTo(2d).Within(_tolerance));
            Assert.That(Vector3.Distance(cart.transform.position, new Vector3(2f, 0f, 0f)), Is.LessThan(_tolerance));
            Assert.That(Vector3.Distance(cart.Zone.transform.position, cart.transform.position), Is.LessThan(_tolerance), "the zone rides with the cart");
            Assert.That(cart.LastMotion.Translation.X, Is.EqualTo(2f).Within(_tolerance));
            Assert.That(cart.LastStepSeconds, Is.EqualTo(2f));

            var status = Detail();
            Assert.That(status.Pushers, Is.EqualTo(1));
            Assert.That(status.Progress, Is.EqualTo(0.1f).Within(_tolerance));
            Assert.That(status.IsContested, Is.False);
            Assert.That(cart.Zone.Owner, Is.EqualTo((Team?)Team.Alpha));
        }

        [Test]
        public void TwoPushersMoveTheCartFaster()
        {
            var cart = Cart();
            var player = Member("Player", 0, _nearStart);
            var mate = Member("Bot-0-1", 0, new Vector3(0f, 0f, -1f));
            Begin(cart, player, mate);

            _runner.Tick(1f);

            Assert.That(_runner.Payload.Distance, Is.EqualTo(2d).Within(_tolerance));
            Assert.That(Detail().Pushers, Is.EqualTo(2));
        }

        [Test]
        public void ADefenderNearTheCartStopsIt()
        {
            var cart = Cart();
            var player = Member("Player", 0, _nearStart);
            var bot = Member("Bot-1-1", 1, new Vector3(1f, 0f, 0f));
            Begin(cart, player, bot);

            _runner.Tick(2f);

            Assert.That(_runner.Payload.Distance, Is.Zero);
            Assert.That(Detail().IsContested, Is.True);
            Assert.That(cart.Zone.Owner, Is.Null, "a contested cart is shown neutral");
        }

        [Test]
        public void ADeadDefenderDoesNotStopTheCart()
        {
            var cart = Cart();
            var player = Member("Player", 0, _nearStart);
            var bot = Member("Bot-1-1", 1, new Vector3(1f, 0f, 0f));
            Begin(cart, player, bot);

            using (CombatEvents.Attribute(player.gameObject, "rifle"))
            {
                bot.Damageable.Kill();
            }

            _runner.Tick(0.5f);

            Assert.That(_runner.Payload.Distance, Is.EqualTo(0.5d).Within(_tolerance));
        }

        [Test]
        public void TheCartRollsBackWhenLeftAlone()
        {
            var cart = Cart();
            var player = Member("Player", 0, _nearStart);
            Begin(cart, player);
            _runner.Tick(2f);

            player.transform.position = _far;
            _runner.Tick(5f);
            _runner.Tick(2f);

            Assert.That(_runner.Payload.Distance, Is.EqualTo(1d).Within(_tolerance));
            Assert.That(Detail().IsRollingBack, Is.True);
            Assert.That(cart.transform.position.x, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(cart.LastMotion.Translation.X, Is.EqualTo(-1f).Within(_tolerance));
        }

        [Test]
        public void ACheckpointExtendsTheClockAndTheEndWinsForTheAttackers()
        {
            var cart = Cart();
            var player = Member("Player", 0, _nearStart);
            Begin(cart, player);

            for (var i = 0; i < 10; i++)
            {
                Follow(player, cart);
                _runner.Tick(1f);
            }

            Assert.That(Detail().CheckpointsReached, Is.EqualTo(1));
            Assert.That(_runner.Status.RemainingSeconds, Is.EqualTo(_timeLimit - 10f + _bonus).Within(_tolerance));

            for (var i = 0; i < 20 && !_runner.IsMatchOver; i++)
            {
                Follow(player, cart);
                _runner.Tick(1f);
            }

            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.Payload.IsDelivered, Is.True);
            Assert.That(cart.transform.position.x, Is.EqualTo(20f).Within(_tolerance));
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(Detail().Progress, Is.EqualTo(1f));
        }

        [Test]
        public void TimeUpWinsForTheDefenders()
        {
            var cart = Cart();
            var player = Member("Player", 0, _far);
            var bot = Member("Bot-1-1", 1, _nearStart);
            Begin(cart, player, bot);

            _runner.Tick(_timeLimit);

            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(_runner.Payload.Distance, Is.Zero);
        }

        [Test]
        public void BeginRejectsAPayloadOffTheCartsRoute()
        {
            var cart = Cart();
            var player = Member("Player", 0, _nearStart);
            cart.BuildRoute();
            var other = new PayloadRoute(new[] { System.Numerics.Vector3.Zero, new System.Numerics.Vector3(5f, 0f, 0f) });
            var payload = PayloadRunner.CreatePayload(Spec(), other, 0, _respawnSeconds);

            Assert.Throws<ArgumentException>(() => _runner.Begin(payload, cart, new[] { player }, Array.Empty<SideSpawnPoint>(), player));
            Assert.Throws<ArgumentNullException>(() => _runner.Begin(null, cart, new[] { player }, Array.Empty<SideSpawnPoint>(), player));
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Payload, Is.Null);
        }

        [Test]
        public void AFailedBeginLeavesNoPayloadAttached()
        {
            var cart = Cart();
            var stray = Member("Bot-Stray", 5, _nearStart);
            var payload = PayloadRunner.CreatePayload(Spec(), cart.BuildRoute(), 0, _respawnSeconds);

            Assert.Throws<ArgumentException>(
                () => _runner.Begin(payload, cart, new[] { stray }, Array.Empty<SideSpawnPoint>()),
                "side 5 is not part of the two teams");
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Payload, Is.Null);
            Assert.That(_runner.Cart, Is.Null);
        }

        [Test]
        public void TheCartRejectsAMissingOrBadRoute()
        {
            var empty = Track(new GameObject("EmptyCart")).AddComponent<PayloadCart>();
            Assert.Throws<InvalidOperationException>(() => empty.BuildRoute());
            Assert.Throws<InvalidOperationException>(() => empty.MoveTo(1d, 0.1f), "a cart without a route cannot move");

            empty.SetRoute(new Transform[] { Point("A", Vector3.zero), null }, null);
            Assert.Throws<InvalidOperationException>(() => empty.BuildRoute());

            var a = Point("B", Vector3.zero);
            var b = Point("C", new Vector3(5f, 0f, 0f));
            empty.SetRoute(new[] { a, b }, new[] { 1 });
            var e = Assert.Throws<ArgumentException>(() => empty.BuildRoute(), "the end cannot be a checkpoint");
            StringAssert.Contains("'EmptyCart'", e.Message);
            Assert.That(empty.IsInitialized, Is.False);
        }

        [Test]
        public void TheCartBuildsAZoneAndABodyWhenNoneAreGiven()
        {
            var go = Track(new GameObject("BareCart"));
            var cart = go.AddComponent<PayloadCart>();
            cart.SetRoute(new[] { Point("A", Vector3.zero), Point("B", new Vector3(0f, 0f, 5f)) }, null);

            cart.BuildRoute();

            Assert.That(cart.Zone, Is.Not.Null);
            Assert.That(cart.Zone.gameObject, Is.SameAs(go));
            Assert.That(go.transform.Find(PayloadCart.BodyName), Is.Not.Null);
            Assert.That(go.GetComponentInChildren<Collider>(), Is.Not.Null);
            Assert.That(go.GetComponent<Rigidbody>().isKinematic, Is.True);
        }

        [Test]
        public void TheCartReportsEachStepToRidersAndDropsAnObstructedOne()
        {
            var cart = Cart();
            cart.BuildRoute();
            var rider = new object();
            var steps = cart.StepCount;

            cart.Board(rider);
            cart.MoveTo(3d, 0.5f);

            Assert.That(cart.IsRiddenBy(rider), Is.True);
            Assert.That(cart.StepCount, Is.EqualTo(steps + 1));
            Assert.That(cart.LastMotion.Translation.X, Is.EqualTo(3f).Within(_tolerance));
            Assert.That(cart.LastMotion.YawDelta, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(cart.Distance, Is.EqualTo(3d).Within(_tolerance));

            cart.Obstruct();
            Assert.That(cart.LastMotion.IsStill, Is.True);
            Assert.That(cart.StepCount, Is.EqualTo(steps + 2));
            Assert.That(cart.transform.position.x, Is.EqualTo(3f).Within(_tolerance), "the cart keeps the place the mode gave it");

            cart.Leave(rider);
            Assert.That(cart.RiderCount, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => cart.MoveTo(1d, -1f));
        }

        [Test]
        public void TheCartTurnsAtACorner()
        {
            var go = Track(new GameObject("CornerCart"));
            go.AddComponent<BoxCollider>();
            var cart = go.AddComponent<PayloadCart>();
            cart.SetRoute(new[] { Point("A", Vector3.zero), Point("B", new Vector3(4f, 0f, 0f)), Point("C", new Vector3(4f, 0f, 4f)) }, null);
            cart.BuildRoute();

            cart.MoveTo(6d, 1f);

            Assert.That(Vector3.Distance(cart.transform.position, new Vector3(4f, 0f, 2f)), Is.LessThan(_tolerance));
            Assert.That(Mathf.DeltaAngle(cart.transform.eulerAngles.y, 0f), Is.EqualTo(0f).Within(0.01f), "the cart faces +Z on the second leg");
            Assert.That(cart.LastMotion.YawDelta, Is.EqualTo(-90f * Mathf.Deg2Rad).Within(1e-3f));
        }

        [Test]
        public void ABodyStandingOnTheCartIsCarriedLikeOnAMovingPlatform()
        {
            var cart = SlabCart();
            var (body, player) = NewPlayer(new Vector3(0f, 1f, 0f));
            for (var i = 0; i < 10; i++)
            {
                player.Tick(Vector2.zero, false, false, false, _rideTick);
            }

            Assert.That(player.IsGrounded, Is.True, "the body should stand on the cart before it moves");
            var bodyStart = body.transform.position;

            var distance = 0d;
            for (var i = 0; i < 50; i++)
            {
                distance += 2d * _rideTick;
                cart.MoveTo(distance, _rideTick);
                player.Tick(Vector2.zero, false, false, false, _rideTick);
            }

            Assert.That(body.transform.position.x - bodyStart.x, Is.EqualTo(2f).Within(0.02f));
            Assert.That(body.transform.position.y, Is.EqualTo(bodyStart.y).Within(0.02f));
            Assert.That(player.RiddenPlatform, Is.SameAs(cart));
            Assert.That(player.Platform, Is.Null, "the cart is not a MovingPlatform");
            Assert.That(cart.IsRiddenBy(player), Is.True);
        }

        [Test]
        public void TheDefinitionChoosesTheAttackersAndReadsItsOwnTuning()
        {
            var cart = Cart();
            var route = cart.BuildRoute();
            var definition = Definition();
            definition.AttackerSideId = 1;
            definition.RespawnSeconds = 2f;
            definition.Tuning = Text("{\"cartSpeed\": 3, \"timeLimitSeconds\": 90}");
            _runner.Definition = definition;

            var payload = _runner.CreatePayload(route);

            Assert.That(payload.Attackers, Is.EqualTo(Side.Bravo));
            Assert.That(payload.Spec.CartSpeed, Is.EqualTo(3f));
            Assert.That(payload.Session.TimeLimitSeconds, Is.EqualTo(90f));

            definition.Tuning = null;
            Assert.That(_runner.CreatePayload(route).Spec, Is.SameAs(PayloadRunner.Tuning), "without its own JSON the definition uses Resources/Tuning/payload");

            definition.AttackerSideId = 2;
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreatePayload(route));
            StringAssert.Contains("'PayloadTuning'", e.Message);
            Assert.That(e.ParamName, Is.EqualTo("attackerSideId"));
        }

        [Test]
        public void WithoutADefinitionTheRunnerUsesItsOwnValuesAndTheBundledTuning()
        {
            var cart = Cart();
            var route = cart.BuildRoute();
            _runner.AttackerSideId = 1;
            _runner.RespawnSeconds = 0f;

            var payload = _runner.CreatePayload(route);

            Assert.That(payload.Attackers, Is.EqualTo(Side.Bravo));
            Assert.That(payload.Spec, Is.SameAs(PayloadRunner.Tuning));
            Assert.That(PayloadRunner.Tuning.CartSpeed, Is.EqualTo(PayloadSpec.DefaultCartSpeed), "the kit ships Resources/Tuning/payload.json with the defaults");

            _runner.RespawnSeconds = -1f;
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreatePayload(route));
        }

        private void Begin(PayloadCart cart, params SideMember[] members)
        {
            var payload = PayloadRunner.CreatePayload(Spec(), cart.BuildRoute(), 0, _respawnSeconds);
            _runner.Begin(payload, cart, members, Array.Empty<SideSpawnPoint>(), members[0]);
        }

        private PayloadStatus Detail()
        {
            var detail = _runner.Status.Detail as PayloadStatus;
            Assert.That(detail, Is.Not.Null, "the mode status carries a PayloadStatus");
            return detail;
        }

        private static void Follow(SideMember member, PayloadCart cart)
        {
            member.transform.position = cart.transform.position + _nearStart;
        }

        private static PayloadSpec Spec()
        {
            return new PayloadSpec(1f, 1, _multipliers, 5f, 0.5f, _bonus, _timeLimit);
        }

        private PayloadCart Cart()
        {
            var go = Track(new GameObject("Cart"));
            go.AddComponent<BoxCollider>();
            var cart = go.AddComponent<PayloadCart>();
            var zoneObject = Track(new GameObject("CartZone"));
            zoneObject.transform.SetParent(go.transform, false);
            var zone = zoneObject.AddComponent<ControlPointZone>();
            zone.ShowsMarker = false;
            zone.Radius = _zoneRadius;
            cart.SetZone(zone);
            cart.SetRoute(
                new[] { Point("Route-0", Vector3.zero), Point("Route-1", new Vector3(10f, 0f, 0f)), Point("Route-2", new Vector3(20f, 0f, 0f)) },
                _middleCheckpoint);
            return cart;
        }

        // 上面が y = 0 の厚さ 0.5・幅 8 の板を台車の体にする。
        private PayloadCart SlabCart()
        {
            var go = Track(new GameObject("SlabCart"));
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.transform.SetParent(go.transform, false);
            slab.transform.localPosition = new Vector3(0f, -_halfThickness, 0f);
            slab.transform.localScale = new Vector3(8f, _halfThickness * 2f, 8f);
            var cart = go.AddComponent<PayloadCart>();
            var zone = go.AddComponent<ControlPointZone>();
            zone.ShowsMarker = false;
            cart.SetZone(zone);
            cart.SetRoute(new[] { Point("Slab-0", Vector3.zero), Point("Slab-1", new Vector3(20f, 0f, 0f)) }, null);
            cart.BuildRoute();
            return cart;
        }

        private (GameObject body, PlayerController player) NewPlayer(Vector3 position)
        {
            var body = Track(new GameObject("Rider"));
            // CharacterController は床から skinWidth だけ浮いて立つので、置く高さを始めからそこにする。
            // 床に接して置くと、最初に横や上へ運ばれた Move で skin の分だけ持ち上がり、運ばれた量に混ざる
            body.transform.position = position + (Vector3.up * _skinWidth);
            var controller = body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            controller.skinWidth = _skinWidth;

            var eye = new GameObject(GameBootstrap.EyeName).transform;
            eye.SetParent(body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);

            var player = body.AddComponent<PlayerController>();
            var move = Action(new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2"));
            var sprint = Action(new InputAction("Sprint", InputActionType.Button));
            var crouch = Action(new InputAction("Crouch", InputActionType.Button));
            var jump = Action(new InputAction("Jump", InputActionType.Button));
            var look = body.AddComponent<FirstPersonLook>();
            player.Initialize(controller, look, move, sprint, crouch, jump, MotorSpec.Default);
            player.Water = NoWaterVolumes.Instance;
            player.SwimTuning = SwimTuning.Default;
            player.WallRunTuning = WallRunTuning.Default;
            return (body, player);
        }

        private InputAction Action(InputAction action)
        {
            _actions.Add(action);
            return action;
        }

        private PayloadDefinition Definition()
        {
            var definition = ScriptableObject.CreateInstance<PayloadDefinition>();
            definition.name = "PayloadTuning";
            _objects.Add(definition);
            return definition;
        }

        private TextAsset Text(string json)
        {
            var asset = new TextAsset(json) { name = "payload-test" };
            _objects.Add(asset);
            return asset;
        }

        private Transform Point(string name, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            return go.transform;
        }

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
