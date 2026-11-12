using System;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CampaignSaveTests
    {
        private const string _valid =
            "{'version':1,'missionId':'m01','checkpointId':'cp2','health':75,'ammoInMagazine':12,'reserveAmmo':60," +
            "'objectives':[{'id':'kills','state':'active','count':2},{'id':'exit','state':'locked','count':0}]}";

        private static readonly string[] _killsAndExit = { "kills", "exit" };
        private static readonly string[] _killsOnly = { "kills" };

        [Test]
        public void ConstructorKeepsValues()
        {
            var save = Sample();

            Assert.That(save.Version, Is.EqualTo(CampaignSave.CurrentVersion));
            Assert.That(save.MissionId, Is.EqualTo("m01"));
            Assert.That(save.CheckpointId, Is.EqualTo("cp2"));
            Assert.That(save.Health, Is.EqualTo(75));
            Assert.That(save.AmmoInMagazine, Is.EqualTo(12));
            Assert.That(save.ReserveAmmo, Is.EqualTo(60));
            Assert.That(save.Objectives.Select(o => o.Id), Is.EqualTo(_killsAndExit));
        }

        [Test]
        public void WritesVersionFirst()
        {
            Assert.That(Sample().ToJson(), Does.StartWith("{\"version\":1,"));
        }

        [Test]
        public void RoundTripsThroughJson()
        {
            var save = Sample();

            AssertSame(CampaignSave.FromJson(save.ToJson()), save);
        }

        [Test]
        public void RoundTripsAwkwardStrings()
        {
            var save = new CampaignSave(
                "m \"quoted\" \\ back/slash",
                "改行\nタブ\t制御\u0001\u001f絵文字😀",
                1,
                0,
                int.MaxValue,
                new[] { new ObjectiveProgress("目標 \"1\"", ObjectiveState.Failed, 0) });

            AssertSame(CampaignSave.FromJson(save.ToJson()), save);
        }

        [Test]
        public void ReadsHandWrittenJsonWithWhitespaceAndEscapes()
        {
            var json = "\n{ \"version\" : 1 ,\r\n \"missionId\" : \"m\\u0030\\/1\", \"checkpointId\": \"\",\t" +
                "\"health\": 100, \"ammoInMagazine\": 0, \"reserveAmmo\": 0, \"objectives\": [ ] }\n";

            var save = CampaignSave.FromJson(json);

            Assert.That(save.MissionId, Is.EqualTo("m0/1"));
            Assert.That(save.CheckpointId, Is.Empty);
            Assert.That(save.Objectives, Is.Empty);
        }

        [Test]
        public void NullCheckpointMeansNone()
        {
            var save = new CampaignSave("m", null, 1, 0, 0, Array.Empty<ObjectiveProgress>());

            Assert.That(save.CheckpointId, Is.Empty);
            Assert.That(CampaignSave.FromJson(save.ToJson()).CheckpointId, Is.Empty);
        }

        [Test]
        public void MissionSurvivesSaveAndLoad()
        {
            var objectives = new[]
            {
                new Objective("kills", "", 3),
                new Objective("exit", "", prerequisites: _killsOnly),
                new Objective("bonus", "", optional: true),
            };
            var mission = new Mission(objectives);
            mission.Progress("kills", 3);
            mission.Fail("bonus");
            var json = new CampaignSave("m01", "cp1", 50, 3, 30, mission.Snapshot()).ToJson();

            var loaded = new Mission(objectives);
            loaded.Restore(CampaignSave.FromJson(json).Objectives);

            Assert.That(loaded.GetState("kills"), Is.EqualTo(ObjectiveState.Completed));
            Assert.That(loaded.GetCount("kills"), Is.EqualTo(3));
            Assert.That(loaded.GetState("exit"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(loaded.GetState("bonus"), Is.EqualTo(ObjectiveState.Failed));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(-1)]
        public void RejectsOtherVersions(int version)
        {
            var json = Json(_valid.Replace("'version':1", "'version':" + version, StringComparison.Ordinal));

            var e = Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
            Assert.That(e.Message, Does.Contain("version"));
        }

        [Test]
        public void ChecksVersionBeforeOtherFields()
        {
            var e = Assert.Throws<FormatException>(() => CampaignSave.FromJson(Json("{'version':2,'slots':[]}")));
            Assert.That(e.Message, Does.Contain("version"));
        }

        [Test]
        public void ValidSampleParses()
        {
            Assert.That(CampaignSave.FromJson(Json(_valid)).Objectives, Has.Count.EqualTo(2));
        }

        // JSON として壊れている
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("{'version':1")]
        [TestCase("{'version':1,}")]
        [TestCase("{version:1}")]
        [TestCase("{'version' 1}")]
        [TestCase("{'version':01}")]
        [TestCase("{'version':1.0}")]
        [TestCase("{'version':1e0}")]
        [TestCase("{'version':-}")]
        [TestCase("{'version':tru}")]
        [TestCase("{'version':1}{}")]
        [TestCase("{'version':1} x")]
        [TestCase("{'version':1,'missionId':'a\\x'}")]
        [TestCase("{'version':1,'missionId':'a\\u12'}")]
        [TestCase("{'version':1,'missionId':'a\\u12zz'}")]
        [TestCase("{'version':1,'missionId':'unterminated}")]
        [TestCase("{'version':1,'missionId':'raw\nnewline'}")]
        [TestCase("{'version':1,'version':1}")]
        [TestCase("{'version':99999999999999999999}")]
        // 形が違う
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("1")]
        [TestCase("{}")]
        [TestCase("{'version':'1'}")]
        [TestCase("{'version':4294967297}")]
        public void RejectsBrokenData(string text)
        {
            var json = Json(text);

            Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
        }

        // 項目が欠けている・余分・型違い・範囲外
        [TestCase("'missionId':'m01',", "")]
        [TestCase("'missionId':'m01'", "'missionId':''")]
        [TestCase("'missionId':'m01'", "'missionId':null")]
        [TestCase("'checkpointId':'cp2'", "'checkpointId':2")]
        [TestCase("'health':75", "'health':'75'")]
        [TestCase("'health':75", "'health':0")]
        [TestCase("'health':75", "'health':true")]
        [TestCase("'ammoInMagazine':12", "'ammoInMagazine':-1")]
        [TestCase("'reserveAmmo':60", "'reserveAmmo':2147483648")]
        [TestCase("'reserveAmmo':60,", "'reserveAmmo':60,'extra':1,")]
        [TestCase("'objectives':[", "'objectives':{},'x':[")]
        [TestCase("[{'id':'kills'", "[1,{'id':'kills'")]
        [TestCase("'state':'active'", "'state':'done'")]
        [TestCase("'state':'active'", "'state':'Active'")]
        [TestCase("'state':'active'", "'state':1")]
        [TestCase("'count':2", "'count':-2")]
        [TestCase("'count':2", "'count':2,'note':''")]
        [TestCase("'id':'exit'", "'id':'kills'")]
        [TestCase("'id':'exit'", "'id':''")]
        [TestCase("{'id':'exit','state':'locked','count':0}", "{'id':'exit','state':'locked'}")]
        public void RejectsInvalidFields(string from, string to)
        {
            Assert.That(_valid.Contains(from, StringComparison.Ordinal), Is.True);
            var json = Json(_valid.Replace(from, to, StringComparison.Ordinal));

            Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
        }

        [Test]
        public void RejectsDeepNesting()
        {
            var json = "{\"version\":1,\"x\":" + new string('[', 10000) + new string(']', 10000) + "}";

            Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
        }

        [Test]
        public void RejectsNullInput()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignSave.FromJson(null));
        }

        [Test]
        public void ConstructorRejectsInvalidValues()
        {
            var none = Array.Empty<ObjectiveProgress>();

            Assert.Throws<ArgumentNullException>(() => new CampaignSave(null, "", 1, 0, 0, none));
            Assert.Throws<ArgumentException>(() => new CampaignSave("", "", 1, 0, 0, none));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSave("m", "", 0, 0, 0, none));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSave("m", "", 1, -1, 0, none));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSave("m", "", 1, 0, -1, none));
            Assert.Throws<ArgumentNullException>(() => new CampaignSave("m", "", 1, 0, 0, null));
            Assert.Throws<ArgumentException>(() => new CampaignSave("m", "", 1, 0, 0, new ObjectiveProgress[] { null }));
            Assert.Throws<ArgumentException>(() => new CampaignSave("m", "", 1, 0, 0, new[]
            {
                new ObjectiveProgress("a", ObjectiveState.Active, 0),
                new ObjectiveProgress("a", ObjectiveState.Active, 0),
            }));
        }

        private static CampaignSave Sample()
        {
            return new CampaignSave("m01", "cp2", 75, 12, 60, new[]
            {
                new ObjectiveProgress("kills", ObjectiveState.Active, 2),
                new ObjectiveProgress("exit", ObjectiveState.Locked, 0),
            });
        }

        /// <summary>読みやすさのため ' で書いた JSON を " に直す。</summary>
        private static string Json(string text)
        {
            return text.Replace('\'', '"');
        }

        private static void AssertSame(CampaignSave actual, CampaignSave expected)
        {
            Assert.That(actual.Version, Is.EqualTo(expected.Version));
            Assert.That(actual.MissionId, Is.EqualTo(expected.MissionId));
            Assert.That(actual.CheckpointId, Is.EqualTo(expected.CheckpointId));
            Assert.That(actual.Health, Is.EqualTo(expected.Health));
            Assert.That(actual.AmmoInMagazine, Is.EqualTo(expected.AmmoInMagazine));
            Assert.That(actual.ReserveAmmo, Is.EqualTo(expected.ReserveAmmo));
            Assert.That(
                actual.Objectives.Select(o => o.Id + ":" + o.State + ":" + o.Count),
                Is.EqualTo(expected.Objectives.Select(o => o.Id + ":" + o.State + ":" + o.Count)));
        }
    }
}
