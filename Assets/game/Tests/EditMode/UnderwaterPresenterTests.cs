using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using Shooter.Runtime.Traversal;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 水は場面の Water に頼らず箱を直接渡し、Step を手で進める。水面は y = 2。
    public sealed class UnderwaterPresenterTests
    {
        private const float SurfaceY = 2f;

        private readonly List<Object> _created = new List<Object>();
        private readonly List<DamageReport> _reports = new List<DamageReport>();
        private GameObject _body;
        private Transform _eye;
        private Damageable _damageable;
        private UnderwaterPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Swimmer");
            _created.Add(_body);
            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize("Swimmer", 100);
            _damageable.Damaged += (_, report) => _reports.Add(report);

            _eye = new GameObject("Eye").transform;
            _eye.SetParent(_body.transform, false);
            _eye.localPosition = new Vector3(0f, 1.6f, 0f);

            _presenter = _body.AddComponent<UnderwaterPresenter>();
            _presenter.Eye = _eye;
            _presenter.Catalog = null;
            _presenter.Water = new WaterVolumes(new[]
            {
                new WaterBox(new System.Numerics.Vector3(-10f, -10f, -10f), new System.Numerics.Vector3(10f, 3f, 10f), SurfaceY),
            });
            _presenter.Swim = new SwimTuning(breathSeconds: 2f, drownDamage: 10, drownIntervalSeconds: 1f);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            _reports.Clear();
        }

        [Test]
        public void HeadAboveTheSurfaceIsNotUnderwater()
        {
            _body.transform.position = new Vector3(0f, 1f, 0f);

            _presenter.Step(5f);

            Assert.That(_presenter.IsBodyInWater, Is.True);
            Assert.That(_presenter.IsEyeUnderwater, Is.False, "the eye is at 2.6, above the surface");
            Assert.That(_damageable.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void DivingUnderTintsTheScreenWithTheDefaultColor()
        {
            _body.transform.position = new Vector3(0f, -1f, 0f);

            _presenter.Step(0.1f);

            Assert.That(_presenter.IsEyeUnderwater, Is.True);
            Assert.That(_presenter.OverlayColor, Is.EqualTo(WaterCatalog.DefaultOverlayColor));
            Assert.That(_presenter.OverlayTexture, Is.Not.Null, "without a catalog the overlay is a plain color");
            Assert.That(_presenter.OverlayTexture.width, Is.EqualTo(1));
        }

        [Test]
        public void TheCatalogImageAndColorAreUsed()
        {
            var catalog = ScriptableObject.CreateInstance<WaterCatalog>();
            var image = new Texture2D(4, 4);
            _created.Add(catalog);
            _created.Add(image);
            catalog.Overlay = image;
            catalog.OverlayColor = Color.green;
            _presenter.Catalog = catalog;

            Assert.That(_presenter.OverlayTexture, Is.SameAs(image));
            Assert.That(_presenter.OverlayColor, Is.EqualTo(Color.green));
        }

        [Test]
        public void RunningOutOfBreathDealsDrowningDamage()
        {
            LogAssert.Expect(LogType.Log, "[Water] drown target=Swimmer dmg=10 hp=90");
            LogAssert.Expect(LogType.Log, "[Water] drown target=Swimmer dmg=10 hp=80");
            _body.transform.position = new Vector3(0f, -1f, 0f);

            _presenter.Step(1.5f);
            Assert.That(_damageable.Health.Current, Is.EqualTo(100), "the breath lasts 2 seconds");
            Assert.That(_presenter.BreathFraction, Is.EqualTo(0.25f).Within(1e-5f));

            _presenter.Step(0.5f);
            Assert.That(_damageable.Health.Current, Is.EqualTo(90));
            Assert.That(_reports.Count, Is.EqualTo(1));
            Assert.That(_reports[0].Kind, Is.EqualTo(DamageKind.Drowning));
            Assert.That(_reports[0].SourcePosition, Is.Null);

            _presenter.Step(1f);
            Assert.That(_damageable.Health.Current, Is.EqualTo(80));
        }

        [Test]
        public void SurfacingRefillsTheBreath()
        {
            _body.transform.position = new Vector3(0f, -1f, 0f);
            _presenter.Step(1.5f);

            _body.transform.position = new Vector3(0f, 1f, 0f);
            _presenter.Step(0.1f);
            Assert.That(_presenter.BreathFraction, Is.EqualTo(1f));

            _body.transform.position = new Vector3(0f, -1f, 0f);
            _presenter.Step(1.5f);
            Assert.That(_damageable.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void TurningDrowningOffDealsNoDamage()
        {
            _presenter.AppliesDrowning = false;
            _body.transform.position = new Vector3(0f, -1f, 0f);

            _presenter.Step(10f);

            Assert.That(_presenter.IsEyeUnderwater, Is.True);
            Assert.That(_damageable.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void TheDeadDoNotDrownAgain()
        {
            _damageable.Kill();
            _reports.Clear();
            _body.transform.position = new Vector3(0f, -1f, 0f);

            _presenter.Step(10f);

            Assert.That(_reports, Is.Empty);
        }

        [Test]
        public void EnteringTheWaterSplashesOnTheSurface()
        {
            var splashes = new List<Vector3>();
            _presenter.Splashed += splashes.Add;

            _body.transform.position = new Vector3(3f, 5f, 4f);
            _presenter.Step(0.1f);
            _body.transform.position = new Vector3(3f, 1f, 4f);
            _presenter.Step(0.1f);
            _presenter.Step(0.1f);

            Assert.That(splashes, Is.EqualTo(new[] { new Vector3(3f, SurfaceY, 4f) }), "one splash, on the surface, when the body goes in");
        }

        [Test]
        public void StartingInTheWaterDoesNotSplash()
        {
            var splashes = new List<Vector3>();
            _presenter.Splashed += splashes.Add;
            _body.transform.position = new Vector3(0f, 1f, 0f);

            _presenter.Step(0.1f);

            Assert.That(splashes, Is.Empty);
        }

        [Test]
        public void TheLongestFrameIsStillCounted()
        {
            _body.transform.position = new Vector3(0f, -1f, 0f);
            LogAssert.Expect(LogType.Log, "[Water] drown target=Swimmer dmg=30 hp=70");

            _presenter.Step(4f);

            Assert.That(_damageable.Health.Current, Is.EqualTo(70), "out of breath at 2 s, then at 3 s and 4 s");
        }
    }
}
