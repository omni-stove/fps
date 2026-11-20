using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class NameplateTuningFileTests
    {
        private GameContent _content;
        private TextAsset _asset;

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            if (_asset != null)
            {
                Object.DestroyImmediate(_asset);
            }

            if (_content != null)
            {
                Object.DestroyImmediate(_content);
            }

            KitTuning.Reload();
        }

        [Test]
        public void TheBundledFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>(NameplateJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/nameplates.json");

            var spec = NameplateJson.Parse(asset.text, "Resources/Tuning/nameplates.json");

            Assert.That(spec.FriendlyRange, Is.EqualTo(NameplateSpec.DefaultFriendlyRange));
            Assert.That(spec.EnemyRange, Is.EqualTo(NameplateSpec.DefaultEnemyRange));
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.NameplatesResourcePath, Is.EqualTo("Tuning/nameplates"));
            Assert.That(KitTuning.Nameplates.FriendlyRange, Is.EqualTo(60f));
            Assert.That(KitTuning.Nameplates.EnemyRange, Is.EqualTo(35f));
        }

        [Test]
        public void TheGameContentFileIsReadBeforeResources()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Nameplates";
            _asset = new TextAsset("{\"friendlyRange\": 25, \"enemyRange\": 12}") { name = "nameplates" };
            _content.NameplatesTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Nameplates' (nameplates)");
            var spec = KitTuning.Nameplates;

            Assert.That(spec.FriendlyRange, Is.EqualTo(25f));
            Assert.That(spec.EnemyRange, Is.EqualTo(12f));
        }

        [Test]
        public void ABrokenFileFallsBackToTheDefaults()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Broken";
            _asset = new TextAsset("{\"enemyRange\": -3}") { name = "nameplates" };
            _content.NameplatesTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Broken' (nameplates)");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"(?s)^\[Tuning\] nameplates JSON .* item 'enemyRange' is invalid: .*; using the default values instead$"));
            var spec = KitTuning.Nameplates;

            Assert.That(spec, Is.SameAs(NameplateSpec.Default));
        }
    }
}
