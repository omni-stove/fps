using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class ServerRulesExporterPayloadTests
    {
        private const string ScenePath = "Assets/Test.unity";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ASceneWithoutACartHasNoPayload()
        {
            Assert.That(ServerRulesExporter.ToPayload(ScenePath, Array.Empty<PayloadCart>(), 0), Is.Null);
        }

        [Test]
        public void TheCartRouteCheckpointsAndAttackersAreWritten()
        {
            var cart = Cart(new Vector3(0f, -1f, -10f), new Vector3(0f, -1f, 0f), new Vector3(5f, -1f, 0f), new Vector3(5f, -1f, 10f));
            cart.SetRoute(cart.Points, new[] { 1, 2 });

            var payload = ServerRulesExporter.ToPayload(ScenePath, new[] { cart }, 1);

            Assert.That(payload.Attackers, Is.EqualTo(Team.Bravo));
            Assert.That(payload.Route.PointCount, Is.EqualTo(4));
            Assert.That(payload.Route.Point(2), Is.EqualTo(new System.Numerics.Vector3(5f, -1f, 0f)));
            Assert.That(payload.CheckpointPoints, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(payload.Route.Length, Is.EqualTo(25d).Within(1e-4));
        }

        [Test]
        public void ACartWithoutAZoneUsesTheRadiusTheGameAddsWhilePlaying()
        {
            var cart = Cart(Vector3.zero, new Vector3(0f, 0f, 10f));

            var payload = ServerRulesExporter.ToPayload(ScenePath, new[] { cart }, 0);

            Assert.That(payload.Radius, Is.EqualTo(ControlPointZone.DefaultRadius));
            Assert.That(payload.Height, Is.EqualTo(ControlPointZone.DefaultHeight));
        }

        [Test]
        public void TheZoneOfTheCartGivesTheRadiusAndHeight()
        {
            var cart = Cart(Vector3.zero, new Vector3(0f, 0f, 10f));
            var zoneObject = new GameObject("Zone");
            zoneObject.transform.SetParent(cart.transform, false);
            var zone = zoneObject.AddComponent<ControlPointZone>();
            zone.Radius = 3.5f;
            zone.Height = 4f;

            var payload = ServerRulesExporter.ToPayload(ScenePath, new[] { cart }, 0);

            Assert.That(payload.Radius, Is.EqualTo(3.5f));
            Assert.That(payload.Height, Is.EqualTo(4f));
        }

        [Test]
        public void TwoCartsAreRefused()
        {
            var first = Cart(Vector3.zero, new Vector3(0f, 0f, 10f));
            var second = Cart(Vector3.zero, new Vector3(10f, 0f, 0f));

            var e = Assert.Throws<InvalidOperationException>(() => ServerRulesExporter.ToPayload(ScenePath, new[] { first, second }, 0));
            Assert.That(e.Message, Does.Contain("has 2 PayloadCarts"));
        }

        [Test]
        public void AnAttackerSideWithoutATeamIsRefused()
        {
            var cart = Cart(Vector3.zero, new Vector3(0f, 0f, 10f));

            var e = Assert.Throws<InvalidOperationException>(() => ServerRulesExporter.ToPayload(ScenePath, new[] { cart }, 2));
            Assert.That(e.Message, Does.Contain("side 2"));
        }

        [Test]
        public void ABrokenRouteNamesTheCart()
        {
            var cart = Cart(Vector3.zero);

            var e = Assert.Throws<InvalidOperationException>(() => ServerRulesExporter.ToPayload(ScenePath, new[] { cart }, 0));
            Assert.That(e.Message, Does.Contain("PayloadCart 'Cart'"));
            Assert.That(e.Message, Does.Contain("at least 2 points"));
        }

        [Test]
        public void AnUnsetRoutePointIsRefused()
        {
            var cart = Cart(Vector3.zero, new Vector3(0f, 0f, 10f));
            cart.SetRoute(new[] { cart.Points[0], null }, Array.Empty<int>());

            var e = Assert.Throws<InvalidOperationException>(() => ServerRulesExporter.ToPayload(ScenePath, new[] { cart }, 0));
            Assert.That(e.Message, Does.Contain("route point #1 is not set"));
        }

        private PayloadCart Cart(params Vector3[] route)
        {
            var root = new GameObject("Cart");
            _created.Add(root);
            var points = new Transform[route.Length];
            for (var i = 0; i < route.Length; i++)
            {
                var point = new GameObject("Point " + i);
                _created.Add(point);
                point.transform.position = route[i];
                points[i] = point.transform;
            }

            var cart = root.AddComponent<PayloadCart>();
            cart.SetRoute(points, Array.Empty<int>());
            return cart;
        }
    }
}
