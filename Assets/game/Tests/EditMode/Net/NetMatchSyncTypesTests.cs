using Fps.Core;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace Fps.Net.Tests
{
    /// <summary>試合の同期に使う構造体 (NetworkVariable / NetworkList の中身) の値の扱い。</summary>
    public sealed class NetMatchSyncTypesTests
    {
        [Test]
        public void MatchStatusRoundTripsWinner()
        {
            Assert.That(new NetMatchStatus(GameModeState.Ended, Team.Alpha).Winner, Is.EqualTo(Team.Alpha));
            Assert.That(new NetMatchStatus(GameModeState.Ended, Team.Bravo).Winner, Is.EqualTo(Team.Bravo));
            Assert.That(new NetMatchStatus(GameModeState.Ended, null).Winner, Is.Null);
            Assert.That(new NetMatchStatus(GameModeState.Waiting, null).WinnerCode, Is.EqualTo(NetMatchStatus.NoWinner));
        }

        [Test]
        public void MatchStatusEqualityUsesStateAndWinner()
        {
            var playing = new NetMatchStatus(GameModeState.Playing, null);

            Assert.That(playing.Equals(new NetMatchStatus(GameModeState.Playing, null)), Is.True);
            Assert.That(playing.Equals(new NetMatchStatus(GameModeState.Ended, null)), Is.False);
            Assert.That(
                new NetMatchStatus(GameModeState.Ended, Team.Alpha).Equals(new NetMatchStatus(GameModeState.Ended, Team.Bravo)),
                Is.False);
        }

        [Test]
        public void PlayerStatsEqualityUsesAllFields()
        {
            var stats = new NetPlayerStats(3, Team.Bravo, 2, 1);

            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Bravo, 2, 1)), Is.True);
            Assert.That(stats.GetHashCode(), Is.EqualTo(new NetPlayerStats(3, Team.Bravo, 2, 1).GetHashCode()));
            Assert.That(stats.Equals(new NetPlayerStats(4, Team.Bravo, 2, 1)), Is.False);
            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Alpha, 2, 1)), Is.False);
            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Bravo, 3, 1)), Is.False);
            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Bravo, 2, 2)), Is.False);
        }

        [Test]
        public void PlayerStatsRoundTripsTheKiller()
        {
            Assert.That(new NetPlayerStats(3, Team.Bravo, 2, 1).Killer, Is.Null);
            Assert.That(new NetPlayerStats(3, Team.Bravo, 2, 1, 5).Killer, Is.EqualTo(5UL));

            // ホスト (クライアント 0) に倒されたことと、倒した相手がいないことを取り違えない
            Assert.That(new NetPlayerStats(3, Team.Bravo, 2, 1, 0).Killer, Is.EqualTo(0UL));
            Assert.That(default(NetPlayerStats).Killer, Is.Null);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => new NetPlayerStats(3, Team.Bravo, 2, 1, ulong.MaxValue));
        }

        [Test]
        public void PlayerStatsEqualityUsesTheKiller()
        {
            var stats = new NetPlayerStats(3, Team.Bravo, 2, 1, 0);

            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Bravo, 2, 1, 0)), Is.True);
            Assert.That(stats.GetHashCode(), Is.EqualTo(new NetPlayerStats(3, Team.Bravo, 2, 1, 0).GetHashCode()));
            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Bravo, 2, 1)), Is.False);
            Assert.That(stats.Equals(new NetPlayerStats(3, Team.Bravo, 2, 1, 4)), Is.False);
        }

        [Test]
        public void PlayerStatsSerializeAllFields()
        {
            var withKiller = new NetPlayerStats(7, Team.Alpha, 4, 2, 9);
            var withoutKiller = new NetPlayerStats(7, Team.Alpha, 4, 2);

            Assert.That(RoundTrip(withKiller), Is.EqualTo(withKiller));
            Assert.That(RoundTrip(withKiller).Killer, Is.EqualTo(9UL));
            Assert.That(RoundTrip(withoutKiller), Is.EqualTo(withoutKiller));
            Assert.That(RoundTrip(withoutKiller).Killer, Is.Null);
        }

        [Test]
        public void MatchRulesTakeOnlyTheClientValuesFromGameRules()
        {
            var rules = new GameRules(
                FriendlyFire.Reflect,
                RespawnMode.Wave,
                7f,
                2f,
                15f,
                DeathBody.Collapse,
                DeathCamera.Killer,
                true,
                0.5f,
                Destruction.On);

            var synced = NetMatchRules.From(rules);

            Assert.That(synced.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(synced.DeathCamera, Is.EqualTo(DeathCamera.Killer));
            Assert.That(synced.RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(synced.InfiniteReserve, Is.True);
            Assert.Throws<System.ArgumentNullException>(() => NetMatchRules.From(null));
        }

        [Test]
        public void MatchRulesDefaultMatchesTheDefaultGameRules()
        {
            var expected = new NetMatchRules(
                GameRules.Default.DeathBody, GameRules.Default.DeathCamera, GameRules.Default.RespawnMode, GameRules.Default.InfiniteReserve);

            Assert.That(NetMatchRules.Default, Is.EqualTo(expected));

            // NetworkVariable の初期値が届く前の値 (default) も既定のルールと同じ見た目になる
            Assert.That(default(NetMatchRules), Is.EqualTo(NetMatchRules.Default));
        }

        [Test]
        public void MatchRulesEqualityUsesAllFields()
        {
            var rules = new NetMatchRules(DeathBody.Collapse, DeathCamera.Killer, RespawnMode.Wave, true);

            Assert.That(rules.Equals(new NetMatchRules(DeathBody.Collapse, DeathCamera.Killer, RespawnMode.Wave, true)), Is.True);
            Assert.That(rules.GetHashCode(), Is.EqualTo(new NetMatchRules(DeathBody.Collapse, DeathCamera.Killer, RespawnMode.Wave, true).GetHashCode()));
            Assert.That(rules.Equals(new NetMatchRules(DeathBody.Vanish, DeathCamera.Killer, RespawnMode.Wave, true)), Is.False);
            Assert.That(rules.Equals(new NetMatchRules(DeathBody.Collapse, DeathCamera.None, RespawnMode.Wave, true)), Is.False);
            Assert.That(rules.Equals(new NetMatchRules(DeathBody.Collapse, DeathCamera.Killer, RespawnMode.None, true)), Is.False);
            Assert.That(rules.Equals(new NetMatchRules(DeathBody.Collapse, DeathCamera.Killer, RespawnMode.Wave, false)), Is.False);
        }

        [TestCase(DeathBody.Collapse, DeathCamera.Killer, RespawnMode.Wave, true)]
        [TestCase(DeathBody.Vanish, DeathCamera.None, RespawnMode.Individual, false)]
        [TestCase(DeathBody.Collapse, DeathCamera.None, RespawnMode.None, false)]
        public void MatchRulesSerializeAllFields(DeathBody deathBody, DeathCamera deathCamera, RespawnMode respawnMode, bool infiniteReserve)
        {
            var rules = new NetMatchRules(deathBody, deathCamera, respawnMode, infiniteReserve);

            Assert.That(RoundTrip(rules), Is.EqualTo(rules));
        }

        [Test]
        public void MatchStatusSerializesStateAndWinner()
        {
            var status = new NetMatchStatus(GameModeState.Ended, Team.Bravo);

            Assert.That(RoundTrip(status), Is.EqualTo(status));
            Assert.That(RoundTrip(new NetMatchStatus(GameModeState.Ended, null)).Winner, Is.Null);
        }

        [Test]
        public void WeaponStateSerializesAllFields()
        {
            var idle = new NetWeaponState(30, 90, false, 1, 2, false);
            var reloading = new NetWeaponState(0, 60, true, 0, 2, false, 1234.5678d, 2.5f, 0.5f, 0.65f);

            Assert.That(RoundTrip(idle), Is.EqualTo(idle));
            Assert.That(RoundTrip(reloading), Is.EqualTo(reloading));
            Assert.That(RoundTrip(reloading).ReloadStartedAt, Is.EqualTo(1234.5678d), "the server time keeps its double precision");
        }

        [Test]
        public void ReloadProgressIsComputedFromTheSyncedStartAndLength()
        {
            var state = RoundTrip(new NetWeaponState(0, 60, true, 0, 1, false, 100d, 2f, 0.5f, 0.65f));

            Assert.That(state.HasReloadProgress, Is.True);
            Assert.That(state.HasActiveReloadWindow, Is.True);
            Assert.That(state.ReloadProgressAt(100d), Is.Zero);
            Assert.That(state.ReloadProgressAt(101d), Is.EqualTo(0.5f));
            Assert.That(state.ReloadProgressAt(101.5d), Is.EqualTo(0.75f));
            Assert.That(state.ReloadProgressAt(99d), Is.Zero, "a clock slightly behind the start does not go negative");
            Assert.That(state.ReloadProgressAt(103d), Is.EqualTo(1f), "a late frame does not overshoot");
            Assert.That(state.ReloadProgressAt(double.NaN), Is.Zero);
        }

        [Test]
        public void ReloadProgressMatchesTheServerWeaponAfterTheRoundTrip()
        {
            var a = ActionRules.Default;
            var d = GameRules.Default;
            var rules = new GameRules(
                d.FriendlyFire, d.RespawnMode, d.WaveIntervalSeconds, d.RegenDelaySeconds, d.RegenPerSecond, d.DeathBody, d.DeathCamera,
                d.InfiniteReserve, d.ReserveAmmoMultiplier, d.Destruction,
                new ActionRules(a.Downed, a.DownedBleedoutSeconds, a.DownedHealthFraction, a.ReviveSeconds, a.Cover, a.BlindFire, ActiveReload.On));
            var spec = new WeaponSpec(
                "00000000-0000-4000-8000-000000000001", null, WeaponCategory.AssaultRifle, FireMode.Auto, 3, 10, 0.1f, 2f, 25, 100f, DamageFalloff.None,
                activeReloadWindowStart: 0.5f,
                activeReloadWindowLength: 0.25f,
                activeReloadFailSeconds: 0.5f);
            var weapon = new ServerWeapon(spec, rules);
            weapon.TryFire();
            Assert.That(weapon.TryStartReload(), Is.True);
            const double startedAt = 50d;

            weapon.Tick(0.5f);
            var state = RoundTrip(NetPlayerWeapon.BuildState(weapon, 0, 1, false, startedAt));
            Assert.That(state.ReloadProgressAt(startedAt + 0.5d), Is.EqualTo(weapon.ReloadProgress));
            Assert.That(state.ActiveReloadWindowStart, Is.EqualTo(0.5f));
            Assert.That(state.ActiveReloadWindowEnd, Is.EqualTo(0.75f));

            // 失敗で長さが延びても、開始時刻は変えずに同じ進みになる
            Assert.That(weapon.TryActiveReload(out var result), Is.True);
            Assert.That(result, Is.EqualTo(ActiveReloadResult.Fail));
            weapon.Tick(0.75f);
            var failed = RoundTrip(NetPlayerWeapon.BuildState(weapon, 0, 1, false, startedAt));
            Assert.That(failed.ReloadSeconds, Is.EqualTo(2.5f));
            Assert.That(failed.ReloadProgressAt(startedAt + 1.25d), Is.EqualTo(weapon.ReloadProgress));
            Assert.That(failed.HasActiveReloadWindow, Is.False);
        }

        [Test]
        public void DownedStateSerializesAllFields()
        {
            var downed = new NetDownedState(true, 12, 42);
            var full = new NetDownedState(true, 0, 100);

            Assert.That(RoundTrip(downed), Is.EqualTo(downed));
            Assert.That(RoundTrip(full), Is.EqualTo(full));
            Assert.That(RoundTrip(NetDownedState.None), Is.EqualTo(NetDownedState.None));
            Assert.That(RoundTrip(downed).ToString(), Is.EqualTo("downed=true bleedout=12 revive=42%"));
        }

        [Test]
        public void DownedStateEqualityUsesAllFields()
        {
            var state = new NetDownedState(true, 12, 42);

            Assert.That(state.Equals(new NetDownedState(true, 12, 42)), Is.True);
            Assert.That(state.GetHashCode(), Is.EqualTo(new NetDownedState(true, 12, 42).GetHashCode()));
            Assert.That(state.Equals(new NetDownedState(false, 12, 42)), Is.False);
            Assert.That(state.Equals(new NetDownedState(true, 11, 42)), Is.False);
            Assert.That(state.Equals(new NetDownedState(true, 12, 43)), Is.False);
            Assert.That(default(NetDownedState), Is.EqualTo(NetDownedState.None), "the initial value means not downed");
        }

        [Test]
        public void DownedStateIsQuantizedToWhatTheHudShows()
        {
            Assert.That(NetDownedState.From(false, 3f, 0.5f), Is.EqualTo(NetDownedState.None), "values are dropped when not downed");
            Assert.That(NetDownedState.From(true, 14.2f, 0f), Is.EqualTo(new NetDownedState(true, 15, 0)));
            Assert.That(NetDownedState.From(true, 0.01f, 0.426f), Is.EqualTo(new NetDownedState(true, 1, 42)));
            Assert.That(NetDownedState.From(true, null, 1f), Is.EqualTo(new NetDownedState(true, 0, 100)));
        }

        // NetworkVariable / NetworkList が送るときと同じく INetworkSerializable で書いて読み戻す
        private static T RoundTrip<T>(T value)
            where T : INetworkSerializable, new()
        {
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(value);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out T result);
                    Assert.That(reader.Position, Is.EqualTo(writer.Length), "every written byte should be read back");
                    return result;
                }
            }
        }

        [TestCase(NetSpawnPoint.SpawnTeam.Any, null)]
        [TestCase(NetSpawnPoint.SpawnTeam.Alpha, Team.Alpha)]
        [TestCase(NetSpawnPoint.SpawnTeam.Bravo, Team.Bravo)]
        public void SpawnTeamMapsToCoreTeam(NetSpawnPoint.SpawnTeam spawnTeam, Team? expected)
        {
            Assert.That(NetSpawnPoint.ToCoreTeam(spawnTeam), Is.EqualTo(expected));
        }
    }
}
