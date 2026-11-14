using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="HordeRunner"/> が波を越えたら勝ち・全員倒れたら負けにし、倒れた味方を波の間に戻し、敵を倒すと通貨を入れ、
    /// 通貨で防衛物 (<see cref="HordeFortification"/>) を直す・置くことを確かめる。
    /// WaveSpawner は NavMesh が要るので使わず、波の予定に敵を出した・倒したことを直接伝えて <see cref="HordeRunner.RewardEnemyKill"/> を呼ぶ。
    /// </summary>
    public sealed class HordeRunnerTests
    {
        private const float RestSeconds = 5f;
        private const int StartingCurrency = 20;
        private const int KillReward = 10;
        private const int RepairCostPerPiece = 5;
        private const int BuildCost = 30;

        private readonly List<Object> _objects = new List<Object>();
        private HordeRunner _runner;
        private HordeDefinition _definition;
        private WaveSchedule _schedule;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<HordeDefinition>();
            _definition.name = "TestHorde";
            _objects.Add(_definition);
            _definition.StartingCurrency = StartingCurrency;
            _definition.KillReward = KillReward;
            _definition.KillRewardPerWave = 0;

            _runner = Track(new GameObject("Runner")).AddComponent<HordeRunner>();
            _runner.Definition = _definition;
            _schedule = WaveSchedule.FromTable(new[] { new Wave(2, 1f, 2, RestSeconds), new Wave(2, 1f, 2, RestSeconds) });
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
        public void AFallenAllyComesBackOnceTheWaveIsClearedAndKillsPayCurrency()
        {
            var player = Member("Player", Vector3.zero);
            var ally = Member("Bot-0-1", new Vector3(3f, 0f, 0f));
            var hud = BoundHudView();
            _runner.BeginHorde(_schedule, new[] { player, ally }, new[] { SpawnPoint(new Vector3(0f, 0f, -2f)) }, player, hud);

            Assert.That(_schedule.Phase, Is.EqualTo(WavePhase.Fighting), "the first wave starts with the match");
            Assert.That(hud.Lines, Does.Contain(Localizer.Resolve(HordeText.Wave(1, 2))).And.Contain(Localizer.Resolve(HordeText.RemainingEnemies(2))));

            ally.Damageable.Kill();
            _runner.Tick(30f);
            Assert.That(ally.IsAlive, Is.False, "nobody comes back while the wave is being fought");
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing), "the player is still standing");

            ClearWave();
            _runner.Tick(0f);

            Assert.That(ally.IsAlive, Is.True);
            Assert.That(_runner.Horde.Currency, Is.EqualTo(StartingCurrency + (KillReward * 2)));
            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.EqualTo(2d), "the score counts the enemies killed");
            Assert.That(hud.Lines, Does.Contain(Localizer.Resolve(HordeText.NextWave(RestSeconds)))
                .And.Contain(Localizer.Resolve(HordeText.Currency(StartingCurrency + (KillReward * 2)))));
        }

        [Test]
        public void ClearingEveryWaveWins()
        {
            var player = Member("Player", Vector3.zero);
            var hud = BoundHudView();
            _runner.BeginHorde(_schedule, new[] { player }, new SideSpawnPoint[0], player, hud);

            ClearWave();
            _schedule.Tick(RestSeconds);
            ClearWave();
            _runner.Tick(0f);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.Horde.IsVictory, Is.True);
            Assert.That(hud.Lines, Does.Contain(Localizer.Resolve(HordeText.Victory)));
            Assert.That(hud.ResultText, Is.EqualTo("ALPHA の勝ち"));
        }

        [Test]
        public void WhenEveryoneIsDownTheHordeWins()
        {
            var player = Member("Player", Vector3.zero);
            var ally = Member("Bot-0-1", new Vector3(3f, 0f, 0f));
            var hud = BoundHudView();
            _runner.BeginHorde(_schedule, new[] { player, ally }, new SideSpawnPoint[0], player, hud);

            player.Damageable.Kill();
            _runner.Tick(0f);
            Assert.That(hud.Lines, Does.Contain(Localizer.Resolve(HordeText.ReturnsAfterWave)));

            ally.Damageable.Kill();
            _runner.Tick(0f);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(hud.Lines, Does.Contain(Localizer.Resolve(HordeText.Defeat)));
        }

        [Test]
        public void ABrokenWallCanOnlyBeRepairedBetweenWaves()
        {
            var player = Member("Player", Vector3.zero);
            var wall = Fortification("Wall", new Vector3(0f, 0f, 2f), true);
            _runner.SetFortifications(new[] { wall });
            var hud = BoundHudView();
            _runner.BeginHorde(_schedule, new[] { player }, new SideSpawnPoint[0], player, hud);

            var structure = wall.Structure;
            structure.ApplyStructureHit(structure.Pieces[0].Collider, structure.Pieces[0].transform.position, 1000);
            Assert.That(wall.PendingAction, Is.EqualTo(HordeFortificationAction.Repair));

            _runner.Tick(0f);
            Assert.That(
                hud.Lines,
                Does.Contain(Localizer.Resolve(
                    HordeText.Prompt(null, HordeFortificationAction.Repair, "Wall", RepairCostPerPiece, HordePurchaseVerdict.WaveInProgress))));
            Assert.That(_runner.TryInteract(), Is.EqualTo(HordePurchaseVerdict.WaveInProgress));
            Assert.That(structure.Pieces[0].IsBroken, Is.True);

            ClearWave();
            var currency = _runner.Horde.Currency;

            Assert.That(_runner.TryInteract(), Is.EqualTo(HordePurchaseVerdict.Ok));
            Assert.That(structure.Pieces[0].IsBroken, Is.False);
            Assert.That(structure.Integrity.DestroyedCount, Is.EqualTo(0));
            Assert.That(_runner.Horde.Currency, Is.EqualTo(currency - RepairCostPerPiece));
            Assert.That(wall.PendingAction, Is.EqualTo(HordeFortificationAction.None));
            Assert.That(_runner.TryInteract(), Is.EqualTo(HordePurchaseVerdict.NothingToDo), "nothing is left to repair");
        }

        [Test]
        public void ABarricadeIsPlacedWithCurrencyBetweenWaves()
        {
            var player = Member("Player", Vector3.zero);
            var barricade = Fortification("Barricade", new Vector3(0f, 0f, 2f), false);
            _runner.SetFortifications(new[] { barricade });
            _runner.BeginHorde(_schedule, new[] { player }, new SideSpawnPoint[0], player);

            Assert.That(barricade.IsBuilt, Is.False);
            Assert.That(barricade.Structure.Pieces[0].IsBroken, Is.True, "an unplaced barricade is neither seen nor solid");
            Assert.That(barricade.PendingAction, Is.EqualTo(HordeFortificationAction.Build));

            ClearWave();
            Assert.That(_runner.Horde.Currency, Is.LessThan(BuildCost * 2));
            _runner.TryInteract();
            Assert.That(_runner.Horde.Currency, Is.LessThan(BuildCost), "the first barricade spent most of the currency");
            Assert.That(barricade.IsBuilt, Is.True);
            Assert.That(barricade.Structure.Pieces[0].IsBroken, Is.False);
            Assert.That(barricade.Structure.Pieces[1].IsBroken, Is.False);
        }

        [Test]
        public void PlacingWithoutEnoughCurrencyChangesNothing()
        {
            _definition.StartingCurrency = 0;
            _definition.KillReward = 0;
            var player = Member("Player", Vector3.zero);
            var barricade = Fortification("Barricade", new Vector3(0f, 0f, 2f), false);
            _runner.SetFortifications(new[] { barricade });
            _runner.BeginHorde(_schedule, new[] { player }, new SideSpawnPoint[0], player);
            ClearWave();

            Assert.That(_runner.TryInteract(), Is.EqualTo(HordePurchaseVerdict.NotEnoughCurrency));
            Assert.That(barricade.IsBuilt, Is.False);
            Assert.That(_runner.Horde.Currency, Is.EqualTo(0));
        }

        [Test]
        public void OnlyFortificationsWithinReachCanBeUsed()
        {
            var player = Member("Player", Vector3.zero);
            var far = Fortification("Far", new Vector3(0f, 0f, 10f), false);
            _runner.SetFortifications(new[] { far });
            _runner.BeginHorde(_schedule, new[] { player }, new SideSpawnPoint[0], player);
            ClearWave();

            Assert.That(_runner.NearestFortification(player.GroundPosition), Is.Null);
            Assert.That(_runner.TryInteract(), Is.EqualTo(HordePurchaseVerdict.NothingToDo));
            Assert.That(far.IsBuilt, Is.False);
        }

        [Test]
        public void APieceOverlappingSomeoneAliveIsNotRestored()
        {
            var player = Member("Player", Vector3.zero);
            var barricade = Fortification("Barricade", new Vector3(0f, 0f, 2f), false);
            _runner.SetFortifications(new[] { barricade });
            _runner.BeginHorde(_schedule, new[] { player }, new SideSpawnPoint[0], player);
            ClearWave();

            var blocker = Track(new GameObject("Blocker"));
            blocker.transform.position = barricade.Structure.Pieces[0].transform.position;
            blocker.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.5f, 0.5f);
            blocker.AddComponent<Damageable>().Initialize("blocker", Damageable.DefaultMaxHealth);

            Assert.That(barricade.RestorablePieces(), Is.EqualTo(new List<int> { 1 }));

            _runner.TryInteract();
            Assert.That(barricade.Structure.Pieces[0].IsBroken, Is.True, "nobody gets walled in");
            Assert.That(barricade.Structure.Pieces[1].IsBroken, Is.False);
        }

        [Test]
        public void TheDefinitionDecidesTheWaveCountAndTheRestBetweenWaves()
        {
            _definition.WaveCount = 3;
            _definition.RestSeconds = 7f;
            _definition.FirstEnemyCount = 1;
            _definition.EnemiesPerWave = 0f;
            _schedule = _runner.CreateSchedule();
            var player = Member("Player", Vector3.zero);
            var hud = BoundHudView();
            _runner.BeginHorde(_schedule, new[] { player }, new SideSpawnPoint[0], player, hud);

            Assert.That(_schedule.WaveCount, Is.EqualTo(3));
            Assert.That(hud.Lines, Does.Contain(Localizer.Resolve(HordeText.Wave(1, 3))));

            for (var wave = 1; wave < 3; wave++)
            {
                ClearWave();
                _runner.Tick(0f);
                Assert.That(_schedule.Phase, Is.EqualTo(WavePhase.Resting));
                Assert.That(_schedule.RestRemaining, Is.EqualTo(7f));
                Assert.That(hud.Lines, Does.Contain(Localizer.Resolve(HordeText.NextWave(7f))));

                _schedule.Tick(6f);
                Assert.That(_schedule.Phase, Is.EqualTo(WavePhase.Resting), "the rest lasts as long as the definition says");
                _schedule.Tick(1f);
                Assert.That(_schedule.WaveNumber, Is.EqualTo(wave + 1));
            }

            ClearWave();
            _runner.Tick(0f);
            Assert.That(_runner.Horde.IsVictory, Is.True, "the third wave was the last");
        }

        [Test]
        public void TheDefinitionDecidesTheCurrency()
        {
            _definition.StartingCurrency = 7;
            _definition.KillReward = 3;
            _definition.KillRewardPerWave = 4;
            _definition.PurchasesOnlyBetweenWaves = false;

            var spec = _runner.CreateSpec();

            Assert.That(spec.StartingCurrency, Is.EqualTo(7));
            Assert.That(spec.KillRewardAt(1), Is.EqualTo(3));
            Assert.That(spec.KillRewardAt(2), Is.EqualTo(7));
            Assert.That(spec.PurchasesOnlyBetweenWaves, Is.False);
            Assert.That(spec.Defenders, Is.EqualTo(_runner.PlayerSide));
            Assert.That(spec.Attackers, Is.EqualTo(_runner.EnemySide));
        }

        [Test]
        public void WithoutADefinitionTheDefaultsApplyAndTheWavesComeFromTheSpawner()
        {
            _runner.Definition = null;

            var spec = _runner.CreateSpec();
            Assert.That(spec.StartingCurrency, Is.EqualTo(HordeSpec.DefaultStartingCurrency));
            Assert.That(spec.KillReward, Is.EqualTo(HordeSpec.DefaultKillReward));
            Assert.That(spec.KillRewardPerWave, Is.EqualTo(HordeSpec.DefaultKillRewardPerWave));
            Assert.That(spec.PurchasesOnlyBetweenWaves, Is.True);
            Assert.Throws<System.InvalidOperationException>(() => _runner.CreateSchedule(), "neither a definition nor a spawner decides the waves");

            var spawner = Track(new GameObject("WaveSpawner")).AddComponent<WaveSpawner>();
            spawner.SetFormula(1, 0f, 1f, 0f, 1f, 1, 2f, 4);
            _runner.Spawner = spawner;
            Assert.That(_runner.CreateSchedule().WaveCount, Is.EqualTo(4));

            _runner.Definition = _definition;
            _definition.WaveCount = 6;
            Assert.That(_runner.CreateSchedule().WaveCount, Is.EqualTo(6), "the definition wins over the spawner");
        }

        [Test]
        public void ANewDefinitionHoldsTheDefaults()
        {
            var fresh = ScriptableObject.CreateInstance<HordeDefinition>();
            _objects.Add(fresh);

            var spec = fresh.ToSpec(Side.Alpha, Side.Bravo);
            Assert.That(spec.StartingCurrency, Is.EqualTo(HordeSpec.DefaultStartingCurrency));
            Assert.That(spec.KillReward, Is.EqualTo(HordeSpec.DefaultKillReward));
            Assert.That(spec.KillRewardPerWave, Is.EqualTo(HordeSpec.DefaultKillRewardPerWave));
            Assert.That(fresh.CreateSchedule().WaveCount, Is.EqualTo(WaveSpawner.DefaultWaveCount));
            Assert.That(fresh.RestSeconds, Is.EqualTo(WaveSpawner.DefaultRestSeconds));
        }

        [Test]
        public void AnInvalidDefinitionNamesTheAssetAndStopsTheHorde()
        {
            _definition.KillReward = -1;
            var error = Assert.Throws<System.ArgumentException>(() => _runner.CreateSpec());
            Assert.That(error.Message, Does.Contain("TestHorde"));

            _definition.KillReward = KillReward;
            _definition.FirstEnemyCount = 0;
            error = Assert.Throws<System.ArgumentException>(() => _runner.CreateSchedule());
            Assert.That(error.Message, Does.Contain("TestHorde"));

            _definition.FirstEnemyCount = 1;
            _definition.RestSeconds = -1f;
            Assert.Throws<System.ArgumentException>(() => _definition.Validate(Side.Alpha, Side.Bravo));
        }

        private void ClearWave()
        {
            while (_schedule.Phase == WavePhase.Fighting)
            {
                while (_schedule.SpawnBudget > 0)
                {
                    _schedule.ReportSpawned();
                }

                _schedule.ReportKilled();
                _runner.RewardEnemyKill();
            }
        }

        private HordeFortification Fortification(string name, Vector3 position, bool startsBuilt)
        {
            var root = Track(new GameObject(name));
            root.transform.position = position;
            var structure = root.AddComponent<DestructibleStructure>();
            structure.DestructionEnabled = true;
            structure.SpawnDebris = false;

            // 2 つとも地面に接するので、片方が壊れてももう片方は崩れない。
            DestructiblePiece.Create(root.transform, name + "-0", new Vector3(-0.5f, 0.5f, 0f), Vector3.one, 0, 100, grounded: true, carveObstacle: false);
            DestructiblePiece.Create(root.transform, name + "-1", new Vector3(0.5f, 0.5f, 0f), Vector3.one, 1, 100, grounded: true, carveObstacle: false);

            var fortification = root.AddComponent<HordeFortification>();
            fortification.StartsBuilt = startsBuilt;
            fortification.BuildCost = BuildCost;
            fortification.RepairCostPerPiece = RepairCostPerPiece;
            fortification.InteractRadius = 3f;
            return fortification;
        }

        private SideMember Member(string name, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = Side.Alpha;
            return member;
        }

        private SideSpawnPoint SpawnPoint(Vector3 position)
        {
            var point = Track(new GameObject("Spawn-0")).AddComponent<SideSpawnPoint>();
            point.transform.position = position;
            point.Side = Side.Alpha;
            return point;
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
