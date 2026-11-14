using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 調整値の JSON (<see cref="KitTuning"/>) を、ファイルが無いときは既定値、壊れているときはエラーを出して既定値で読むことと、
    /// 同梱の Resources/Tuning の JSON がそのまま読めることを確かめる。
    /// </summary>
    public sealed class KitTuningTests
    {
        [TearDown]
        public void TearDown()
        {
            KitTuning.Reload();
        }

        [Test]
        public void TheBundledFilesAreReadWithoutErrors()
        {
            foreach (var path in new[] { KitTuning.GrenadeResourcePath, KitTuning.PingResourcePath })
            {
                var asset = Resources.Load<TextAsset>(path);
                Assert.That(asset, Is.Not.Null, "Resources/" + path + ".json");
            }

            KitTuning.Reload();
            var grenade = KitTuning.Grenade;
            var ping = KitTuning.Ping;

            // 壊れていれば KitTuning がエラーを出し、テストの側で失敗になる。
            Assert.That(grenade, Is.Not.Null);
            Assert.That(ping, Is.Not.Null);
            Assert.That(KitTuning.Grenade, Is.SameAs(grenade), "read once and kept");
            Assert.That(KitTuning.Ping, Is.SameAs(ping), "read once and kept");
        }

        [Test]
        public void ReadingUsesTheValuesInTheText()
        {
            var grenade = KitTuning.Read("{\"damageRadius\": 9, \"throwSpeed\": 20}", "grenade.json", TuningJson.ParseGrenade, GrenadeSpec.Default);
            var ping = KitTuning.Read("{\"lifetimeSeconds\": 12}", "ping.json", TuningJson.ParsePing, PingSpec.Default);

            Assert.That(grenade.DamageRadius, Is.EqualTo(9f));
            Assert.That(grenade.ThrowSpeed, Is.EqualTo(20f));
            Assert.That(ping.LifetimeSeconds, Is.EqualTo(12f));
        }

        [Test]
        public void AMissingFileUsesTheDefaults()
        {
            var spec = KitTuning.Read(null, "grenade.json", TuningJson.ParseGrenade, GrenadeSpec.Default);

            Assert.That(spec, Is.SameAs(GrenadeSpec.Default));
        }

        [Test]
        public void ABrokenFileLogsTheItemAndUsesTheDefaults()
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"^\[Tuning\] ping JSON 'ping\.json' item 'maxMarkers' is invalid: .*; using the default values instead$", RegexOptions.Singleline));

            var spec = KitTuning.Read("{\"maxMarkers\": 0}", "ping.json", TuningJson.ParsePing, PingSpec.Default);

            Assert.That(spec, Is.SameAs(PingSpec.Default));
        }

        [Test]
        public void ReadRejectsMissingArguments()
        {
            Assert.Throws<System.ArgumentNullException>(() => KitTuning.Read<GrenadeSpec>("{}", "g", null, GrenadeSpec.Default));
            Assert.Throws<System.ArgumentNullException>(() => KitTuning.Read("{}", "g", TuningJson.ParseGrenade, null));
        }
    }
}
