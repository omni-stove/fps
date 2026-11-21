using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 場の Damageable を探す代わりに Candidates で体を渡し、ほかのテストが残した体に左右されないようにする。
    public sealed class ThermalHighlighterTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<Damageable> _bodies = new List<Damageable>();
        private GameObject _player;
        private VisionModeController _controller;
        private ThermalHighlighter _highlighter;
        private Material _original;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _player = new GameObject("ThermalHighlighterTests.Player");
            _created.Add(_player);
            _player.AddComponent<SideMember>().Side = new Side(0);
            _controller = _player.AddComponent<VisionModeController>();
            _controller.Catalog = null;
            _controller.SightSource = () => SightView.None;
            _highlighter = _player.AddComponent<ThermalHighlighter>();
            _highlighter.Catalog = null;
            _highlighter.Candidates = () => _bodies;
            _highlighter.Controller = _controller;

            _original = new Material(Shader.Find(ThermalHighlighter.CodeShaderName)) { name = "Original" };
            _created.Add(_original);
        }

        [TearDown]
        public void TearDown()
        {
            // Edit Mode では OnDestroy が走らないので、コードで作った材質はここで消す。
            if (_highlighter != null && _highlighter.CodeMaterial != null)
            {
                Object.DestroyImmediate(_highlighter.CodeMaterial);
            }

            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            _bodies.Clear();
            GameSignals.ResetAll();
        }

        [Test]
        public void ThermalGogglesLightUpTheEnemyAndPutItBackWhenTakenOff()
        {
            var enemy = Body("Enemy", 1, out var renderer);

            _controller.SetGoggles(VisionMode.Thermal);

            Assert.That(_highlighter.IsActive, Is.True);
            Assert.That(_highlighter.IsHighlighted(enemy), Is.True);
            Assert.That(renderer.sharedMaterial, Is.SameAs(_highlighter.CodeMaterial), "no catalog material: the code-built glow");
            Assert.That(_highlighter.CodeMaterial.color, Is.EqualTo(VisionVisualCatalog.DefaultThermalHighlightColor));

            _controller.SetGoggles(VisionMode.None);

            Assert.That(_highlighter.IsActive, Is.False);
            Assert.That(_highlighter.HighlightedCount, Is.Zero);
            Assert.That(renderer.sharedMaterial, Is.SameAs(_original));
        }

        [Test]
        public void AlliesAndTheWearerAreNotLitUp()
        {
            var ally = Body("Ally", 0, out var allyRenderer);
            var self = _player.AddComponent<Damageable>();
            self.Initialize("player", 100);
            var selfRenderer = AddRenderer(_player.transform, "PlayerBody");
            _bodies.Add(self);
            var enemy = Body("Enemy", 1, out _);

            _controller.SetGoggles(VisionMode.Thermal);

            Assert.That(_highlighter.IsHighlighted(ally), Is.False);
            Assert.That(allyRenderer.sharedMaterial, Is.SameAs(_original));
            Assert.That(_highlighter.IsHighlighted(self), Is.False);
            Assert.That(selfRenderer.sharedMaterial, Is.SameAs(_original));
            Assert.That(_highlighter.IsHighlighted(enemy), Is.True);
            Assert.That(_highlighter.HighlightedCount, Is.EqualTo(1));
        }

        [Test]
        public void NightVisionDoesNotLightAnyoneUp()
        {
            var enemy = Body("Enemy", 1, out var renderer);

            _controller.SetGoggles(VisionMode.Night);

            Assert.That(_highlighter.IsActive, Is.False);
            Assert.That(_highlighter.IsHighlighted(enemy), Is.False);
            Assert.That(renderer.sharedMaterial, Is.SameAs(_original));
        }

        [Test]
        public void AThermalScopeLightsUpOnlyWhileAimedIn()
        {
            var enemy = Body("Enemy", 1, out var renderer);
            var sight = SightView.Of(new SightSpec(visionMode: VisionMode.Thermal), ViewMode.FirstPerson, 1f);
            _controller.SightSource = () => sight;

            _controller.Refresh();
            Assert.That(_highlighter.IsHighlighted(enemy), Is.True);

            sight = SightView.None;
            _controller.Refresh();
            Assert.That(renderer.sharedMaterial, Is.SameAs(_original));
        }

        [Test]
        public void TheCatalogMaterialIsUsedOnEverySlot()
        {
            var glow = new Material(Shader.Find(ThermalHighlighter.CodeShaderName)) { name = "Glow" };
            _created.Add(glow);
            var catalog = ScriptableObject.CreateInstance<VisionVisualCatalog>();
            _created.Add(catalog);
            catalog.ThermalMaterial = glow;
            _highlighter.Catalog = catalog;
            var second = new Material(_original) { name = "Second" };
            _created.Add(second);
            Body("Enemy", 1, out var renderer);
            renderer.sharedMaterials = new[] { _original, second };

            _controller.SetGoggles(VisionMode.Thermal);

            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { glow, glow }));
            Assert.That(_highlighter.CodeMaterial, Is.Null, "nothing is built when the catalog has a material");

            _controller.SetGoggles(VisionMode.None);

            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { _original, second }));
        }

        [Test]
        public void RescanningPicksUpNewEnemiesAndPutsBackTheDead()
        {
            var first = Body("First", 1, out var firstRenderer);
            _controller.SetGoggles(VisionMode.Thermal);
            Assert.That(_highlighter.HighlightedCount, Is.EqualTo(1));

            var second = Body("Second", 1, out _);
            first.Kill();

            Assert.That(_highlighter.Rescan(), Is.EqualTo(1));
            Assert.That(_highlighter.IsHighlighted(first), Is.False, "the dead body cools down");
            Assert.That(firstRenderer.sharedMaterial, Is.SameAs(_original));
            Assert.That(_highlighter.IsHighlighted(second), Is.True);
        }

        [Test]
        public void ABodyThatWasDestroyedIsDroppedWithoutErrors()
        {
            var enemy = Body("Enemy", 1, out _);
            _controller.SetGoggles(VisionMode.Thermal);

            Object.DestroyImmediate(enemy.gameObject);
            _bodies.Clear();

            Assert.That(_highlighter.Rescan(), Is.Zero);
            _controller.SetGoggles(VisionMode.None);
            Assert.That(_highlighter.HighlightedCount, Is.Zero);
        }

        [Test]
        public void DyingWithThermalGogglesPutsTheEnemiesBack()
        {
            var self = _player.AddComponent<Damageable>();
            self.Initialize("player", 100);
            Body("Enemy", 1, out var renderer);
            _controller.SetGoggles(VisionMode.Thermal);

            self.Kill();

            Assert.That(_highlighter.IsActive, Is.False);
            Assert.That(renderer.sharedMaterial, Is.SameAs(_original));
        }

        [Test]
        public void BodiesWithoutADamageableAreLitWhileTheSourceReturnsThem()
        {
            var bodies = new List<GameObject>();
            _highlighter.Bodies = () => bodies;
            var remote = PlainBody("Remote", out var renderer);
            bodies.Add(remote);

            _controller.SetGoggles(VisionMode.Thermal);

            Assert.That(_highlighter.IsHighlighted(remote), Is.True);
            Assert.That(renderer.sharedMaterial, Is.SameAs(_highlighter.CodeMaterial));
            Assert.That(_highlighter.HighlightedCount, Is.EqualTo(1));

            bodies.Clear();

            Assert.That(_highlighter.Rescan(), Is.Zero, "a body the source stops returning (now an ally or dead) cools down");
            Assert.That(_highlighter.IsHighlighted(remote), Is.False);
            Assert.That(renderer.sharedMaterial, Is.SameAs(_original));
        }

        [Test]
        public void AHiddenBodyIsPutBackAndLitAgainWhenItShowsUp()
        {
            var remote = PlainBody("Remote", out var renderer);
            _highlighter.Bodies = () => new[] { remote };
            _controller.SetGoggles(VisionMode.Thermal);

            remote.SetActive(false);
            Assert.That(_highlighter.Rescan(), Is.Zero);
            Assert.That(renderer.sharedMaterial, Is.SameAs(_original));

            remote.SetActive(true);
            Assert.That(_highlighter.Rescan(), Is.EqualTo(1));
            Assert.That(renderer.sharedMaterial, Is.SameAs(_highlighter.CodeMaterial));
        }

        [Test]
        public void TakingThermalOffPutsBackBothKindsOfBodies()
        {
            Body("Enemy", 1, out var enemyRenderer);
            var remote = PlainBody("Remote", out var remoteRenderer);
            _highlighter.Bodies = () => new[] { remote };

            _controller.SetGoggles(VisionMode.Thermal);
            Assert.That(_highlighter.HighlightedCount, Is.EqualTo(2));

            _controller.SetGoggles(VisionMode.None);

            Assert.That(_highlighter.HighlightedCount, Is.Zero);
            Assert.That(enemyRenderer.sharedMaterial, Is.SameAs(_original));
            Assert.That(remoteRenderer.sharedMaterial, Is.SameAs(_original));
        }

        [Test]
        public void ADestroyedBodyAndTheWearersOwnChildrenAreSkipped()
        {
            var remote = PlainBody("Remote", out _);
            var own = new GameObject("OwnChild");
            own.transform.SetParent(_player.transform, false);
            var ownRenderer = AddRenderer(own.transform, "OwnMesh");
            _highlighter.Bodies = () => new[] { remote, own, null };

            _controller.SetGoggles(VisionMode.Thermal);
            Assert.That(_highlighter.IsHighlighted(own), Is.False);
            Assert.That(ownRenderer.sharedMaterial, Is.SameAs(_original));

            Object.DestroyImmediate(remote);
            Assert.That(_highlighter.Rescan(), Is.Zero);
            _controller.SetGoggles(VisionMode.None);
            Assert.That(_highlighter.HighlightedCount, Is.Zero);
        }

        [Test]
        public void TheCatalogDefaultsCoverTheHighlight()
        {
            var catalog = ScriptableObject.CreateInstance<VisionVisualCatalog>();
            _created.Add(catalog);

            Assert.That(catalog.ThermalMaterial, Is.Null);
            Assert.That(catalog.ThermalHighlightColor, Is.EqualTo(VisionVisualCatalog.DefaultThermalHighlightColor));
            Assert.That(catalog.ThermalRescanSeconds, Is.EqualTo(VisionVisualCatalog.DefaultThermalRescanSeconds));
            Assert.That(VisionVisualCatalog.ThermalRescanSecondsOf(null), Is.EqualTo(VisionVisualCatalog.DefaultThermalRescanSeconds));

            catalog.ThermalRescanSeconds = 0f;
            Assert.That(catalog.ThermalRescanSeconds, Is.EqualTo(VisionVisualCatalog.MinThermalRescanSeconds), "never every frame");
            catalog.ThermalHighlightColor = Color.cyan;
            Assert.That(VisionVisualCatalog.ThermalHighlightColorOf(catalog), Is.EqualTo(Color.cyan));
            Assert.That(VisionVisualCatalog.SwitchVolumeOf(null), Is.EqualTo(VisionVisualCatalog.DefaultSwitchVolume));
            Assert.That(VisionVisualCatalog.SwitchSoundFor(null, true), Is.Null);
        }

        private Damageable Body(string name, int side, out MeshRenderer renderer)
        {
            var body = new GameObject(name);
            _created.Add(body);
            body.AddComponent<SideMember>().Side = new Side(side);
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, 100);
            renderer = AddRenderer(body.transform, name + "Mesh");
            _bodies.Add(damageable);
            return damageable;
        }

        private GameObject PlainBody(string name, out MeshRenderer renderer)
        {
            var body = new GameObject(name);
            _created.Add(body);
            renderer = AddRenderer(body.transform, name + "Mesh");
            return body;
        }

        private MeshRenderer AddRenderer(Transform parent, string name)
        {
            var mesh = new GameObject(name);
            mesh.transform.SetParent(parent, false);
            mesh.AddComponent<MeshFilter>();
            var renderer = mesh.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _original;
            return renderer;
        }
    }
}
