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
    // bot は NavMesh の無い Edit Mode で初期化した EnemyAgent で歩かないので、渡した用事は EnemyAgent.Errand で見る。
    public sealed class CaptureTheFlagRunnerTests
    {
        private const float ReturnSeconds = 10f;
        private const float RespawnSeconds = 5f;
        private const float PlanInterval = 0.5f;

        private static readonly Vector3 AlphaBase = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 BravoBase = new Vector3(0f, 0f, 40f);
        private static readonly Vector3 Midfield = new Vector3(5f, 0f, 20f);

        private readonly List<Object> _objects = new List<Object>();
        private CaptureTheFlagRunner _runner;
        private FlagBase _alphaBase;
        private FlagBase _bravoBase;
        private FlagObject _alphaFlag;
        private FlagObject _bravoFlag;
        private SideMember _alphaOne;
        private SideMember _alphaTwo;
        private SideMember _bravoOne;
        private SideMember _bravoTwo;
        private SideSpawnPoint _alphaSpawn;
        private SideSpawnPoint _bravoSpawn;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _runner = Track(new GameObject("Runner")).AddComponent<CaptureTheFlagRunner>();
            _runner.RespawnSeconds = RespawnSeconds;
            _runner.ReturnSeconds = ReturnSeconds;
            _runner.BotPlanIntervalSeconds = PlanInterval;

            _alphaBase = Base("Base-Alpha", Side.Alpha, AlphaBase);
            _bravoBase = Base("Base-Bravo", Side.Bravo, BravoBase);
            _runner.SetBases(new[] { _alphaBase, _bravoBase });

            _alphaFlag = Flag("Flag-Alpha", Side.Alpha);
            _bravoFlag = Flag("Flag-Bravo", Side.Bravo);
            _runner.SetFlagObjects(new[] { _alphaFlag, _bravoFlag });

            // 名前の順に ID が振られる: A-1 = 0, A-2 = 1, B-1 = 2, B-2 = 3。どの旗と台からも離れた所に立たせる。
            _alphaOne = Member("A-1", Side.Alpha, new Vector3(-10f, 0f, 10f));
            _alphaTwo = Member("A-2", Side.Alpha, new Vector3(-12f, 0f, 10f));
            _bravoOne = Member("B-1", Side.Bravo, new Vector3(-10f, 0f, 30f));
            _bravoTwo = Member("B-2", Side.Bravo, new Vector3(-12f, 0f, 30f));

            _alphaSpawn = SpawnPoint("Spawn-Alpha", Side.Alpha, new Vector3(-20f, 0f, 0f));
            _bravoSpawn = SpawnPoint("Spawn-Bravo", Side.Bravo, new Vector3(-20f, 0f, 40f));
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
            GameSignals.ResetAll();
        }

        [Test]
        public void TryBeginFailsWithoutBases()
        {
            _runner.SetBases(Array.Empty<FlagBase>());
            LogAssert.Expect(LogType.Error, new Regex("FlagBase"));

            Assert.That(_runner.TryBegin(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void ResolveBasesNeedsOneBasePerTeam()
        {
            var resolved = CaptureTheFlagRunner.ResolveBases(new[] { _bravoBase, _alphaBase });
            Assert.That(resolved[Side.Alpha], Is.SameAs(_alphaBase));
            Assert.That(resolved[Side.Bravo], Is.SameAs(_bravoBase));

            Assert.Throws<InvalidOperationException>(() => CaptureTheFlagRunner.ResolveBases(new[] { _alphaBase }), "Bravo has no base");
            Assert.Throws<InvalidOperationException>(() => CaptureTheFlagRunner.ResolveBases(new FlagBase[] { _alphaBase, _bravoBase, null }));
            Assert.Throws<InvalidOperationException>(() => CaptureTheFlagRunner.ResolveBases(null));

            var extra = Base("Base-Extra", Side.Alpha, Midfield);
            Assert.Throws<InvalidOperationException>(() => CaptureTheFlagRunner.ResolveBases(new[] { _alphaBase, _bravoBase, extra }), "two bases for Alpha");

            extra.Side = new Side(2);
            Assert.Throws<InvalidOperationException>(() => CaptureTheFlagRunner.ResolveBases(new[] { _alphaBase, _bravoBase, extra }), "side 2 has no flag");
        }

        [Test]
        public void BeginningStandsBothFlagsOnTheirBasesAndSizesTheBasesToTheCaptureRadius()
        {
            _runner.CaptureRadius = 3f;

            Begin();

            Assert.That(_alphaBase.Radius, Is.EqualTo(3f));
            Assert.That(_bravoBase.Radius, Is.EqualTo(3f));
            Assert.That(_alphaFlag.ShownState, Is.EqualTo((FlagState?)FlagState.AtBase));
            Assert.That(_alphaFlag.transform.position, Is.EqualTo(AlphaBase));
            Assert.That(_bravoFlag.ShownState, Is.EqualTo((FlagState?)FlagState.AtBase));
            Assert.That(_bravoFlag.transform.position, Is.EqualTo(BravoBase));
            Assert.That(_bravoFlag.IsVisible, Is.True);
            Assert.That(_bravoFlag.Model, Is.Not.Null, "an empty model reference builds a placeholder flag");
            Assert.That(_bravoFlag.Model.GetComponentsInChildren<Collider>(true), Is.Empty, "the flag does not block shots or movement");
            Assert.That(_runner.FlagPosition(Side.Bravo), Is.EqualTo((Vector3?)BravoBase));
        }

        [Test]
        public void AnEnemyWalkingOntoTheFlagTakesIt()
        {
            Begin();
            LogAssert.Expect(LogType.Log, "[Mode] flag-taken flag=1 player=0");

            MoveTo(_alphaOne, BravoBase + new Vector3(1f, 0f, 0f));
            _runner.Tick(0f);

            Assert.That(_runner.Game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Carried));
            Assert.That(_runner.Game.GetCarrierId(Side.Bravo), Is.EqualTo(_runner.IdOf(_alphaOne)));
            Assert.That(_bravoFlag.ShownState, Is.EqualTo((FlagState?)FlagState.Carried));
            Assert.That(_bravoFlag.Carrier, Is.SameAs(_alphaOne.transform));

            var detail = (CaptureTheFlagStatus)_runner.Status.Detail;
            Assert.That(detail.GetState(Side.Bravo), Is.EqualTo(FlagState.Carried));
            Assert.That(detail.GetCarrierId(Side.Bravo), Is.EqualTo(_runner.IdOf(_alphaOne)));
            Assert.That(detail.LocalPlayerId, Is.Null, "nobody plays on this screen");
        }

        [Test]
        public void StandingOnYourOwnFlagOrOutsideThePickupRadiusTakesNothing()
        {
            _runner.PickupRadius = 1f;
            Begin();

            MoveTo(_bravoOne, BravoBase);
            MoveTo(_alphaOne, BravoBase + new Vector3(1.5f, 0f, 0f));
            _runner.Tick(0f);
            Assert.That(_runner.Game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase), "1.5 m is outside the 1 m pickup radius");

            MoveTo(_alphaOne, BravoBase + new Vector3(0f, CaptureTheFlagRunner.ReachHeight + 1f, 0f));
            _runner.Tick(0f);
            Assert.That(_runner.Game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase), "a floor above the flag is out of reach");
        }

        [Test]
        public void CarryingTheEnemyFlagHomeScoresAndSendsItBack()
        {
            Begin();
            LogAssert.Expect(LogType.Log, "[Mode] flag-captured flag=1 player=0 side=0 score=1");
            MoveTo(_alphaOne, BravoBase);
            _runner.Tick(0f);

            MoveTo(_alphaOne, AlphaBase + new Vector3(1.5f, 0f, 0f));
            _runner.Tick(0f);

            var game = _runner.Game;
            Assert.That(game.GetCaptures(Side.Alpha), Is.EqualTo(1));
            Assert.That(game.Session.GetScore(Side.Alpha), Is.EqualTo(1d));
            Assert.That(game.IsCarrying(_runner.IdOf(_alphaOne)), Is.False);
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase));
            Assert.That(_bravoFlag.ShownState, Is.EqualTo((FlagState?)FlagState.AtBase));
            Assert.That(_bravoFlag.transform.position, Is.EqualTo(BravoBase));
            Assert.That(_runner.Status.SideScores, Has.Member(new SideScore(Side.Alpha, 1d)));
        }

        [Test]
        public void TheCarrierCannotScoreWhileTheirOwnFlagIsAway()
        {
            Begin();
            MoveTo(_bravoOne, AlphaBase);
            MoveTo(_alphaOne, BravoBase);
            _runner.Tick(0f);
            Assert.That(_runner.Game.GetFlagState(Side.Alpha), Is.EqualTo(FlagState.Carried));

            MoveTo(_bravoOne, Midfield);
            MoveTo(_alphaOne, AlphaBase);
            _runner.Tick(0f);

            Assert.That(_runner.Game.GetCaptures(Side.Alpha), Is.EqualTo(0));
            Assert.That(_runner.Game.IsCarrying(_runner.IdOf(_alphaOne)), Is.True, "the carrier keeps the flag and waits");
        }

        [Test]
        public void ACarrierWhoFallsDropsTheFlagWhereTheyFell()
        {
            Begin();
            MoveTo(_alphaOne, BravoBase);
            _runner.Tick(0f);
            LogAssert.Expect(LogType.Log, "[Mode] flag-dropped flag=1 player=0");

            MoveTo(_alphaOne, Midfield);
            _alphaOne.Damageable.Kill();

            Assert.That(_runner.Game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped), "the flag falls at once, not on the next tick");
            Assert.That(_runner.FlagPosition(Side.Bravo), Is.EqualTo((Vector3?)Midfield));
            Assert.That(_bravoFlag.ShownState, Is.EqualTo((FlagState?)FlagState.Dropped));
            Assert.That(_bravoFlag.transform.position, Is.EqualTo(Midfield));
            Assert.That(_bravoFlag.Carrier, Is.Null);

            _runner.Tick(0f);
            var detail = (CaptureTheFlagStatus)_runner.Status.Detail;
            Assert.That(detail.GetState(Side.Bravo), Is.EqualTo(FlagState.Dropped));
            Assert.That(detail.GetReturnSeconds(Side.Bravo), Is.EqualTo(ReturnSeconds));
        }

        [Test]
        public void ACarrierWhoRespawnsAtOnceStillLeavesTheFlagWhereTheyFell()
        {
            _runner.RespawnSeconds = 0f;
            Begin();
            MoveTo(_alphaOne, BravoBase);
            _runner.Tick(0f);

            MoveTo(_alphaOne, Midfield);
            _alphaOne.Damageable.Kill();
            _runner.Tick(0f);

            Assert.That(_alphaOne.transform.position, Is.EqualTo(_alphaSpawn.Position), "the carrier is already back at the spawn");
            Assert.That(_runner.Game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped));
            Assert.That(_runner.FlagPosition(Side.Bravo), Is.EqualTo((Vector3?)Midfield));
        }

        [Test]
        public void AnEnemyPicksTheDroppedFlagUpAndATeammateReturnsIt()
        {
            Begin();
            MoveTo(_alphaOne, BravoBase);
            _runner.Tick(0f);
            MoveTo(_alphaOne, Midfield);
            _alphaOne.Damageable.Kill();

            MoveTo(_alphaTwo, Midfield + new Vector3(0.5f, 0f, 0f));
            _runner.Tick(0f);
            Assert.That(_runner.Game.GetCarrierId(Side.Bravo), Is.EqualTo(_runner.IdOf(_alphaTwo)));
            Assert.That(_bravoFlag.Carrier, Is.SameAs(_alphaTwo.transform));

            var fallen = Midfield + new Vector3(0f, 0f, -5f);
            MoveTo(_alphaTwo, fallen);
            _alphaTwo.Damageable.Kill();
            LogAssert.Expect(LogType.Log, "[Mode] flag-returned flag=1 by=" + _runner.IdOf(_bravoOne));

            MoveTo(_bravoOne, fallen + new Vector3(0.5f, 0f, 0f));
            _runner.Tick(0f);

            Assert.That(_runner.Game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase));
            Assert.That(_bravoFlag.ShownState, Is.EqualTo((FlagState?)FlagState.AtBase));
            Assert.That(_bravoFlag.transform.position, Is.EqualTo(BravoBase));
        }

        [Test]
        public void ADroppedFlagGoesHomeByItselfAfterTheReturnTime()
        {
            Begin();
            MoveTo(_alphaOne, BravoBase);
            _runner.Tick(0f);
            MoveTo(_alphaOne, Midfield);
            _alphaOne.Damageable.Kill();

            _runner.Tick(ReturnSeconds - 1f);
            Assert.That(_runner.Game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped));

            LogAssert.Expect(LogType.Log, "[Mode] flag-returned flag=1 by=-");
            _runner.Tick(1f);

            Assert.That(_runner.Game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase));
            Assert.That(_runner.FlagPosition(Side.Bravo), Is.EqualTo((Vector3?)BravoBase));
            Assert.That(_bravoFlag.ShownState, Is.EqualTo((FlagState?)FlagState.AtBase));
        }

        [Test]
        public void ReachingTheScoreLimitWinsTheMatch()
        {
            _runner.ScoreLimit = 1;
            Begin();
            MoveTo(_alphaOne, BravoBase);
            _runner.Tick(0f);

            MoveTo(_alphaOne, AlphaBase);
            _runner.Tick(0f);

            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)Side.Alpha));

            MoveTo(_bravoOne, AlphaBase);
            _runner.Tick(0f);
            Assert.That(_runner.Game.GetFlagState(Side.Alpha), Is.EqualTo(FlagState.AtBase), "nobody takes a flag after the match");
        }

        [Test]
        public void TheStatusNamesTheLocalCarrier()
        {
            _runner.Begin(
                new[] { _alphaOne, _alphaTwo, _bravoOne, _bravoTwo },
                new[] { _alphaSpawn, _bravoSpawn },
                _alphaTwo);

            MoveTo(_alphaTwo, BravoBase);
            _runner.Tick(0f);

            var detail = (CaptureTheFlagStatus)_runner.Status.Detail;
            Assert.That(detail.LocalPlayerId, Is.EqualTo(ModeRunnerBase.LocalPlayerId));
            Assert.That(detail.LocalSide, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(detail.IsLocalCarrying, Is.True);
            Assert.That(detail.CarriedFlag, Is.EqualTo((Side?)Side.Bravo));
        }

        [Test]
        public void AWiredDefinitionReplacesTheInspectorValues()
        {
            var definition = Definition("CtfTuning");
            definition.ScoreLimit = 5;
            definition.TimeLimitSeconds = 300f;
            definition.RespawnSeconds = 2f;
            definition.PickupRadius = 4f;
            definition.CaptureRadius = 6f;
            definition.ReturnSeconds = 15f;
            definition.PointsPerCapture = 2;
            definition.BotPlanIntervalSeconds = 1.5f;
            definition.BotAttackShare = 0.25f;
            _runner.Definition = definition;
            _runner.PickupRadius = 1f;

            Begin();

            var spec = _runner.Game.Spec;
            Assert.That(spec.ScoreLimit, Is.EqualTo(5));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(300f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(2f));
            Assert.That(spec.PickupRadius, Is.EqualTo(4f));
            Assert.That(spec.CaptureRadius, Is.EqualTo(6f));
            Assert.That(spec.ReturnSeconds, Is.EqualTo(15f));
            Assert.That(spec.PointsPerCapture, Is.EqualTo(2));
            Assert.That(_runner.EffectiveBotPlanIntervalSeconds, Is.EqualTo(1.5f));
            Assert.That(_runner.EffectiveBotAttackShare, Is.EqualTo(0.25f));
            Assert.That(_bravoBase.Radius, Is.EqualTo(6f));

            MoveTo(_alphaOne, BravoBase + new Vector3(3f, 0f, 0f));
            _runner.Tick(0f);
            Assert.That(_runner.Game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Carried), "3 m is inside the definition's 4 m, not the Inspector's 1 m");
        }

        [Test]
        public void TryBeginFailsWithAnInvalidDefinitionAndNamesIt()
        {
            var definition = Definition("BrokenCtf");
            definition.ReturnSeconds = 0f;
            _runner.Definition = definition;
            LogAssert.Expect(LogType.Error, new Regex("BrokenCtf"));

            Assert.That(_runner.TryBegin(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void BeginningRejectsAnAttackShareOutsideZeroToOne()
        {
            _runner.BotAttackShare = 1.5f;

            Assert.Throws<ArgumentOutOfRangeException>(Begin);
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Game, Is.Null);
        }

        [Test]
        public void BotsAreSentToTakeDefendCarryAndRecoverTheFlags()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            _objects.Add(_rifle);
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            // 名前の順に ID が振られる: A-Bot-1 = 0, A-Bot-2 = 1, B-Bot-1 = 2。Alpha の 2 人は半分ずつ攻めと守り、Bravo の 1 人は攻める。
            var attacker = Bot("A-Bot-1", Side.Alpha, new Vector3(-10f, 0f, 10f));
            var defender = Bot("A-Bot-2", Side.Alpha, new Vector3(-12f, 0f, 10f));
            var raider = Bot("B-Bot-1", Side.Bravo, new Vector3(-10f, 0f, 30f));
            _runner.Begin(new[] { attacker, defender, raider }, new[] { _alphaSpawn, _bravoSpawn });

            AssertErrand(attacker, BravoBase, false);
            AssertErrand(defender, AlphaBase, false);
            AssertErrand(raider, AlphaBase, false);

            MoveTo(attacker, BravoBase);
            _runner.Tick(0f);
            Assert.That(_runner.Game.GetCarrierId(Side.Bravo), Is.EqualTo(_runner.IdOf(attacker)));
            AssertErrand(attacker, AlphaBase, true);

            MoveTo(attacker, Midfield);
            attacker.Damageable.Kill();

            Assert.That(attacker.GetComponent<EnemyAgent>().Errand, Is.Null, "a fallen bot has nothing to do");
            AssertErrand(raider, Midfield, true);
            AssertErrand(defender, AlphaBase, false);

            defender.Damageable.Kill();
            _runner.Tick(PlanInterval);
            Assert.That(defender.GetComponent<EnemyAgent>().Errand, Is.Null, "the next plan clears the errand of a bot who fell");
        }

        [Test]
        public void StoppingClearsTheBotsErrands()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            _objects.Add(_rifle);
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            var bot = Bot("A-Bot-1", Side.Alpha, new Vector3(-10f, 0f, 10f));
            _runner.Begin(new[] { bot, _bravoOne }, new[] { _alphaSpawn, _bravoSpawn });
            Assert.That(bot.GetComponent<EnemyAgent>().Errand, Is.Not.Null);

            _runner.Stop();

            Assert.That(bot.GetComponent<EnemyAgent>().Errand, Is.Null);
        }

        [Test]
        public void TheFlagUsesTheModelPrefabWithItsCollidersRemoved()
        {
            var prefab = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            prefab.name = "BannerPrefab";
            _bravoFlag.ModelPrefab = prefab;

            _bravoFlag.ShowDropped(Midfield);
            _objects.Add(_bravoFlag.Model.GetComponent<Renderer>().sharedMaterial);

            Assert.That(_bravoFlag.Model.name, Is.EqualTo(FlagObject.ModelName));
            Assert.That(_bravoFlag.Model.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(prefab.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(_bravoFlag.Model.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(_bravoFlag.transform.position, Is.EqualTo(Midfield));
            Assert.That(_bravoFlag.ShownState, Is.EqualTo((FlagState?)FlagState.Dropped));

            _bravoFlag.Hide();
            Assert.That(_bravoFlag.IsVisible, Is.False);
            Assert.That(_bravoFlag.Model.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(_bravoFlag.ShownState, Is.Null);
        }

        [Test]
        public void TheBaseMarkerIsAColliderFreeDiscInTheTeamColour()
        {
            _bravoBase.Radius = 3f;

            _bravoBase.RefreshMarker();

            var marker = _bravoBase.Marker;
            // Edit Mode では OnDestroy が呼ばれず、複製した材質が残るので、ここで消す。
            _objects.Add(marker.GetComponent<Renderer>().sharedMaterial);
            Assert.That(marker.name, Is.EqualTo(FlagBase.MarkerName));
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(marker.transform.lossyScale.x, Is.EqualTo(6f).Within(0.001f), "the disc spans the radius both ways");
            AssertColor(marker.GetComponent<Renderer>().sharedMaterial.color, FlagBase.ColorOf(Side.Bravo));
            Assert.That(_bravoBase.Contains(BravoBase + new Vector3(2.9f, 0f, 0f)), Is.True);
            Assert.That(_bravoBase.Contains(BravoBase + new Vector3(3.1f, 0f, 0f)), Is.False);
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-3f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-3f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-3f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-3f));
        }

        private void AssertErrand(SideMember bot, Vector3 destination, bool overridesCombat)
        {
            var agent = bot.GetComponent<EnemyAgent>();
            Assert.That(agent.Errand.HasValue, Is.True, bot.name + " has an errand");
            Assert.That(Vector3.Distance(agent.Errand.Value, destination), Is.LessThan(0.001f), bot.name + " goes to " + destination);
            Assert.That(agent.ErrandOverridesCombat, Is.EqualTo(overridesCombat), bot.name);
        }

        private CaptureTheFlagDefinition Definition(string name)
        {
            var definition = ScriptableObject.CreateInstance<CaptureTheFlagDefinition>();
            definition.name = name;
            _objects.Add(definition);
            return definition;
        }

        private void Begin()
        {
            _runner.Begin(
                new[] { _alphaOne, _alphaTwo, _bravoOne, _bravoTwo },
                new[] { _alphaSpawn, _bravoSpawn });
        }

        // 足元 (SideMember.GroundPosition) を feet に合わせる。bot は NavMeshAgent の高さの分だけ Transform が足元より上にある。
        private static void MoveTo(SideMember member, Vector3 feet)
        {
            member.transform.position += feet - member.GroundPosition;
        }

        private SideMember Member(string name, Side side, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = side;
            return member;
        }

        private SideMember Bot(string name, Side side, Vector3 feet)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            go.name = name;
            go.transform.position = feet + Vector3.up;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = side;
            var agent = go.AddComponent<EnemyAgent>();
            agent.Id = name;
            agent.WeaponDefinition = _rifle;
            Assert.That(agent.TryInitialize(), Is.True);
            Physics.SyncTransforms();
            return member;
        }

        private SideSpawnPoint SpawnPoint(string name, Side side, Vector3 position)
        {
            var point = Track(new GameObject(name)).AddComponent<SideSpawnPoint>();
            point.transform.position = position;
            point.Side = side;
            return point;
        }

        private FlagBase Base(string name, Side side, Vector3 position)
        {
            var flagBase = Track(new GameObject(name)).AddComponent<FlagBase>();
            flagBase.transform.position = position;
            flagBase.Side = side;
            return flagBase;
        }

        private FlagObject Flag(string name, Side side)
        {
            var flag = Track(new GameObject(name)).AddComponent<FlagObject>();
            flag.Side = side;
            return flag;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
