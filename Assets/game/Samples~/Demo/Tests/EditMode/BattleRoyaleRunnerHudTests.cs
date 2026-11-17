using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class BattleRoyaleRunnerHudTests
    {
        private static readonly Vector3 ZeroSpawn = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 OneSpawn = new Vector3(0f, 0f, 8f);

        private readonly List<Object> _objects = new List<Object>();
        private BattleRoyaleRunner _runner;
        private SideSpawnPoint[] _spawns;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
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

            GameSignals.ResetAll();
        }

        [Test]
        public void TheHudShowsSquadsPlacementAndTheZone()
        {
            ModeHudFormatters.Register<BattleRoyaleStatus>(new BattleRoyaleHudFormatter());
            try
            {
                var zone = Zone(30f, new ShrinkingZone.PhaseSettings(12f, 10f, 10f, 1f));
                _runner.Zone = zone;
                var player = Member("Player", 0, ZeroSpawn);
                var enemy = Member("Bot-1-1", 1, OneSpawn);
                var hud = BoundHudView();
                Begin(new[] { player, enemy }, player);

                Assert.That(hud.TimeText, Is.EqualTo("00:00"), "without a time limit the HUD counts up");
                Assert.That(hud.ScoreTexts[0], Is.EqualTo("ALPHA 残り 1"));
                Assert.That(hud.Scores[0].IsOwn, Is.True);
                Assert.That(hud.Lines, Does.Contain("残り分隊 2/2"));
                Assert.That(hud.Lines, Does.Contain("順位 2 位以内"));
                Assert.That(hud.Lines, Does.Contain("エリア縮小まで 00:12"));
                Assert.That(hud.Lines, Does.Contain("分隊 生存 1"));
                Assert.That(hud.ResultText, Is.Empty);

                KillBy(player, enemy);
                _runner.Tick(0f);

                Assert.That(hud.Lines, Does.Contain("順位 1 位"));
                Assert.That(hud.ResultText, Is.EqualTo("ALPHA の勝ち"));
            }
            finally
            {
                ModeHudFormatters.Unregister(typeof(BattleRoyaleStatus));
            }
        }

        private void Begin(IReadOnlyList<SideMember> members, SideMember localPlayer)
        {
            var br = BattleRoyaleRunner.CreateBattleRoyale(4, 2, 0f, ActionRules.Default);
            _runner.Begin(br, members, _spawns, localPlayer);
        }

        private static void KillBy(SideMember killer, SideMember victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
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
