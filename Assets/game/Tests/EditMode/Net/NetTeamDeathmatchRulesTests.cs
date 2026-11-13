using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Net.Tests
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> が試合の開始時にルールのアセットを読む経路 (<see cref="NetTeamDeathmatch.TryLoadRules"/>) と、
    /// 倒された人ごとに最後に倒した相手を持つ <see cref="LastKillerTable"/> を、実際の <see cref="TeamDeathmatchSession"/> の
    /// Killed で確かめる (NetTeamDeathmatch.HandleKilled と同じつなぎ方)。
    /// </summary>
    public sealed class NetTeamDeathmatchRulesTests
    {
        private const int _maxHealth = 100;

        // Join の順に Alpha・Bravo・Alpha・Bravo
        private const int _alpha1 = 0;
        private const int _bravo1 = 1;
        private const int _alpha2 = 2;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private GameRulesDefinition RulesAsset(string assetName)
        {
            var asset = ScriptableObject.CreateInstance<GameRulesDefinition>();
            _created.Add(asset);
            asset.name = assetName;
            return asset;
        }

        [Test]
        public void UnassignedRulesFallBackToTheDefault()
        {
            Assert.That(NetTeamDeathmatch.TryLoadRules(null, out var rules, out var usedDefault, out var error), Is.True);
            Assert.That(rules, Is.SameAs(GameRules.Default));
            Assert.That(usedDefault, Is.True);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void DestroyedRulesAssetFallsBackToTheDefault()
        {
            var asset = RulesAsset("Destroyed");
            Object.DestroyImmediate(asset);

            Assert.That(NetTeamDeathmatch.TryLoadRules(asset, out var rules, out var usedDefault, out _), Is.True);
            Assert.That(rules, Is.SameAs(GameRules.Default));
            Assert.That(usedDefault, Is.True);
        }

        [Test]
        public void InvalidRulesAreRejectedWithTheAssetName()
        {
            var asset = RulesAsset("BrokenRules");
            asset.WaveIntervalSeconds = 0f;

            Assert.That(NetTeamDeathmatch.TryLoadRules(asset, out var rules, out var usedDefault, out var error), Is.False);
            Assert.That(rules, Is.Null);
            Assert.That(usedDefault, Is.False);
            Assert.That(error, Does.Contain("BrokenRules"));
            Assert.That(error, Does.Contain("waveIntervalSeconds"));
        }

        [Test]
        public void ValidRulesAreReadFromTheAsset()
        {
            var asset = RulesAsset("WaveRules");
            asset.FriendlyFire = FriendlyFire.On;
            asset.RespawnMode = RespawnMode.Wave;
            asset.WaveIntervalSeconds = 6f;
            asset.RegenDelaySeconds = 4f;
            asset.RegenPerSecond = 20f;
            asset.DeathBody = DeathBody.Collapse;
            asset.DeathCamera = DeathCamera.Killer;
            asset.InfiniteReserve = true;
            asset.ReserveAmmoMultiplier = 1.5f;
            asset.Destruction = Destruction.On;

            Assert.That(NetTeamDeathmatch.TryLoadRules(asset, out var rules, out var usedDefault, out var error), Is.True, error);
            Assert.That(usedDefault, Is.False);
            Assert.That(error, Is.Null);
            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.On));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(6f));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(4f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Killer));
            Assert.That(rules.InfiniteReserve, Is.True);
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1.5f));
            Assert.That(rules.Destruction, Is.EqualTo(Destruction.On));

            // StartSession と同じく Spec に渡すと、サーバーの判定はこのルールで動く
            var spec = new TeamDeathmatchSpec(10, 300f, 3f, rules);
            Assert.That(spec.Rules, Is.SameAs(rules));

            // クライアントへは見た目と HUD に要る値だけを送る
            Assert.That(NetMatchRules.From(spec.Rules), Is.EqualTo(new NetMatchRules(DeathBody.Collapse, DeathCamera.Killer, RespawnMode.Wave, true)));
        }

        [Test]
        public void StartLogListsEveryRuleValue()
        {
            var log = MatchLog.Rules("TeamDeathmatch", new GameRules(
                FriendlyFire.Reflect, RespawnMode.Wave, 6f, 4f, 20f, DeathBody.Collapse, DeathCamera.Killer, true, 1.5f, Destruction.On));

            Assert.That(log, Is.EqualTo(
                "[Match] rules name=TeamDeathmatch friendlyFire=Reflect respawnMode=Wave waveInterval=6.00s regenDelay=4.00s"
                + " regenPerSecond=20.00 deathBody=Collapse deathCamera=Killer infiniteReserve=true reserveMultiplier=1.50 destruction=On"));
            Assert.That(MatchLog.Rules("default", GameRules.Default), Does.StartWith("[Match] rules name=default friendlyFire=Off"));
        }

        [Test]
        public void EnemyKillRecordsTheKiller()
        {
            var (session, killers) = StartSession(FriendlyFire.Off);

            Assert.That(killers.Get(_bravo1), Is.Null, "nobody has been killed yet");
            session.ApplyDamage(_alpha1, _bravo1, _maxHealth);

            Assert.That(killers.Get(_bravo1), Is.EqualTo(_alpha1));
            Assert.That(killers.Get(_alpha1), Is.Null);
        }

        [Test]
        public void TeamKillRecordsTheAlly()
        {
            var (session, killers) = StartSession(FriendlyFire.On);

            session.ApplyDamage(_alpha2, _alpha1, _maxHealth);

            Assert.That(killers.Get(_alpha1), Is.EqualTo(_alpha2));
        }

        [Test]
        public void ReflectedDeathHasNoKillerOtherThanOneself()
        {
            var (session, killers) = StartSession(FriendlyFire.Reflect);

            // 前に敵に倒された記録があっても、跳ね返りで倒れたら自分以外の相手はいない
            session.ApplyDamage(_bravo1, _alpha1, _maxHealth);
            Assert.That(killers.Get(_alpha1), Is.EqualTo(_bravo1));
            session.Tick(10f);
            Assert.That(session.IsAlive(_alpha1), Is.True, "alpha1 should have respawned");

            session.ApplyDamage(_alpha1, _alpha2, _maxHealth);

            Assert.That(session.IsAlive(_alpha1), Is.False, "the shot is reflected to the shooter");
            Assert.That(session.IsAlive(_alpha2), Is.True);
            Assert.That(killers.Get(_alpha1), Is.Null);
            Assert.That(killers.Get(_alpha2), Is.Null);
        }

        [Test]
        public void SelfKillHasNoKiller()
        {
            var (session, killers) = StartSession(FriendlyFire.Off);

            session.ApplyDamage(_alpha1, _alpha1, _maxHealth);

            Assert.That(killers.Get(_alpha1), Is.Null);
        }

        [Test]
        public void TheLastKillerReplacesTheEarlierOne()
        {
            var (session, killers) = StartSession(FriendlyFire.On);

            session.ApplyDamage(_bravo1, _alpha1, _maxHealth);
            session.Tick(10f);
            session.ApplyDamage(_alpha2, _alpha1, _maxHealth);

            Assert.That(killers.Get(_alpha1), Is.EqualTo(_alpha2));
        }

        [Test]
        public void RemoveAndClearForgetTheKiller()
        {
            var (session, killers) = StartSession(FriendlyFire.Off);
            session.ApplyDamage(_alpha1, _bravo1, _maxHealth);

            killers.Remove(_bravo1);
            Assert.That(killers.Get(_bravo1), Is.Null);

            session.Tick(10f);
            session.ApplyDamage(_alpha1, _bravo1, _maxHealth);
            killers.Clear();
            Assert.That(killers.Get(_bravo1), Is.Null);
        }

        private static (TeamDeathmatchSession Session, LastKillerTable Killers) StartSession(FriendlyFire friendlyFire)
        {
            var rules = new GameRules(
                friendlyFire,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.Killer,
                false,
                1f,
                Destruction.Off);
            var points = new[]
            {
                new SpawnPoint(new System.Numerics.Vector3(0f, 0f, -20f), Team.Alpha),
                new SpawnPoint(new System.Numerics.Vector3(0f, 0f, 20f), Team.Bravo),
            };
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(100, 300f, 1f, rules), _maxHealth, points, new SpawnSelector(), _ => System.Numerics.Vector3.Zero);

            var killers = new LastKillerTable();
            session.Killed += killers.Record;

            Assert.That(session.Join(_alpha1), Is.EqualTo(Team.Alpha));
            Assert.That(session.Join(_bravo1), Is.EqualTo(Team.Bravo));
            Assert.That(session.Join(_alpha2), Is.EqualTo(Team.Alpha));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            return (session, killers);
        }
    }
}
