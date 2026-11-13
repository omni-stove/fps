using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// BattleRoyaleRunner が倒れた参加者をダウンさせて分隊の味方に起こさせ、全滅した分隊に持ち物を落とさせて順位を付け、
    /// 縮むエリアを始めて終わりに止め、始めたときに武器を手放させることを確かめる。
    /// bot (EnemyAgent) は NavMesh が要るので、Damageable と SideMember だけの参加者 (プレイヤーには WeaponController) で確かめる。
    /// </summary>
    public sealed class BattleRoyaleRunnerTests
    {
        private static readonly Vector3 ZeroSpawn = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 ZeroSpawnTwo = new Vector3(-3f, 0f, 0f);
        private static readonly Vector3 OneSpawn = new Vector3(0f, 0f, 8f);
        private static readonly Vector3 OneSpawnTwo = new Vector3(3f, 0f, 8f);

        private readonly List<Object> _objects = new List<Object>();
        private BattleRoyaleRunner _runner;
        private SideSpawnPoint[] _spawns;
        private InputAction _attack;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _runner = Track(new GameObject("Runner")).AddComponent<BattleRoyaleRunner>();
            _spawns = new[]
            {
                SpawnPoint("Spawn-0-1", 0, ZeroSpawn),
                SpawnPoint("Spawn-1-1", 1, OneSpawn),
            };
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
            foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(pickup.gameObject);
            }

            _attack.Dispose();
            _reload.Dispose();
        }

        [Test]
        public void ADownedSquadmateIsRevivedWhereTheyLie()
        {
            var player = Member("Player", 0, ZeroSpawn);
            var mate = Member("Bot-0-1", 0, ZeroSpawnTwo);
            var enemy = Member("Bot-1-1", 1, OneSpawn);
            Begin(new[] { player, mate, enemy }, player);

            KillBy(enemy, mate);
            _runner.Tick(0.1f);

            Assert.That(mate.Damageable.IsDowned, Is.True);
            Assert.That(_runner.BattleRoyale.IsDowned(_runner.IdOf(mate)), Is.True);
            Assert.That(_runner.HoldRevive(player, 1f), Is.EqualTo(ReviveStep.Rejected), "the downed squadmate is out of reach");

            MoveNextTo(player, mate);
            var lyingAt = mate.transform.position;
            Assert.That(_runner.HoldRevive(player, 2f), Is.EqualTo(ReviveStep.Progressed));
            Assert.That(_runner.LocalReviveTarget, Is.SameAs(mate));
            _runner.Tick(0.1f);
            Assert.That(_runner.HoldRevive(player, 1f), Is.EqualTo(ReviveStep.Completed));

            Assert.That(mate.IsAlive, Is.True);
            Assert.That(mate.Damageable.IsDowned, Is.False);
            Assert.That(mate.Damageable.Health.Current, Is.EqualTo(30), "revived with 30% of 100");
            Assert.That(mate.transform.position, Is.EqualTo(lyingAt));
        }

        [Test]
        public void AnEnemyCannotReviveAcrossSquads()
        {
            var player = Member("Player", 0, ZeroSpawn);
            var mate = Member("Bot-0-1", 0, ZeroSpawnTwo);
            var enemy = Member("Bot-1-1", 1, OneSpawn);
            Begin(new[] { player, mate, enemy }, player);

            KillBy(enemy, mate);
            MoveNextTo(enemy, mate);

            Assert.That(_runner.HoldRevive(enemy, 5f), Is.EqualTo(ReviveStep.Rejected));
        }

        [Test]
        public void TheLastSquadWinsAndThePlacementsAreLogged()
        {
            var player = Member("Player", 0, ZeroSpawn);
            var enemy = Member("Bot-1-1", 1, OneSpawn);
            var enemyMate = Member("Bot-1-2", 1, OneSpawnTwo);
            Begin(new[] { player, enemy, enemyMate }, player);

            KillBy(player, enemy);
            Assert.That(enemy.Damageable.IsDowned, Is.True);
            KillBy(player, enemyMate);

            Assert.That(enemy.Damageable.IsDowned, Is.False, "the squad is wiped, so the downed member falls too");
            Assert.That(enemy.IsAlive, Is.False);

            LogAssert.Expect(LogType.Log, "[Mode] eliminated squad=1 place=2 remaining=1");
            LogAssert.Expect(LogType.Log, "[Mode] ended winner=0");
            LogAssert.Expect(LogType.Log, "[Mode] placements winner=0 1:squad=0 2:squad=1");
            _runner.Tick(0f);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.BattleRoyale.GetPlacement(new Side(0)), Is.EqualTo(1));
            Assert.That(_runner.BattleRoyale.GetPlacement(new Side(1)), Is.EqualTo(2));
        }

        [Test]
        public void TheFallenDropWhatTheyCarriedButTheDownedDoNot()
        {
            var player = Member("Player", 0, ZeroSpawn);
            var enemy = Member("Bot-1-1", 1, OneSpawn);
            var enemyMate = Member("Bot-1-2", 1, OneSpawnTwo);
            Begin(new[] { player, enemy, enemyMate }, player);

            Assert.That(enemy.GetComponent<DropOnDeath>(), Is.Not.Null, "the runner gives everyone a DropOnDeath");

            KillBy(player, enemy);
            Assert.That(Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include), Is.Empty, "a down drops nothing");

            KillBy(player, enemyMate);

            // DropOnDeath の既定の Drops (予備弾 1 つ) を、全滅した 2 人がそれぞれ落とす。
            Assert.That(Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include).Length, Is.EqualTo(2));
        }

        [Test]
        public void EveryoneStartsUnarmedAndArmsByPickingUpAWeapon()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            var player = Member("Player", 0, ZeroSpawn);
            var weapons = AttachWeapons(player, rifle);
            var enemy = Member("Bot-1-1", 1, OneSpawn);
            Begin(new[] { player, enemy }, player);

            Assert.That(weapons.IsUnarmed, Is.True);
            Assert.That(weapons.Weapon, Is.Null);
            Assert.That(weapons.Fire(), Is.False);
            Assert.That(weapons.DescribeWeapons(out var none), Is.Empty);
            Assert.That(none, Is.Null);

            var pickup = Pickup.Create(PickupContents.ForWeapon(handgun, 10), player.transform.position);
            Assert.That(pickup.TryCollect(player.Damageable, out var outcome), Is.True);

            Assert.That(outcome.AddsWeapon, Is.True);
            Assert.That(weapons.IsUnarmed, Is.False);
            Assert.That(weapons.Loadout.Count, Is.EqualTo(1), "only the picked-up weapon, not the default loadout");
            Assert.That(weapons.Weapon.Spec.Id, Is.EqualTo(handgun.Id));
        }

        [Test]
        public void AFallenPlayerDropsTheWeaponTheyPickedUp()
        {
            var handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            var player = Member("Player", 0, ZeroSpawn);
            var weapons = AttachWeapons(player, NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()));
            var enemy = Member("Bot-1-1", 1, OneSpawn);
            Begin(new[] { player, enemy }, player);
            weapons.AddWeapon(handgun);

            KillBy(enemy, player);

            var dropped = Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include);
            Assert.That(Array.Exists(dropped, p => p.Spec.Kind == PickupKind.Weapon && p.Spec.WeaponId == handgun.Id), Is.True);
            Assert.That(weapons.IsUnarmed, Is.True, "what was dropped is no longer held");
        }

        [Test]
        public void TheZoneRunsOnTheMembersAndStopsWhenTheMatchEnds()
        {
            // 次の円は今の円の中に収まるので、中心は原点から 2m 以内に来る。原点のプレイヤーはずっと中にいる。
            var zone = Zone(10f, new ShrinkingZone.PhaseSettings(0f, 1f, 8f, 200f));
            _runner.Zone = zone;
            var player = Member("Player", 0, ZeroSpawn);
            var enemy = Member("Bot-1-1", 1, new Vector3(50f, 0f, 0f));
            Begin(new[] { player, enemy }, player);

            Assert.That(zone.IsRunning, Is.True);
            Assert.That(_runner.RunningZone, Is.SameAs(zone));

            zone.Tick(1f);

            Assert.That(enemy.IsAlive, Is.False, "the zone damage outside the circle takes the last of the squad");
            Assert.That(player.IsAlive, Is.True);

            _runner.Tick(0f);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)new Side(0)));
            Assert.That(zone.IsRunning, Is.False);
        }

        [Test]
        public void TheHudShowsSquadsPlacementAndTheZone()
        {
            var zone = Zone(30f, new ShrinkingZone.PhaseSettings(12f, 10f, 10f, 1f));
            _runner.Zone = zone;
            var player = Member("Player", 0, ZeroSpawn);
            var enemy = Member("Bot-1-1", 1, OneSpawn);
            var hud = BoundHudView();
            Begin(new[] { player, enemy }, player, hud);

            Assert.That(hud.TimeText, Is.EqualTo("00:00"), "without a time limit the HUD counts up");
            Assert.That(hud.Scores[0].Text, Is.EqualTo("ALPHA 残り 1"));
            Assert.That(hud.Scores[0].IsOwn, Is.True);
            Assert.That(hud.Lines, Does.Contain("残り分隊 2/2"));
            Assert.That(hud.Lines, Does.Contain("順位 2 位以内"));
            Assert.That(hud.Lines, Does.Contain("エリア縮小まで 00:12"));
            Assert.That(hud.Lines, Does.Contain("分隊 生存 1"));
            Assert.That(hud.ResultText, Is.EqualTo(HudFormat.NoResult));

            KillBy(player, enemy);
            _runner.Tick(0f);

            Assert.That(hud.Lines, Does.Contain("順位 1 位"));
            Assert.That(hud.ResultText, Is.EqualTo("ALPHA の勝ち"));
        }

        [Test]
        public void BeginNeedsABattleRoyaleSession()
        {
            var player = Member("Player", 0, ZeroSpawn);
            var session = TeamDeathmatchRunner.CreateSession(2, 4, 60f, 5, 1f);

            Assert.Throws<ArgumentException>(() => _runner.Begin(session, new[] { player }, _spawns, player));
            Assert.That(_runner.IsStarted, Is.False);
            Assert.Throws<ArgumentNullException>(() => _runner.Begin((BattleRoyaleSession)null, new[] { player }, _spawns, player));
        }

        [Test]
        public void CreateBattleRoyaleAlwaysUsesDownsAndZeroMeansNoTimeLimit()
        {
            var br = BattleRoyaleRunner.CreateBattleRoyale(4, 2, 0f, ActionRules.Default);

            Assert.That(ActionRules.Default.Downed, Is.EqualTo(Downed.Off));
            Assert.That(br.UsesDowned, Is.True);
            Assert.That(br.Mode.TimeLimitSeconds, Is.EqualTo(float.PositiveInfinity));
            Assert.That(BattleRoyaleRunner.CreateBattleRoyale(4, 2, 90f, ActionRules.Default).Mode.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleRunner.CreateBattleRoyale(4, 2, -1f, ActionRules.Default));
        }

        private void Begin(IReadOnlyList<SideMember> members, SideMember localPlayer, ModeHudView hud = null)
        {
            var br = BattleRoyaleRunner.CreateBattleRoyale(4, 2, 0f, ActionRules.Default);
            _runner.Begin(br, members, _spawns, localPlayer, hud);
        }

        private static void KillBy(SideMember killer, SideMember victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
        }

        private static void MoveNextTo(SideMember mover, SideMember target)
        {
            mover.transform.position = target.transform.position + new Vector3(1f, 0f, 0f);
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

        private WeaponController AttachWeapons(SideMember owner, WeaponDefinition definition)
        {
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(owner.transform, false);
            var weapons = camera.AddComponent<WeaponController>();
            weapons.Initialize(camera.transform, _attack, _reload, definition);
            return weapons;
        }

        private WeaponDefinition NewWeapon(string name, WeaponSpec spec)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = name;
            WeaponAssetBuilder.Configure(definition, spec);
            _objects.Add(definition);
            return definition;
        }

        private ShrinkingZone Zone(float initialRadius, ShrinkingZone.PhaseSettings phase)
        {
            var zone = Track(new GameObject("Zone")).AddComponent<ShrinkingZone>();
            zone.ShowsBoundary = false;
            zone.BeginsOnStart = false;
            zone.UseRandomSeed = false;
            zone.InitialRadius = initialRadius;
            zone.SetPhases(new[] { phase });
            return zone;
        }

        private SideSpawnPoint SpawnPoint(string name, int side, Vector3 position)
        {
            var point = Track(new GameObject(name)).AddComponent<SideSpawnPoint>();
            point.transform.position = position;
            point.Side = new Side(side);
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
