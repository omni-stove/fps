using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// SearchAndDestroyRunner が、ラウンドの頭に参加者を出現点に並べ、設置地点での設置・落ちた爆弾の拾い・設置された爆弾の解除を
    /// 位置から Core に伝え、ラウンドと試合の終わりを扱うことを確かめる。bot (EnemyAgent) は NavMesh が要るので、
    /// ここでは Damageable と SideMember だけの参加者を使う (プレイヤーがいないので、全員がその場にいれば長押ししているとみなされる)。
    /// </summary>
    public sealed class SearchAndDestroyRunnerTests
    {
        private const float PlantSeconds = 1f;
        private const float DefuseSeconds = 1f;
        private const float FuseSeconds = 2f;

        private static readonly Vector3 SiteA = new Vector3(-14f, 0f, 20f);
        private static readonly Vector3 SiteB = new Vector3(14f, 0f, 20f);

        private readonly List<Object> _objects = new List<Object>();
        private SearchAndDestroyRunner _runner;
        private BombSite[] _sites;
        private SideMember _alphaOne;
        private SideMember _alphaTwo;
        private SideMember _bravoOne;
        private SideMember _bravoTwo;
        private SideSpawnPoint _alphaSpawnOne;
        private SideSpawnPoint _alphaSpawnTwo;
        private SideSpawnPoint _bravoSpawnOne;

        [SetUp]
        public void SetUp()
        {
            _runner = Track(new GameObject("Runner")).AddComponent<SearchAndDestroyRunner>();
            _runner.RoundSeconds = 60f;
            _runner.PlantSeconds = PlantSeconds;
            _runner.DefuseSeconds = DefuseSeconds;
            _runner.FuseSeconds = FuseSeconds;
            _runner.PreparationSeconds = 0f;
            _runner.WinsToWin = 3;
            _runner.SwapEveryRounds = 1;
            _runner.Bomb = Track(new GameObject("Bomb")).AddComponent<BombObject>();

            _sites = new[] { Site("BombSite-A", 0, SiteA), Site("BombSite-B", 1, SiteB) };
            _runner.SetSites(_sites);

            // 名前の順に ID が振られる: A-1 = 0, A-2 = 1, B-1 = 2, B-2 = 3。最初は散らばった所に立たせ、出現点に並べ直されることを見る。
            _alphaOne = Member("A-1", 0, new Vector3(5f, 0f, 5f));
            _alphaTwo = Member("A-2", 0, new Vector3(6f, 0f, 5f));
            _bravoOne = Member("B-1", 1, new Vector3(5f, 0f, 30f));
            _bravoTwo = Member("B-2", 1, new Vector3(6f, 0f, 30f));

            _alphaSpawnTwo = SpawnPoint("Spawn-0-2", 0, new Vector3(-6f, 0f, 0f));
            _alphaSpawnOne = SpawnPoint("Spawn-0-1", 0, Vector3.zero);
            _bravoSpawnOne = SpawnPoint("Spawn-1-1", 1, new Vector3(0f, 0f, 40f));
            SpawnPoint("Spawn-1-2", 1, new Vector3(-6f, 0f, 40f));
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void TryBeginFailsWithoutBombSites()
        {
            _runner.SetSites(Array.Empty<BombSite>());
            LogAssert.Expect(LogType.Error, new Regex("BombSite"));

            Assert.That(_runner.TryBegin(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void SortSitesRejectsGapsAndRepeats()
        {
            Assert.That(SearchAndDestroyRunner.SortSites(new[] { _sites[1], _sites[0] }), Is.EqualTo(_sites));

            _sites[1].Index = 0;
            Assert.Throws<InvalidOperationException>(() => SearchAndDestroyRunner.SortSites(_sites));

            _sites[1].Index = 2;
            Assert.Throws<InvalidOperationException>(() => SearchAndDestroyRunner.SortSites(_sites));
            Assert.Throws<InvalidOperationException>(() => SearchAndDestroyRunner.SortSites(new BombSite[] { null }));
        }

        [Test]
        public void EachRoundLinesTheMembersUpOnTheirOwnSpawnPoints()
        {
            Assert.That(_runner.TryBegin(), Is.True);

            Assert.That(_runner.Game.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(_alphaOne.transform.position, Is.EqualTo(_alphaSpawnOne.Position));
            Assert.That(_alphaTwo.transform.position, Is.EqualTo(_alphaSpawnTwo.Position));
            Assert.That(_bravoOne.transform.position, Is.EqualTo(_bravoSpawnOne.Position));
            Assert.That(_runner.Game.CarrierId, Is.EqualTo(_runner.IdOf(_alphaOne)));
        }

        [Test]
        public void TheCarrierPlantsOnASiteAndTheBombGoesOffForTheAttackers()
        {
            Begin();
            _alphaOne.transform.position = SiteA;

            _runner.Tick(PlantSeconds / 2f);
            Assert.That(_runner.Game.PlantingSite, Is.EqualTo(0));
            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Carried));

            _runner.Tick(PlantSeconds / 2f);
            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Planted));
            Assert.That(_runner.Game.PlantedSite, Is.EqualTo(0));
            Assert.That(_runner.BombGround, Is.EqualTo((Vector3?)SiteA));
            Assert.That(_sites[0].IsPlanted, Is.True);

            _runner.Tick(FuseSeconds);

            var game = _runner.Game;
            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.Detonated));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(game.RoundNumber, Is.EqualTo(2));
            Assert.That(game.Attacker, Is.EqualTo(Side.Bravo), "the sides swap every round");
            Assert.That(game.CarrierId, Is.EqualTo(_runner.IdOf(_bravoOne)));
            Assert.That(_alphaOne.transform.position, Is.EqualTo(_alphaSpawnOne.Position), "the next round starts from the spawn points");
            Assert.That(_sites[0].IsPlanted, Is.False);
        }

        [Test]
        public void ADefenderNextToThePlantedBombDefusesIt()
        {
            Begin();
            _alphaOne.transform.position = SiteB;
            _runner.Tick(PlantSeconds);
            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Planted));

            _bravoOne.transform.position = SiteB + new Vector3(1f, 0f, 0f);
            _runner.Tick(DefuseSeconds);

            Assert.That(_runner.Game.LastRoundEnd, Is.EqualTo(RoundEndReason.Defused));
            Assert.That(_runner.Game.GetRoundWins(Side.Bravo), Is.EqualTo(1));
        }

        [Test]
        public void TheBombFallsWhereTheCarrierDiedAndAnotherAttackerPicksItUp()
        {
            Begin();
            var fallen = new Vector3(3f, 0f, 10f);
            _alphaOne.transform.position = fallen;

            _alphaOne.Damageable.Kill();
            _runner.Tick(0f);

            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Dropped));
            Assert.That(_runner.BombGround, Is.EqualTo((Vector3?)fallen));

            _bravoOne.transform.position = fallen;
            _runner.Tick(0f);
            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Dropped), "defenders do not pick the bomb up");

            _alphaTwo.transform.position = fallen + new Vector3(0.5f, 0f, 0f);
            _runner.Tick(0f);

            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(_runner.Game.CarrierId, Is.EqualTo(_runner.IdOf(_alphaTwo)));
            Assert.That(_runner.BombGround, Is.Null);
            Assert.That(_runner.Bomb.Carrier, Is.SameAs(_alphaTwo.transform));
        }

        [Test]
        public void EliminatingTheDefendersEndsTheMatchAtWinsToWin()
        {
            _runner.WinsToWin = 1;
            var hud = BoundHudView();
            Begin(hud);

            _bravoOne.Damageable.Kill();
            _bravoTwo.Damageable.Kill();
            _runner.Tick(0f);

            Assert.That(_runner.Game.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(_runner.Game.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(hud.ResultText, Is.EqualTo("ALPHA の勝ち"));

            _runner.Tick(1f);
            Assert.That(_bravoOne.IsAlive, Is.False, "nobody comes back after the match");
        }

        [Test]
        public void TheHudShowsTheFuseAfterThePlant()
        {
            var hud = BoundHudView();
            Begin(hud);
            Assert.That(hud.TimeText, Is.EqualTo("01:00"));
            Assert.That(hud.ScoreTexts[0], Is.EqualTo("ALPHA 0 (攻撃)"));

            _alphaOne.transform.position = SiteA;
            _runner.Tick(PlantSeconds);

            Assert.That(hud.TimeText, Is.EqualTo("00:02"));
            Assert.That(hud.Lines, Has.Member("爆弾 A に設置: 起爆まで 2 秒"));
        }

        private void Begin(ModeHudView hud = null)
        {
            _runner.Begin(
                new[] { _alphaOne, _alphaTwo, _bravoOne, _bravoTwo },
                new[] { _alphaSpawnOne, _alphaSpawnTwo, _bravoSpawnOne },
                null,
                hud);
        }

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        private SideSpawnPoint SpawnPoint(string name, int side, Vector3 position)
        {
            var point = Track(new GameObject(name)).AddComponent<SideSpawnPoint>();
            point.transform.position = position;
            point.Side = new Side(side);
            return point;
        }

        private BombSite Site(string name, int index, Vector3 position)
        {
            var site = Track(new GameObject(name)).AddComponent<BombSite>();
            site.transform.position = position;
            site.Index = index;
            return site;
        }

        private ModeHudView BoundHudView()
        {
            var view = Track(new GameObject("ModeHud")).AddComponent<ModeHudView>();
            view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
            return view;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
