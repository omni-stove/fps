using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // bot (EnemyAgent) は NavMesh が要るので、Damageable と SideMember だけの参加者を使う
    // (プレイヤーがいないので、全員がその場にいれば長押ししているとみなされる)。
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
            Begin();

            _bravoOne.Damageable.Kill();
            _bravoTwo.Damageable.Kill();
            _runner.Tick(0f);

            Assert.That(_runner.Game.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(_runner.Game.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)Side.Alpha));

            _runner.Tick(1f);
            Assert.That(_bravoOne.IsAlive, Is.False, "nobody comes back after the match");
        }

        [Test]
        public void TheStatusCarriesTheGameWithTheClockInIt()
        {
            Begin();
            var detail = (SearchAndDestroyStatus)_runner.Status.Detail;
            Assert.That(detail.Game, Is.SameAs(_runner.Game));
            Assert.That(detail.LocalPlayerId, Is.Null, "nobody plays on this screen");
            Assert.That(_runner.Status.OwnSide, Is.Null);
            Assert.That(_runner.Status.RemainingSeconds, Is.Null, "the round clock stops at the plant, so the game keeps it instead of the mode");

            _alphaOne.transform.position = SiteA;
            _runner.Tick(PlantSeconds);

            detail = (SearchAndDestroyStatus)_runner.Status.Detail;
            Assert.That(detail.Game.Bomb, Is.EqualTo(BombState.Planted));
            Assert.That(detail.Game.FuseRemaining, Is.EqualTo(FuseSeconds).Within(0.001f));
        }

        [Test]
        public void TheStatusNamesTheLocalPlayer()
        {
            _runner.Begin(
                new[] { _alphaOne, _alphaTwo, _bravoOne, _bravoTwo },
                new[] { _alphaSpawnOne, _alphaSpawnTwo, _bravoSpawnOne },
                _alphaTwo);

            var detail = (SearchAndDestroyStatus)_runner.Status.Detail;
            Assert.That(detail.LocalPlayerId, Is.EqualTo(ModeRunnerBase.LocalPlayerId));
            Assert.That(_runner.IdOf(_alphaTwo), Is.EqualTo(ModeRunnerBase.LocalPlayerId));
            Assert.That(_runner.Status.OwnSide, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void AWiredDefinitionReplacesTheInspectorValues()
        {
            var definition = Definition("SndTuning");
            definition.RoundSeconds = 90f;
            definition.PlantSeconds = PlantSeconds * 3f;
            definition.DefuseSeconds = 4f;
            definition.FuseSeconds = 7f;
            definition.WinsToWin = 2;
            definition.PreparationSeconds = 0f;
            definition.SwapEveryRounds = 0;
            _runner.Definition = definition;

            Begin();

            var spec = _runner.Game.Spec;
            Assert.That(spec.SiteCount, Is.EqualTo(2), "the scene still decides the sites");
            Assert.That(spec.RoundSeconds, Is.EqualTo(90f));
            Assert.That(spec.PlantSeconds, Is.EqualTo(PlantSeconds * 3f));
            Assert.That(spec.DefuseSeconds, Is.EqualTo(4f));
            Assert.That(spec.FuseSeconds, Is.EqualTo(7f));
            Assert.That(spec.WinsToWin, Is.EqualTo(2));
            Assert.That(spec.SwapEveryRounds, Is.EqualTo(0));

            _alphaOne.transform.position = SiteA;
            _runner.Tick(PlantSeconds);
            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Carried), "the Inspector's plant time is no longer used");

            _runner.Tick(PlantSeconds * 2f);
            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Planted));
            Assert.That(_runner.Game.FuseRemaining, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void TheDefinitionsDefuseRadiusDecidesHowFarADefenderCanDefuse()
        {
            var definition = Definition("SndWideDefuse");
            definition.PlantSeconds = PlantSeconds;
            definition.DefuseSeconds = DefuseSeconds;
            definition.PreparationSeconds = 0f;
            definition.DefuseRadius = 4f;
            _runner.Definition = definition;
            _runner.DefuseRadius = 1f;

            Begin();
            Assert.That(_runner.Game.Spec.DefuseRadius, Is.EqualTo(4f));
            _alphaOne.transform.position = SiteB;
            _runner.Tick(PlantSeconds);
            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Planted));

            _bravoOne.transform.position = SiteB + new Vector3(3f, 0f, 0f);
            _runner.Tick(DefuseSeconds);

            Assert.That(_runner.Game.LastRoundEnd, Is.EqualTo(RoundEndReason.Defused), "3 m is inside the definition's 4 m, not the Inspector's 1 m");
        }

        [Test]
        public void TheDefinitionsPickupRadiusDecidesHowFarAnAttackerPicksTheBombUp()
        {
            var definition = Definition("SndNarrowPickup");
            definition.PreparationSeconds = 0f;
            definition.BombPickupRadius = 0.25f;
            _runner.Definition = definition;

            Begin();
            var fallen = new Vector3(3f, 0f, 10f);
            _alphaOne.transform.position = fallen;
            _alphaOne.Damageable.Kill();
            _runner.Tick(0f);
            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Dropped));

            _alphaTwo.transform.position = fallen + new Vector3(0.5f, 0f, 0f);
            _runner.Tick(0f);
            Assert.That(_runner.Game.Bomb, Is.EqualTo(BombState.Dropped), "0.5 m is outside the definition's 0.25 m");

            _alphaTwo.transform.position = fallen + new Vector3(0.2f, 0f, 0f);
            _runner.Tick(0f);
            Assert.That(_runner.Game.CarrierId, Is.EqualTo(_runner.IdOf(_alphaTwo)));
        }

        [Test]
        public void TheInspectorRadiiAreUsedWithoutADefinition()
        {
            _runner.PickupRadius = 0.75f;
            _runner.DefuseRadius = 3.5f;

            Begin();

            Assert.That(_runner.Game.Spec.BombPickupRadius, Is.EqualTo(0.75f));
            Assert.That(_runner.Game.Spec.DefuseRadius, Is.EqualTo(3.5f));
        }

        [Test]
        public void TryBeginFailsWithAnInvalidDefinitionAndNamesIt()
        {
            var definition = Definition("BrokenSnd");
            definition.FuseSeconds = 0f;
            _runner.Definition = definition;
            LogAssert.Expect(LogType.Error, new Regex("BrokenSnd"));

            Assert.That(_runner.TryBegin(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void WithoutAnEconomyThereIsNoMoneyAndTheWeaponsStayAsTheyWere()
        {
            var rifle = Weapon("Rifle", TestWeapons.RifleSpec());
            AddController(_alphaOne).Definitions = new[] { rifle };
            GameSignals.ResetAll();

            BeginAsLocal(_alphaOne);

            Assert.That(_runner.EffectiveEconomy, Is.Null);
            Assert.That(_runner.RoundEconomy, Is.Null);
            Assert.That(GameSignals.Wallet.HasValue, Is.False);
            Assert.That(GameSignals.BuySender.Value, Is.Null);

            KillBy(_alphaOne, _bravoOne);
            _bravoTwo.Damageable.Kill();
            _runner.Tick(0f);

            Assert.That(_runner.Game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(GameSignals.Wallet.HasValue, Is.False);
            Assert.That(AddController(_alphaOne).Definitions, Is.EqualTo(new[] { rifle }));
        }

        [Test]
        public void WithAnEconomyKillsAndRoundsPayAndTheWalletIsPublished()
        {
            _runner.SwapEveryRounds = 0;
            _runner.Economy = Economy("SndEconomy", "{\"items\": []}");
            GameSignals.ResetAll();

            BeginAsLocal(_alphaOne);

            Assert.That(_runner.RoundEconomy, Is.Not.Null);
            Assert.That(GameSignals.BuySender.Value, Is.SameAs(_runner.RoundEconomy));
            Assert.That(GameSignals.Wallet.Value, Is.EqualTo((WalletStatus?)new WalletStatus(800, 16000, false)), "no preparation time, no buying");

            KillBy(_alphaOne, _bravoOne);
            Assert.That(GameSignals.Wallet.Value.Value.Money, Is.EqualTo(800 + 300));

            _bravoTwo.Damageable.Kill();
            _runner.Tick(0f);

            var economy = _runner.RoundEconomy.Economy;
            Assert.That(_runner.Game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(economy.MoneyOf(_runner.IdOf(_alphaOne)), Is.EqualTo(800 + 300 + 3000));
            Assert.That(economy.MoneyOf(_runner.IdOf(_alphaTwo)), Is.EqualTo(800 + 3000));
            Assert.That(economy.MoneyOf(_runner.IdOf(_bravoTwo)), Is.EqualTo(800 + 1400));
            Assert.That(GameSignals.Wallet.Value.Value.Money, Is.EqualTo(800 + 300 + 3000));

            _runner.Stop();

            Assert.That(GameSignals.Wallet.HasValue, Is.False, "stopping clears what the runner published");
            Assert.That(GameSignals.BuySender.Value, Is.Null);
        }

        [Test]
        public void AWeaponBoughtInThePreparationIsHeldWhenTheRoundStarts()
        {
            var rifle = Weapon("Rifle", TestWeapons.RifleSpec());
            var launcher = Weapon("Launcher", TestWeapons.LauncherSpec());
            AddController(_alphaOne).Definitions = new[] { rifle };
            _runner.PreparationSeconds = 5f;
            _runner.Economy = Economy(
                "SndEconomy", "{\"items\": [{\"id\": \"" + launcher.Id + "\", \"kind\": \"Weapon\", \"price\": 700, \"slot\": 0}]}", rifle, launcher);
            GameSignals.ResetAll();

            BeginAsLocal(_alphaOne);
            Assert.That(_runner.Game.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(GameSignals.Wallet.Value.Value.CanBuy, Is.True);

            GameSignals.BuySender.Value.Buy(launcher.Id);
            _runner.Tick(5f);

            Assert.That(_runner.Game.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(AddController(_alphaOne).Definitions, Is.EqualTo(new[] { launcher }));
            Assert.That(GameSignals.Wallet.Value, Is.EqualTo((WalletStatus?)new WalletStatus(100, 16000, false)));
        }

        [Test]
        public void TheDefinitionsEconomyIsUsedInsteadOfTheRunners()
        {
            var economy = Economy("SndEconomy", "{}");
            var withEconomy = Definition("SndWithEconomy");
            withEconomy.Economy = economy;
            var withoutEconomy = Definition("SndWithoutEconomy");
            _runner.Economy = Economy("RunnerEconomy", "{}");

            _runner.Definition = withEconomy;
            Assert.That(_runner.EffectiveEconomy, Is.SameAs(economy));

            _runner.Definition = withoutEconomy;
            Assert.That(_runner.EffectiveEconomy, Is.Null, "a definition without an economy turns it off");

            _runner.Definition = null;
            Assert.That(_runner.EffectiveEconomy, Is.SameAs(_runner.Economy));
        }

        [Test]
        public void TryBeginFailsWithABrokenEconomyAndNamesIt()
        {
            _runner.Economy = Economy("BrokenEconomy", "{\"startMoney\": -1}");
            LogAssert.Expect(LogType.Error, new Regex("BrokenEconomy"));

            Assert.That(_runner.TryBegin(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
        }

        private EconomyDefinition Economy(string name, string json, params WeaponDefinition[] weapons)
        {
            var economy = ScriptableObject.CreateInstance<EconomyDefinition>();
            economy.name = name;
            _objects.Add(economy);
            var tuning = new TextAsset(json) { name = name + "-json" };
            _objects.Add(tuning);
            economy.Tuning = tuning;
            if (weapons.Length > 0)
            {
                var catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
                catalog.name = name + "-weapons";
                catalog.SetWeapons(weapons);
                _objects.Add(catalog);
                economy.WeaponCatalog = catalog;
            }

            return economy;
        }

        private WeaponDefinition Weapon(string name, WeaponSpec spec)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = name;
            _objects.Add(definition);
            WeaponAssets.Configure(definition, spec);
            return definition;
        }

        private static WeaponController AddController(SideMember member)
        {
            var controller = member.GetComponentInChildren<WeaponController>(true);
            if (controller != null)
            {
                return controller;
            }

            var view = new GameObject(member.name + "-Camera");
            view.transform.SetParent(member.transform, false);
            return view.AddComponent<WeaponController>();
        }

        private static void KillBy(SideMember killer, SideMember victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "test-weapon"))
            {
                victim.Damageable.Kill();
            }
        }

        private void BeginAsLocal(SideMember localPlayer)
        {
            _runner.Begin(
                new[] { _alphaOne, _alphaTwo, _bravoOne, _bravoTwo },
                new[] { _alphaSpawnOne, _alphaSpawnTwo, _bravoSpawnOne },
                localPlayer);
        }

        private SearchAndDestroyDefinition Definition(string name)
        {
            var definition = ScriptableObject.CreateInstance<SearchAndDestroyDefinition>();
            definition.name = name;
            _objects.Add(definition);
            return definition;
        }

        private void Begin()
        {
            _runner.Begin(
                new[] { _alphaOne, _alphaTwo, _bravoOne, _bravoTwo },
                new[] { _alphaSpawnOne, _alphaSpawnTwo, _bravoSpawnOne },
                null);
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

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
