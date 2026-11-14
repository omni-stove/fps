using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="BloodPresenter"/> がキャラへの着弾にだけ部位の血しぶきを出し、背後の近い壁にだけ血痕を付け、
    /// 止める口・カタログが無いとき・プールの上限で正しく振る舞うことを、メモリ上のカタログで確かめる
    /// (Edit Mode では OnEnable が走らないので、PlayImpact と Tick を直接呼ぶ)。
    /// </summary>
    public sealed class BloodPresenterTests
    {
        private readonly List<Object> _created = new List<Object>();
        private HitFeedbackCatalog _catalog;
        private GameObject _head;
        private GameObject _body;
        private GameObject _limb;
        private GameObject _decal;

        [SetUp]
        public void SetUp()
        {
            BloodPresenter.ResetStatics();
            _head = Effect("Head");
            _body = Effect("Body");
            _limb = Effect("Limb");
            _decal = Track(new GameObject("Decal"));

            _catalog = Track(ScriptableObject.CreateInstance<HitFeedbackCatalog>());
            _catalog.SetSprays(new[]
            {
                new ZoneBloodSpray(HitZone.Head, _head),
                new ZoneBloodSpray(HitZone.Body, _body),
                new ZoneBloodSpray(HitZone.Limb, _limb),
            });
            _catalog.DefaultSpray = _body;
            _catalog.WallDecal = _decal;
            _catalog.WallDecalMaxDistance = 2f;
            _catalog.WallDecalLifetime = 10f;
            _catalog.WallDecalSize = 0.5f;
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] == null)
                {
                    continue;
                }

                // Edit Mode では OnDestroy が走らないので、キャラの外に作ったプールの親はここで消す。
                if (_created[i] is GameObject body && body.TryGetComponent<BloodPresenter>(out var presenter) && presenter.PoolRoot != null)
                {
                    Object.DestroyImmediate(presenter.PoolRoot.gameObject);
                }

                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            BloodPresenter.ResetStatics();
        }

        [Test]
        public void ACharacterImpactSpraysTheZonesBloodAlongTheBullet()
        {
            var presenter = Presenter();
            var point = new Vector3(0f, 1.7f, 10f);

            LogAssert.Expect(LogType.Log, "[Blood] spray zone=Head point=(0.00, 1.70, 10.00)");
            var shown = presenter.PlayImpact(CharacterImpact(point, HitZone.Head), Vector3.forward);

            Assert.That(shown, Is.True);
            var pool = presenter.SprayPoolFor(_head);
            Assert.That(pool, Is.Not.Null);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            var spray = pool.Items[0].Root;
            Assert.That(spray.activeSelf, Is.True);
            AssertNear(spray.transform.position, point);
            AssertNear(spray.transform.forward, Vector3.forward, "the spray points the way the bullet travels");
            Assert.That(presenter.SprayPoolFor(_body), Is.Null, "only the hit zone's spray is used");
            Assert.That(presenter.Decals, Is.Null, "no wall behind, no decal");
        }

        [Test]
        public void EachZoneUsesItsOwnSprayAndAnUnknownZoneUsesTheDefault()
        {
            var presenter = Presenter();

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] spray zone=Limb "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] spray zone=none "));
            presenter.PlayImpact(CharacterImpact(Vector3.zero, HitZone.Limb), Vector3.forward);
            presenter.PlayImpact(CharacterImpact(Vector3.zero, null), Vector3.forward);

            Assert.That(presenter.SprayPoolFor(_limb).ActiveCount, Is.EqualTo(1));
            Assert.That(presenter.SprayPoolFor(_body).ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void AnImpactOnAWallShowsNoBlood()
        {
            var presenter = Presenter();
            var wall = Wall("Wall", new Vector3(0f, 1f, 11f));

            var shown = presenter.PlayImpact(new ImpactInfo(new Vector3(0f, 1f, 10.5f), Vector3.back, wall.GetComponent<Collider>()), Vector3.forward);

            Assert.That(shown, Is.False);
            Assert.That(presenter.PoolRoot, Is.Null, "nothing is instantiated for a wall hit");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AWallCloseBehindTheCharacterGetsADecal()
        {
            var presenter = Presenter();
            var wall = Wall("Wall", new Vector3(0f, 1f, 11.5f));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] spray zone=Body "));
            LogAssert.Expect(LogType.Log, "[Blood] decal wall=Wall material=default distance=1.00");
            presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Body), Vector3.forward);

            Assert.That(presenter.Decals, Is.Not.Null);
            Assert.That(presenter.Decals.ActiveCount, Is.EqualTo(1));
            var decal = presenter.Decals.Items[0];
            Assert.That(decal.Root.activeSelf, Is.True);
            AssertNear(decal.Root.transform.position, new Vector3(0f, 1f, 11f - BloodPresenter.DecalSurfaceOffset));
            AssertNear(decal.Root.transform.forward, Vector3.forward, "+Z points into the wall so the quad's face looks out");
            AssertNear(decal.Root.transform.localScale, Vector3.one * 0.5f);
            Assert.That(presenter.DecalAnchorOf(decal), Is.SameAs(wall.GetComponent<Collider>()));
        }

        [Test]
        public void AWallFartherThanTheLimitGetsNoDecal()
        {
            var presenter = Presenter();
            Wall("Wall", new Vector3(0f, 1f, 13.5f));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] spray "));
            presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Body), Vector3.forward);

            Assert.That(presenter.Decals, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheWallProbeSkipsCharactersOnTheWay()
        {
            var other = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            other.name = "OtherDummy";
            other.transform.position = new Vector3(0f, 1f, 10.8f);
            other.transform.localScale = new Vector3(1f, 1f, 0.2f);
            other.AddComponent<Damageable>().Initialize("other", 10);
            var wall = Wall("Wall", new Vector3(0f, 1f, 11.5f));
            Physics.SyncTransforms();

            var found = BloodPresenter.TryFindWall(new Vector3(0f, 1f, 10f), Vector3.forward, 2f, out var hit);

            Assert.That(found, Is.True);
            Assert.That(hit.collider, Is.SameAs(wall.GetComponent<Collider>()));
        }

        [Test]
        public void TheWallProbeIgnoresTriggers()
        {
            var wall = Wall("Trigger", new Vector3(0f, 1f, 11.5f));
            wall.GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms();

            Assert.That(BloodPresenter.TryFindWall(new Vector3(0f, 1f, 10f), Vector3.forward, 2f, out _), Is.False);
        }

        [Test]
        public void BulletDirectionFallsBackToTheReversedNormal()
        {
            AssertNear(BloodPresenter.BulletDirection(Vector3.back, new Vector3(0f, 0f, 3f)), Vector3.forward);
            AssertNear(BloodPresenter.BulletDirection(Vector3.up, null), Vector3.down);
            AssertNear(BloodPresenter.BulletDirection(Vector3.left, Vector3.zero), Vector3.right);
            AssertNear(BloodPresenter.BulletDirection(Vector3.zero, null), Vector3.forward);
        }

        [Test]
        public void TurningBloodOffShowsNothingAndClearsWhatIsShown()
        {
            var presenter = Presenter();
            Wall("Wall", new Vector3(0f, 1f, 11.5f));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] spray "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] decal "));
            presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Head), Vector3.forward);
            var spray = presenter.SprayPoolFor(_head).Items[0];
            var decal = presenter.Decals.Items[0];

            BloodPresenter.BloodEnabled = false;
            var shown = presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Head), Vector3.forward);
            presenter.Tick(Time.time);

            Assert.That(shown, Is.False);
            Assert.That(presenter.SprayPoolFor(_head).ActiveCount, Is.Zero);
            Assert.That(presenter.Decals.ActiveCount, Is.Zero);
            Assert.That(spray.Root.activeSelf, Is.False);
            Assert.That(decal.Root.activeSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AMissingCatalogWarnsOnceAndShowsNothing()
        {
            var presenter = Presenter();
            presenter.Catalog = null;

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Blood\] no HitFeedbackCatalog at Resources/Presentation/HitFeedbackCatalog"));
            Assert.That(presenter.PlayImpact(CharacterImpact(Vector3.zero, HitZone.Head), Vector3.forward), Is.False);
            Assert.That(presenter.PlayImpact(CharacterImpact(Vector3.zero, HitZone.Body), Vector3.forward), Is.False);
            presenter.Tick(Time.time + 100f);

            Assert.That(presenter.PoolRoot, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EmptySlotsSkipOnlyThatPart()
        {
            var presenter = Presenter();
            _catalog.SetSprays(null);
            _catalog.DefaultSpray = null;
            var wall = Wall("Wall", new Vector3(0f, 1f, 11.5f));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] decal "));
            var shown = presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Head), Vector3.forward);

            Assert.That(shown, Is.True, "the decal is still placed");
            Assert.That(presenter.Decals.ActiveCount, Is.EqualTo(1));
            Assert.That(presenter.DecalAnchorOf(presenter.Decals.Items[0]), Is.SameAs(wall.GetComponent<Collider>()));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ManyImpactsStayWithinThePoolCapacities()
        {
            var presenter = Presenter();
            Wall("Wall", new Vector3(0f, 1f, 11.5f));

            for (var i = 0; i < 100; i++)
            {
                presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Body), Vector3.forward);
            }

            var sprays = presenter.SprayPoolFor(_body);
            Assert.That(sprays.Count, Is.EqualTo(BloodPresenter.SprayCapacity));
            Assert.That(presenter.Decals.Count, Is.EqualTo(BloodPresenter.DecalCapacity));
            Assert.That(presenter.PoolRoot.childCount, Is.EqualTo(sprays.Count + presenter.Decals.Count), "nothing is instantiated outside the pools");
        }

        [Test]
        public void DecalsExpireAndLeaveWhenTheirWallIsGone()
        {
            var presenter = Presenter();
            var wall = Wall("Wall", new Vector3(0f, 1f, 11.5f));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] decal "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] decal "));
            presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Body), Vector3.forward);
            var now = Time.time;

            presenter.Tick(now + 1f);
            Assert.That(presenter.Decals.ActiveCount, Is.EqualTo(1), "the decal stays while its wall is there");

            wall.SetActive(false);
            presenter.Tick(now + 1f);
            Assert.That(presenter.Decals.ActiveCount, Is.Zero, "a decal does not float where its wall was");
            Assert.That(presenter.DecalAnchorOf(presenter.Decals.Items[0]), Is.Null);

            wall.SetActive(true);
            Physics.SyncTransforms();
            presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Body), Vector3.forward);
            presenter.Tick(now + _catalog.WallDecalLifetime + 1f);
            Assert.That(presenter.Decals.ActiveCount, Is.Zero, "a decal is removed after its lifetime");
            Assert.That(presenter.Decals.Count, Is.EqualTo(1), "the hidden decal is reused");
        }

        [Test]
        public void ReplacingTheDecalPrefabRebuildsTheDecalPool()
        {
            var presenter = Presenter();
            Wall("Wall", new Vector3(0f, 1f, 11.5f));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] decal "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Blood\] decal "));
            presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Body), Vector3.forward);
            var old = presenter.Decals.Items[0].Root;

            var replacement = Track(new GameObject("Replacement"));
            _catalog.WallDecal = replacement;
            presenter.PlayImpact(CharacterImpact(new Vector3(0f, 1f, 10f), HitZone.Body), Vector3.forward);

            Assert.That(old == null, Is.True, "decals made from the old prefab are destroyed");
            Assert.That(presenter.Decals.Count, Is.EqualTo(1));
            Assert.That(presenter.Decals.Items[0].Root.name, Is.EqualTo("Replacement"));
        }

        [Test]
        public void TheLogFormatsAreStable()
        {
            Assert.That(BloodPresenter.FormatSprayLog(HitZone.Head, new Vector3(1f, 2.5f, -3f)), Is.EqualTo("[Blood] spray zone=Head point=(1.00, 2.50, -3.00)"));
            Assert.That(BloodPresenter.FormatSprayLog(null, Vector3.zero), Is.EqualTo("[Blood] spray zone=none point=(0.00, 0.00, 0.00)"));
            Assert.That(BloodPresenter.FormatDecalLog(null, 0.5f), Is.EqualTo("[Blood] decal wall=(unknown) material=default distance=0.50"));
        }

        [Test]
        public void CharacterComponentsAttachTheBloodPresenter()
        {
            var body = Track(new GameObject("Character"));

            CharacterComponents.Attach(body, throwsGrenades: false);
            CharacterComponents.Attach(body, throwsGrenades: false);

            Assert.That(body.GetComponents<BloodPresenter>(), Has.Length.EqualTo(1));
        }

        private BloodPresenter Presenter()
        {
            var body = Track(new GameObject("Shooter"));
            var presenter = body.AddComponent<BloodPresenter>();
            presenter.Catalog = _catalog;
            return presenter;
        }

        private GameObject Wall(string wallName, Vector3 center)
        {
            // 1m 厚の箱。表の面は center.z - 0.5。
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.name = wallName;
            wall.transform.position = center;
            wall.transform.localScale = new Vector3(4f, 4f, 1f);
            Physics.SyncTransforms();
            return wall;
        }

        private static ImpactInfo CharacterImpact(Vector3 point, HitZone? zone)
        {
            return new ImpactInfo(point, Vector3.back, null, "flesh", true, zone);
        }

        private GameObject Effect(string effectName)
        {
            var effect = Track(new GameObject(effectName));
            var main = effect.AddComponent<ParticleSystem>().main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            return effect;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message = null)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), message ?? $"expected {expected} but was {actual}");
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
