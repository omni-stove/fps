using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2RemotePlayerTests
    {
        private const string Desert = "skin-desert";
        private const string _mark = "DesertMark";

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private NetV2RemotePlayer _remote;
        private GameObject _desertSkin;
        private readonly List<string> _asked = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _desertSkin = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            _desertSkin.name = "DesertSkin";
            _desertSkin.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            new GameObject(_mark).transform.SetParent(_desertSkin.transform, false);
            _remote = NetV2RemotePlayer.Create(7, _stance, MotorSpec.Default, null);
            Track(_remote.gameObject);
            _asked.Clear();
            _remote.SkinSource = id =>
            {
                _asked.Add(id);
                return id == Desert ? _desertSkin : null;
            };
        }

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

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        [Test]
        public void ASkinIdFromTheSnapshotSwapsTheBody()
        {
            var before = _remote.Body;

            _remote.ShowCosmetic(Desert);

            Assert.That(_remote.CosmeticId, Is.EqualTo(Desert));
            Assert.That(before == null, Is.True, "the default body is gone");
            Assert.That(_remote.Body, Is.Not.Null);
            Assert.That(_remote.Body.name, Is.EqualTo("Body"));
            Assert.That(_remote.Body.parent, Is.SameAs(_remote.transform));
            Assert.That(_remote.Body.Find(_mark), Is.Not.Null, "the body is made from the skin");
            Assert.That(_remote.Body.GetComponentsInChildren<Collider>(true), Is.Empty, "the skin's colliders are dropped");
            Assert.That(_remote.Body.localScale, Is.EqualTo(new Vector3(0.8f, 1f, 0.8f)), "the skin keeps its own size");
        }

        [Test]
        public void TheSameIdIsNotLookedUpAgain()
        {
            _remote.ShowCosmetic(Desert);
            var body = _remote.Body;

            _remote.ShowCosmetic(Desert);

            Assert.That(_remote.Body, Is.SameAs(body));
            Assert.That(_asked, Is.EqualTo(new[] { Desert }));
        }

        [Test]
        public void AnEmptyOrUnknownIdGoesBackToTheDefaultBody()
        {
            _remote.ShowCosmetic(Desert);

            _remote.ShowCosmetic("");

            Assert.That(_remote.CosmeticId, Is.Empty);
            Assert.That(_remote.Body.Find(_mark), Is.Null);
            Assert.That(_asked, Is.EqualTo(new[] { Desert }), "an empty id is not looked up");

            _remote.ShowCosmetic("unknown");

            Assert.That(_remote.CosmeticId, Is.EqualTo("unknown"));
            Assert.That(_remote.Body.Find(_mark), Is.Null, "an id this client does not know keeps the default body");

            _remote.ShowCosmetic(null);
            Assert.That(_remote.CosmeticId, Is.Empty);
        }

        [Test]
        public void TheNewBodyKeepsTheStance()
        {
            _remote.Show(new RemotePose(System.Numerics.Vector3.Zero, 0f, PlayerStance.Crouching, null));

            _remote.ShowCosmetic(Desert);

            // しゃがみの高さ 1.2 / 立ちの高さ 2 だけ、スキンの縦 (1) を縮める
            Assert.That(_remote.Body.localScale.y, Is.EqualTo(0.6f).Within(1e-5f), "the crouch still squashes the new body");
            Assert.That(_remote.Body.localScale.x, Is.EqualTo(0.8f).Within(1e-5f));
        }
    }
}
