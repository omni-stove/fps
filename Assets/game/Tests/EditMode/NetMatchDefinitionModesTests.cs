using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class NetMatchDefinitionModesTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private NetMatchDefinition _match;
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            _match = Make<NetMatchDefinition>();
            _content = Make<GameContent>();
            GameContent.Use(_content);
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void NothingWiredPlaysTheCoreDefaultsUnderTheMapsWeatherAndTime()
        {
            var settings = _match.ToSettings();

            Assert.That(settings.WeatherId, Is.Null);
            Assert.That(settings.TimeOfDayId, Is.Null);
            Assert.That(settings.CaptureTheFlag, Is.SameAs(CaptureTheFlagSpec.Default));
            Assert.That(settings.Hardpoint, Is.SameAs(HardpointSpec.Default));
            Assert.That(settings.SearchAndDestroy, Is.SameAs(SearchAndDestroySpec.Default));
            Assert.That(settings.FreeForAll, Is.SameAs(FreeForAllSpec.Default));
            Assert.That(settings.KillConfirmed, Is.SameAs(KillConfirmedSpec.Default));
            Assert.That(settings.GunGame, Is.SameAs(GunGameSpec.Default));
        }

        [Test]
        public void TheWeatherAndTimeOfDayIdsAreCarried()
        {
            _match.WeatherId = "rain";
            _match.TimeOfDayId = "dusk";

            var settings = _match.ToSettings();

            Assert.That(settings.WeatherId, Is.EqualTo("rain"));
            Assert.That(settings.TimeOfDayId, Is.EqualTo("dusk"));
        }

        [TestCase("rain storm")]
        [TestCase("rain/../x")]
        public void AnIdThatIsNotLettersDigitsOrDashesIsRefused(string id)
        {
            _match.WeatherId = id;

            var e = Assert.Throws<ArgumentException>(() => _match.ToSettings());

            Assert.That(e.ParamName, Is.EqualTo("weatherId"));
        }

        [Test]
        public void TheWiredModeAssetsAreRead()
        {
            var ctf = Make<CaptureTheFlagDefinition>();
            ctf.CopyFrom(new CaptureTheFlagSpec(scoreLimit: 5, returnSeconds: 12f));
            var hardpoint = Make<HardpointDefinition>();
            hardpoint.RotateSeconds = 45f;
            hardpoint.ScoreLimit = 150;
            var snd = Make<SearchAndDestroyDefinition>();
            snd.CopyFrom(new SearchAndDestroySpec(winsToWin: 6, fuseSeconds: 40f));
            var ffa = Make<FreeForAllDefinition>();
            ffa.ScoreLimit = 25;
            ffa.PointsPerKill = 2;
            var kc = Make<KillConfirmedDefinition>();
            kc.CopyFrom(new KillConfirmedSpec(30, 420f, 2f, 20f, 3, 1.5f));
            var ladder = Make<GunGameLadderDefinition>();
            ladder.TimeLimitSeconds = 480f;
            ladder.RespawnSeconds = 1f;
            ladder.DemotesOnMelee = false;
            _match.CaptureTheFlag = ctf;
            _match.Hardpoint = hardpoint;
            _match.SearchAndDestroy = snd;
            _match.FreeForAll = ffa;
            _match.KillConfirmed = kc;
            _match.GunGameLadder = ladder;

            var settings = _match.ToSettings();

            Assert.That(settings.CaptureTheFlag.ScoreLimit, Is.EqualTo(5));
            Assert.That(settings.CaptureTheFlag.ReturnSeconds, Is.EqualTo(12f));
            Assert.That(settings.Hardpoint.RotateSeconds, Is.EqualTo(45f));
            Assert.That(settings.Hardpoint.ScoreLimit, Is.EqualTo(150));
            Assert.That(settings.Hardpoint.PointCount, Is.EqualTo(HardpointSpec.MinPointCount));
            Assert.That(settings.SearchAndDestroy.WinsToWin, Is.EqualTo(6));
            Assert.That(settings.SearchAndDestroy.FuseSeconds, Is.EqualTo(40f));
            Assert.That(settings.SearchAndDestroy.SiteCount, Is.EqualTo(SearchAndDestroySpec.DefaultSiteCount));
            Assert.That(settings.FreeForAll.ScoreLimit, Is.EqualTo(25));
            Assert.That(settings.FreeForAll.PointsPerKill, Is.EqualTo(2));
            Assert.That(settings.KillConfirmed.TagLifetimeSeconds, Is.EqualTo(20f));
            Assert.That(settings.KillConfirmed.PointsPerConfirm, Is.EqualTo(3));
            Assert.That(settings.GunGame.TimeLimitSeconds, Is.EqualTo(480f));
            Assert.That(settings.GunGame.RespawnSeconds, Is.EqualTo(1f));
            Assert.That(settings.GunGame.DemotesOnMelee, Is.False);
            Assert.That(settings.GunGame.TierWeaponIds, Is.Empty, "a ladder without tiers still exports the rules that do not play gun game");
        }

        [Test]
        public void TheGameContentModeAssetsAreReadWhenNothingIsWired()
        {
            var ffa = Make<FreeForAllDefinition>();
            ffa.ScoreLimit = 40;
            var kc = Make<KillConfirmedDefinition>();
            kc.CopyFrom(new KillConfirmedSpec(12, 200f, 1f, 9f));
            _content.FreeForAll = ffa;
            _content.KillConfirmed = kc;

            var settings = _match.ToSettings();

            Assert.That(settings.FreeForAll.ScoreLimit, Is.EqualTo(40));
            Assert.That(settings.KillConfirmed.ScoreLimit, Is.EqualTo(12));
            Assert.That(settings.KillConfirmed.TagLifetimeSeconds, Is.EqualTo(9f));
        }

        [Test]
        public void TheWiredModeAssetWinsOverGameContent()
        {
            var wired = Make<FreeForAllDefinition>();
            wired.ScoreLimit = 11;
            var content = Make<FreeForAllDefinition>();
            content.ScoreLimit = 22;
            _match.FreeForAll = wired;
            _content.FreeForAll = content;

            Assert.That(_match.ToSettings().FreeForAll.ScoreLimit, Is.EqualTo(11));
        }

        [Test]
        public void AnInvalidModeAssetStopsTheSettings()
        {
            var ffa = Make<FreeForAllDefinition>();
            ffa.ScoreLimit = 0;
            ffa.name = "BrokenFreeForAll";
            _match.FreeForAll = ffa;

            var e = Assert.Throws<InvalidOperationException>(() => _match.ToSettings());

            Assert.That(e.Message, Does.Contain("BrokenFreeForAll").And.Contain("scoreLimit"));
        }

        [Test]
        public void TheNewObjectivesFollowTheOlderOnesInTheEnum()
        {
            Assert.That((int)MatchObjective.CaptureTheFlag, Is.EqualTo(3));
            Assert.That((int)MatchObjective.GunGame, Is.EqualTo(8));
        }

        private T Make<T>()
            where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            _objects.Add(made);
            return made;
        }
    }
}
