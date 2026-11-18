using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class CosmeticLooksTests
    {
        private const string Desert = "skin-desert";
        private const string Tiger = "camo-tiger";
        private const string Bare = "skin-bare";

        private static readonly CosmeticCatalog _catalog = new CosmeticCatalog(new[]
        {
            new CosmeticItem(Desert, CosmeticKind.Skin, "cosmetic.skin-desert", "body-desert"),
            new CosmeticItem(Tiger, CosmeticKind.Camo, "cosmetic.camo-tiger", "paint-tiger"),
            new CosmeticItem(Bare, CosmeticKind.Skin, "cosmetic.skin-bare", "body-missing"),
        });

        private readonly List<Object> _created = new List<Object>();
        private CosmeticVisualCatalog _visuals;
        private GameObject _desertBody;
        private Material _tigerPaint;

        [SetUp]
        public void SetUp()
        {
            CosmeticLooks.ResetOnPlay();
            _desertBody = Track(new GameObject("DesertBody"));
            _tigerPaint = Track(new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "TigerPaint" });
            _visuals = Track(ScriptableObject.CreateInstance<CosmeticVisualCatalog>());
            _visuals.Add("body-desert", _desertBody, null);
            _visuals.Add("paint-tiger", null, _tigerPaint);
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
            CosmeticLooks.ResetOnPlay();
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        [Test]
        public void ASkinIdGivesItsBodyAndACamoIdGivesItsPaint()
        {
            Assert.That(CosmeticLooks.BodyOf(_catalog, _visuals, Desert), Is.SameAs(_desertBody));
            Assert.That(CosmeticLooks.PaintOf(_catalog, _visuals, Tiger), Is.SameAs(_tigerPaint));
        }

        [Test]
        public void AnIdOfTheOtherKindOrOutsideTheCatalogGivesNothing()
        {
            Assert.That(CosmeticLooks.BodyOf(_catalog, _visuals, Tiger), Is.Null, "a camo is not a body");
            Assert.That(CosmeticLooks.PaintOf(_catalog, _visuals, Desert), Is.Null, "a skin is not a paint");
            Assert.That(CosmeticLooks.BodyOf(_catalog, _visuals, "missing"), Is.Null);
            Assert.That(CosmeticLooks.BodyOf(_catalog, _visuals, ""), Is.Null);
            Assert.That(CosmeticLooks.BodyOf(_catalog, _visuals, null), Is.Null);
            Assert.That(CosmeticLooks.BodyOf(_catalog, null, Desert), Is.Null, "without visuals the default look is kept");
            Assert.That(CosmeticLooks.BodyOf(null, _visuals, Desert), Is.Null);
        }

        [Test]
        public void ALookWithoutAVisualIsWarnedOnce()
        {
            var warnings = new List<string>();
            void Capture(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && condition.StartsWith(CosmeticLog.LogTag, System.StringComparison.Ordinal))
                {
                    warnings.Add(condition);
                }
            }

            Application.logMessageReceived += Capture;
            try
            {
                Assert.That(CosmeticLooks.BodyOf(_catalog, _visuals, Bare), Is.Null);
                Assert.That(CosmeticLooks.BodyOf(_catalog, _visuals, Bare), Is.Null);
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            Assert.That(warnings, Is.EqualTo(new[] { "[Cosmetic] no visual id=skin-bare visual=body-missing; the default look is kept" }));
        }

        [Test]
        public void TheVisualCatalogFindsByVisualIdOnly()
        {
            Assert.That(_visuals.Find("body-desert").Body, Is.SameAs(_desertBody));
            Assert.That(_visuals.FindBody("paint-tiger"), Is.Null, "the tiger entry has no body");
            Assert.That(_visuals.FindPaint("body-desert"), Is.Null);
            Assert.That(_visuals.Find("DesertBody"), Is.Null, "the asset name is not an id");
            Assert.That(_visuals.Find(null), Is.Null);
            Assert.That(CosmeticVisualCatalog.ResourcePath, Is.EqualTo("Presentation/CosmeticVisualCatalog"));
        }

        [Test]
        public void NothingIsWornWithoutAPublishedLocker()
        {
            GameSignals.Cosmetics.Clear();
            Assert.That(CosmeticLooks.WornPaint(), Is.Null);
            Assert.That(CosmeticLooks.WornId(CosmeticKind.Skin), Is.Null);
        }

        [Test]
        public void CamoPaintRestoresTheOriginalsAndSkipsEffects()
        {
            var model = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var renderer = model.GetComponent<Renderer>();
            var original = renderer.sharedMaterial;
            var flash = new GameObject("Flash");
            flash.transform.SetParent(model.transform, false);
            var line = flash.AddComponent<LineRenderer>();
            var lineMaterial = line.sharedMaterial;

            Assert.That(CamoPaint.Apply(model, null), Is.Null, "nothing is added when there is no paint");
            Assert.That(model.GetComponent<CamoPaint>(), Is.Null);

            var painter = CamoPaint.Apply(model, _tigerPaint);

            Assert.That(painter.Paint, Is.SameAs(_tigerPaint));
            Assert.That(renderer.sharedMaterial, Is.SameAs(_tigerPaint));
            Assert.That(line.sharedMaterial, Is.SameAs(lineMaterial), "the muzzle effects keep their look");

            var added = GameObject.CreatePrimitive(PrimitiveType.Cube);
            added.transform.SetParent(model.transform, false);
            var addedOriginal = added.GetComponent<Renderer>().sharedMaterial;
            CamoPaint.Apply(model, _tigerPaint);
            Assert.That(added.GetComponent<Renderer>().sharedMaterial, Is.SameAs(_tigerPaint), "a part added later is painted on the next apply");

            CamoPaint.Apply(model, null);

            Assert.That(renderer.sharedMaterial, Is.SameAs(original));
            Assert.That(added.GetComponent<Renderer>().sharedMaterial, Is.SameAs(addedOriginal));
            Assert.That(CamoPaint.Apply(null, _tigerPaint), Is.Null);
        }
    }
}
