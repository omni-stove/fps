using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// <see cref="NetHud"/> が NetworkVariable の変化を HUD の Label に写すこと。ネットワークは起動せず、
    /// 値が届いたときに NGO が呼ぶのと同じ OnValueChanged を直接呼ぶ。Label は Edit Mode でも木が作れるよう
    /// Hud.uxml を複製して <see cref="HudView"/> を作る (<c>HudViewTests</c> と同じ)。
    /// </summary>
    public sealed class NetHudTests
    {
        private const float _respawnSeconds = 3f;
        private const int _maxHealth = 100;

        private GameObject _playerObject;
        private GameObject _matchObject;
        private NetPlayer _player;
        private NetPlayerWeapon _weapon;
        private NetTeamDeathmatch _match;
        private NetHud _netHud;
        private HudView _view;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("TestNetPlayer");
            _playerObject.AddComponent<NetworkObject>();
            _player = _playerObject.AddComponent<NetPlayer>();
            _weapon = _playerObject.AddComponent<NetPlayerWeapon>();

            _matchObject = new GameObject("TestNetMatch");
            _matchObject.AddComponent<NetworkObject>();
            _match = _matchObject.AddComponent<NetTeamDeathmatch>();
            _match.RespawnSeconds = _respawnSeconds;
            _match.MaxHealth = _maxHealth;

            _netHud = NetHud.Create(_player);
            _view = new HudView(CloneLayout(), _netHud.Hud.Model);
            _view.SetTeamColors(NetPlayer.AlphaColor, NetPlayer.BravoColor);
        }

        [TearDown]
        public void TearDown()
        {
            _view?.Dispose();

            if (_netHud != null)
            {
                // Edit Mode では OnDestroy が走らないので、試合の差し替えの購読を他のテストへ持ち越さない
                _netHud.UnbindAll();
                var settings = _netHud.Hud.OwnedPanelSettings;
                Object.DestroyImmediate(_netHud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }
            }

            if (_playerObject != null)
            {
                Object.DestroyImmediate(_playerObject);
            }

            if (_matchObject != null)
            {
                Object.DestroyImmediate(_matchObject);
            }
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        // サーバーから値が届いたときに NGO が呼ぶのと同じ呼び出し
        private static void Arrive<T>(NetworkVariable<T> variable, T previous, T current)
        {
            Assert.That(variable.OnValueChanged, Is.Not.Null, "NetHud should listen to this NetworkVariable");
            variable.OnValueChanged(previous, current);
        }

        private static NetMatchStatus Status(GameModeState state, Team? winner = null)
        {
            return new NetMatchStatus(state, winner);
        }

        private void StartPlaying()
        {
            Arrive(_match.StatusVariable, Status(GameModeState.Waiting), Status(GameModeState.Playing));
        }

        private static bool IsShown(VisualElement element)
        {
            return element.style.display.value != DisplayStyle.None;
        }

        [Test]
        public void CreatesAHudObjectWithTheTeamColors()
        {
            Assert.That(_netHud.name, Is.EqualTo(NetHud.ObjectName));
            Assert.That(_netHud.GetComponent<UIDocument>(), Is.Not.Null);
            Assert.That(_netHud.Hud.OwnedPanelSettings, Is.Not.Null);
            Assert.That(_netHud.Player, Is.SameAs(_player));
            Assert.That(_netHud.Hud.TeamColor(Team.Alpha), Is.EqualTo(NetPlayer.AlphaColor));
            Assert.That(_netHud.Hud.TeamColor(Team.Bravo), Is.EqualTo(NetPlayer.BravoColor));
        }

        [Test]
        public void ThePauseMenuKeepsTheMatchTimeRunning()
        {
            var pause = _netHud.Hud.PauseMenu;
            Assert.That(pause, Is.Not.Null);
            Assert.That(pause.TimeMode, Is.EqualTo(PauseTimeMode.KeepRunning));

            var timeScaleBefore = Time.timeScale;
            try
            {
                pause.Open();
                Assert.That(Time.timeScale, Is.EqualTo(timeScaleBefore), "opening the pause menu online must not stop time");
            }
            finally
            {
                // Edit Mode では OnDisable が走らないので、開いたまま他のテストへ持ち越さない
                pause.Close();
            }
        }

        [Test]
        public void WithoutAMatchTheScoresAndTimeAreHidden()
        {
            Assert.That(_netHud.Match, Is.Null);
            Assert.That(IsShown(_view.ScorePanel), Is.False);
            Assert.That(IsShown(_view.ResultLabel), Is.False);
            Assert.That(_view.TimeLabel.text, Is.EqualTo(HudFormat.NoTime));
            Assert.That(_view.Crosshair, Is.Not.Null, "the crosshair stays in the centre of the screen");
        }

        [Test]
        public void BindingTheMatchShowsTheScoresAndTime()
        {
            _netHud.BindMatch(_match);

            Assert.That(_netHud.Match, Is.SameAs(_match));
            Assert.That(IsShown(_view.ScorePanel), Is.True);
            Assert.That(_view.FirstScoreLabel.text, Is.EqualTo(HudFormat.TeamScore(Team.Alpha, 0)));
            Assert.That(_view.SecondScoreLabel.text, Is.EqualTo(HudFormat.TeamScore(Team.Bravo, 0)));
            Assert.That(_view.TimeLabel.text, Is.EqualTo(HudFormat.Time(0f)));
        }

        [Test]
        public void HealthFollowsThePlayerAgainstTheMaxHealth()
        {
            _netHud.BindMatch(_match);

            Arrive(_player.HealthVariable, 0, _maxHealth);
            Assert.That(_view.HealthLabel.text, Is.EqualTo("100"));
            Assert.That(_view.HealthLabel.ClassListContains(HudView.LowHealthClass), Is.False);

            Arrive(_player.HealthVariable, _maxHealth, 20);
            Assert.That(_view.HealthLabel.text, Is.EqualTo("20"));
            Assert.That(_view.HealthLabel.ClassListContains(HudView.LowHealthClass), Is.True);
        }

        [Test]
        public void AmmoFollowsTheWeaponAndShowsTheReload()
        {
            Arrive(_weapon.StateVariable, default, new NetWeaponState(30, 90, false, 0, 1, false));
            Assert.That(_view.AmmoLabel.text, Is.EqualTo("30 / 90"));

            Arrive(_weapon.StateVariable, new NetWeaponState(30, 90, false, 0, 1, false), new NetWeaponState(0, 90, true, 0, 1, false));
            Assert.That(_view.AmmoLabel.text, Is.EqualTo(HudFormat.Ammo(0, 90, true)));
            Assert.That(_view.AmmoLabel.ClassListContains(HudView.ReloadingClass), Is.True);

            Arrive(_weapon.StateVariable, new NetWeaponState(0, 90, true, 0, 1, false), new NetWeaponState(30, 60, false, 0, 1, false));
            Assert.That(_view.AmmoLabel.text, Is.EqualTo("30 / 60"));
            Assert.That(_view.AmmoLabel.ClassListContains(HudView.ReloadingClass), Is.False);
        }

        [Test]
        public void ReloadBarFollowsTheSyncedReloadAndWindow()
        {
            var idle = new NetWeaponState(30, 90, false, 0, 1, false);
            var reloading = new NetWeaponState(0, 90, true, 0, 1, false, 10d, 2f, 0.5f, 0.65f);
            var failed = new NetWeaponState(0, 90, true, 0, 1, false, 10d, 2.5f, 0f, 0f);
            var model = _netHud.Hud.Model;

            Arrive(_weapon.StateVariable, default, idle);
            _netHud.RefreshReloadProgress(10d);
            Assert.That(model.ReloadProgress, Is.Null);
            Assert.That(IsShown(_view.ReloadBar), Is.False);

            Arrive(_weapon.StateVariable, idle, reloading);
            _netHud.RefreshReloadProgress(11d);
            Assert.That(model.ReloadProgress, Is.EqualTo(0.5f));
            Assert.That(model.ReloadWindowStart, Is.EqualTo(0.5f));
            Assert.That(model.ReloadWindowEnd, Is.EqualTo(0.65f));
            Assert.That(IsShown(_view.ReloadBar), Is.True);
            Assert.That(IsShown(_view.ReloadWindow), Is.True);

            // 失敗すると長さが延びて幅は届かなくなる。オフラインと同じく進みだけを出す
            Arrive(_weapon.StateVariable, reloading, failed);
            _netHud.RefreshReloadProgress(11.25d);
            Assert.That(model.ReloadProgress, Is.EqualTo(0.5f));
            Assert.That(model.ReloadWindowEnd, Is.Zero);
            Assert.That(IsShown(_view.ReloadBar), Is.True);
            Assert.That(IsShown(_view.ReloadWindow), Is.False);

            Arrive(_weapon.StateVariable, failed, new NetWeaponState(30, 60, false, 0, 1, false));
            Assert.That(model.ReloadProgress, Is.Null, "the bar goes away as soon as the reload ends");
            Assert.That(IsShown(_view.ReloadBar), Is.False);
        }

        [Test]
        public void OffRuleShowsNoReloadBar()
        {
            // Off のルールではサーバーが長さを 0 で送る
            Arrive(_weapon.StateVariable, default, new NetWeaponState(0, 90, true, 0, 1, false));
            _netHud.RefreshReloadProgress(11d);

            Assert.That(_netHud.Hud.Model.ReloadProgress, Is.Null);
            Assert.That(IsShown(_view.ReloadBar), Is.False);
            Assert.That(_view.AmmoLabel.ClassListContains(HudView.ReloadingClass), Is.True, "the usual reload is still shown");
        }

        [Test]
        public void InfiniteReserveShowsInfinityWhicheverArrivesFirst()
        {
            _netHud.BindMatch(_match);
            var infinite = new NetMatchRules(DeathBody.Vanish, DeathCamera.None, RespawnMode.Individual, true);

            // 武器の状態が先に届き、ルールが後から届く
            Arrive(_weapon.StateVariable, default, new NetWeaponState(30, 0, false, 0, 1, false));
            Assert.That(_view.AmmoLabel.text, Is.EqualTo("30 / 0"));
            Arrive(_match.RulesVariable, NetMatchRules.Default, infinite);
            Assert.That(_view.AmmoLabel.text, Is.EqualTo("30 / " + HudFormat.InfiniteReserve));

            // ルールが届いた後の弾数の変化とリロードも ∞ のまま
            Arrive(_weapon.StateVariable, new NetWeaponState(30, 0, false, 0, 1, false), new NetWeaponState(0, 0, true, 0, 1, false));
            Assert.That(_view.AmmoLabel.text, Is.EqualTo(HudFormat.Ammo(0, 0, true, true)));
            Assert.That(_view.AmmoLabel.ClassListContains(HudView.ReloadingClass), Is.True);

            // 次の試合で有限に戻れば数を出す
            Arrive(_match.RulesVariable, infinite, NetMatchRules.Default);
            Assert.That(_view.AmmoLabel.text, Is.EqualTo(HudFormat.Ammo(0, 0, true)));
        }

        [Test]
        public void TimeFollowsTheRemainingSeconds()
        {
            _netHud.BindMatch(_match);

            Arrive(_match.RemainingSecondsVariable, 0, 125);
            Assert.That(_view.TimeLabel.text, Is.EqualTo("02:05"));

            Arrive(_match.RemainingSecondsVariable, 125, 124);
            Assert.That(_view.TimeLabel.text, Is.EqualTo("02:04"));
        }

        [Test]
        public void ScoresPutTheOwnTeamFirstInItsColor()
        {
            _netHud.BindMatch(_match);

            Arrive(_player.TeamVariable, Team.Alpha, Team.Bravo);
            Arrive(_match.AlphaScoreVariable, 0, 2);
            Arrive(_match.BravoScoreVariable, 0, 5);

            Assert.That(_view.FirstScoreLabel.text, Is.EqualTo("BRAVO 5"));
            Assert.That(_view.SecondScoreLabel.text, Is.EqualTo("ALPHA 2"));
            Assert.That(_view.FirstScoreLabel.style.color.value, Is.EqualTo(NetPlayer.BravoColor));
            Assert.That(_view.SecondScoreLabel.style.color.value, Is.EqualTo(NetPlayer.AlphaColor));

            Arrive(_player.TeamVariable, Team.Bravo, Team.Alpha);
            Assert.That(_view.FirstScoreLabel.text, Is.EqualTo("ALPHA 2"));
            Assert.That(_view.FirstScoreLabel.style.color.value, Is.EqualTo(NetPlayer.AlphaColor));
        }

        [Test]
        public void ShowsTheRespawnCountdownWhileDown()
        {
            _netHud.BindMatch(_match);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, _maxHealth);
            Assert.That(IsShown(_view.NoticeLabel), Is.False);

            Arrive(_player.HealthVariable, _maxHealth, 0);
            Assert.That(_netHud.IsAwaitingRespawn, Is.True);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.Respawn(3)));
            Assert.That(IsShown(_view.NoticeLabel), Is.True);

            _netHud.Tick(1.2f);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.Respawn(2)));

            // 数え終わってもサーバーが出現させるまでは 0 秒のまま待つ
            _netHud.Tick(5f);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.Respawn(0)));

            Arrive(_player.HealthVariable, 0, _maxHealth);
            Assert.That(_netHud.IsAwaitingRespawn, Is.False);
            Assert.That(IsShown(_view.NoticeLabel), Is.False);
        }

        private void ArriveRespawnMode(RespawnMode mode)
        {
            Arrive(
                _match.RulesVariable,
                NetMatchRules.Default,
                new NetMatchRules(DeathBody.Collapse, DeathCamera.Killer, mode, false));
        }

        [Test]
        public void WaveShowsTheSecondsUntilTheNextWaveWhileDown()
        {
            _netHud.BindMatch(_match);
            ArriveRespawnMode(RespawnMode.Wave);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, _maxHealth);
            Arrive(_match.NextWaveSecondsVariable, 0, 7);
            Assert.That(IsShown(_view.NoticeLabel), Is.False, "the wave countdown is shown only while down");

            Arrive(_player.HealthVariable, _maxHealth, 0);
            Assert.That(_netHud.IsAwaitingRespawn, Is.True);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.Respawn(7)));

            // 自分の待ち時間ではなく、サーバーが配る次のウェーブまでの秒数で進む
            _netHud.Tick(1.2f);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.Respawn(7)));
            Arrive(_match.NextWaveSecondsVariable, 7, 6);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.Respawn(6)));

            Arrive(_player.HealthVariable, 0, _maxHealth);
            Assert.That(_netHud.IsAwaitingRespawn, Is.False);
            Assert.That(IsShown(_view.NoticeLabel), Is.False);
        }

        [Test]
        public void NoneShowsThatThereIsNoRespawnUntilTheNextMatch()
        {
            _netHud.BindMatch(_match);
            ArriveRespawnMode(RespawnMode.None);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, _maxHealth);

            Arrive(_player.HealthVariable, _maxHealth, 0);
            Assert.That(_netHud.IsAwaitingRespawn, Is.True);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.NoRespawnUntilNextMatch));

            _netHud.Tick(10f);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.NoRespawnUntilNextMatch));

            // 終わったら次の試合までの秒数に替わる
            Arrive(_match.StatusVariable, Status(GameModeState.Playing), Status(GameModeState.Ended, Team.Alpha));
            Arrive(_match.NextMatchSecondsVariable, 0, 10);
            Assert.That(_netHud.IsAwaitingRespawn, Is.False);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.NextMatch(10)));
        }

        [Test]
        public void RulesArrivingWhileDownSwitchTheNotice()
        {
            Assert.That(_match.SyncedRules, Is.EqualTo(NetMatchRules.Default), "before the server starts the match");
            _netHud.BindMatch(_match);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, _maxHealth);
            Arrive(_player.HealthVariable, _maxHealth, 0);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.Respawn(3)));

            ArriveRespawnMode(RespawnMode.None);

            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.NoRespawnUntilNextMatch));
        }

        [Test]
        public void NoRespawnCountdownBeforeTheMatchIsPlaying()
        {
            _netHud.BindMatch(_match);

            Arrive(_player.HealthVariable, _maxHealth, 0);

            Assert.That(_netHud.IsAwaitingRespawn, Is.False);
            Assert.That(IsShown(_view.NoticeLabel), Is.False);
        }

        [Test]
        public void ShowsTheResultAndTheNextMatchCountdownAfterTheEnd()
        {
            _netHud.BindMatch(_match);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, _maxHealth);
            Arrive(_player.HealthVariable, _maxHealth, 0);

            Arrive(_match.StatusVariable, Status(GameModeState.Playing), Status(GameModeState.Ended, Team.Bravo));
            Arrive(_match.NextMatchSecondsVariable, 0, 10);

            Assert.That(_netHud.IsAwaitingRespawn, Is.False, "nobody respawns until the next match starts");
            Assert.That(_view.ResultLabel.text, Is.EqualTo(HudFormat.Result(Team.Bravo)));
            Assert.That(IsShown(_view.ResultLabel), Is.True);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.NextMatch(10)));

            Arrive(_match.NextMatchSecondsVariable, 10, 9);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.NextMatch(9)));

            // クライアントには Ended → Playing で届くことがある
            Arrive(_match.StatusVariable, Status(GameModeState.Ended, Team.Bravo), Status(GameModeState.Playing));
            Arrive(_match.NextMatchSecondsVariable, 9, 0);
            Assert.That(IsShown(_view.ResultLabel), Is.False);
            Assert.That(IsShown(_view.NoticeLabel), Is.False);
        }

        [Test]
        public void ShowsADraw()
        {
            _netHud.BindMatch(_match);

            Arrive(_match.StatusVariable, Status(GameModeState.Playing), Status(GameModeState.Ended));

            Assert.That(_view.ResultLabel.text, Is.EqualTo(HudFormat.Draw));
        }

        [Test]
        public void UnbindingTheMatchHidesItsFields()
        {
            _netHud.BindMatch(_match);
            Arrive(_match.StatusVariable, Status(GameModeState.Playing), Status(GameModeState.Ended, Team.Alpha));
            var status = _match.StatusVariable;

            _netHud.BindMatch(null);

            Assert.That(IsShown(_view.ScorePanel), Is.False);
            Assert.That(IsShown(_view.ResultLabel), Is.False);
            Assert.That(IsShown(_view.NoticeLabel), Is.False);
            Assert.That(_view.TimeLabel.text, Is.EqualTo(HudFormat.NoTime));
            Assert.That(status.OnValueChanged, Is.Null, "the old match is no longer listened to");
        }

        [Test]
        public void UnbindAllStopsListening()
        {
            _netHud.BindMatch(_match);

            _netHud.UnbindAll();

            Assert.That(_player.HealthVariable.OnValueChanged, Is.Null);
            Assert.That(_player.TeamVariable.OnValueChanged, Is.Null);
            Assert.That(_weapon.StateVariable.OnValueChanged, Is.Null);
            Assert.That(_match.AlphaScoreVariable.OnValueChanged, Is.Null);
            Assert.That(_match.RemainingSecondsVariable.OnValueChanged, Is.Null);
            Assert.That(_match.StatusVariable.OnValueChanged, Is.Null);
            Assert.That(_match.NextWaveSecondsVariable.OnValueChanged, Is.Null);
            Assert.That(_match.RulesVariable.OnValueChanged, Is.Null);
            Assert.That(_player.DownedStateVariable.OnValueChanged, Is.Null);
        }

        [Test]
        public void ShowsTheBleedoutAndTheReviveProgressWhileDowned()
        {
            _netHud.BindMatch(_match);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, _maxHealth);

            // ダウン中は体力が残るので、リスポーンの待ちにはならない
            Arrive(_player.HealthVariable, _maxHealth, 30);
            Arrive(_player.DownedStateVariable, NetDownedState.None, new NetDownedState(true, 12, 0));
            Assert.That(_netHud.IsAwaitingRespawn, Is.False);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo("ダウン中 あと 12 秒"));
            Assert.That(IsShown(_view.NoticeLabel), Is.True);

            Arrive(_player.DownedStateVariable, new NetDownedState(true, 12, 0), new NetDownedState(true, 11, 40));
            Assert.That(_view.NoticeLabel.text, Is.EqualTo("ダウン中 あと 11 秒 (蘇生 40%)"));

            // 起こされたら案内を消す
            Arrive(_player.DownedStateVariable, new NetDownedState(true, 11, 40), NetDownedState.None);
            Assert.That(IsShown(_view.NoticeLabel), Is.False);
        }

        [Test]
        public void BleedingOutSwitchesToTheRespawnCountdown()
        {
            _netHud.BindMatch(_match);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, 30);
            Arrive(_player.DownedStateVariable, NetDownedState.None, new NetDownedState(true, 1, 0));

            // サーバーは体力 0 とダウンの解除を同じフレームに書く
            Arrive(_player.HealthVariable, 30, 0);
            Arrive(_player.DownedStateVariable, new NetDownedState(true, 1, 0), NetDownedState.None);

            Assert.That(_netHud.IsAwaitingRespawn, Is.True);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo(HudFormat.Respawn(3)));
        }

        [Test]
        public void ShowsTheProgressWhileRevivingAnAlly()
        {
            _netHud.BindMatch(_match);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, _maxHealth);

            _netHud.ShowReviving(0);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo("蘇生中 0%"));

            _netHud.ShowReviving(55);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo("蘇生中 55%"));

            _netHud.ShowReviving(null);
            Assert.That(IsShown(_view.NoticeLabel), Is.False);
        }

        [Test]
        public void ShowsSpawnProtectionUntilItEnds()
        {
            _netHud.BindMatch(_match);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, _maxHealth);

            _netHud.ShowSpawnProtection(2);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo("保護中 あと 2 秒"));

            _netHud.ShowSpawnProtection(1);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo("保護中 あと 1 秒"));

            _netHud.ShowSpawnProtection(0);
            Assert.That(IsShown(_view.NoticeLabel), Is.False);
        }

        [Test]
        public void RevivingAndBeingDownedWinOverSpawnProtection()
        {
            _netHud.BindMatch(_match);
            StartPlaying();
            Arrive(_player.HealthVariable, 0, _maxHealth);
            _netHud.ShowSpawnProtection(2);

            _netHud.ShowReviving(55);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo("蘇生中 55%"));

            _netHud.ShowReviving(null);
            Assert.That(_view.NoticeLabel.text, Is.EqualTo("保護中 あと 2 秒"));

            Arrive(_player.DownedStateVariable, NetDownedState.None, new NetDownedState(true, 9, 0));
            Assert.That(_view.NoticeLabel.text, Is.EqualTo("ダウン中 あと 9 秒"));
        }

        [Test]
        public void OwnDownedNoticeWinsOverReviving()
        {
            _netHud.BindMatch(_match);
            StartPlaying();
            _netHud.ShowReviving(55);

            Arrive(_player.DownedStateVariable, NetDownedState.None, new NetDownedState(true, 9, 0));

            Assert.That(_view.NoticeLabel.text, Is.EqualTo("ダウン中 あと 9 秒"));
        }

        [Test]
        public void PlayerStatsBecomeConnectedScoreboardRows()
        {
            var row = new NetPlayerStats(3, Team.Bravo, 5, 2);

            Assert.That(NetHud.TryToScoreboardEntry(row, 3, out var local), Is.True);
            Assert.That(local.PlayerId, Is.EqualTo(3));
            Assert.That(local.Name, Is.EqualTo(ScoreboardRows.PlayerName(3)));
            Assert.That(local.Team, Is.EqualTo(Team.Bravo));
            Assert.That(local.Kills, Is.EqualTo(5));
            Assert.That(local.Deaths, Is.EqualTo(2));
            Assert.That(local.Connection, Is.EqualTo(ScoreboardConnection.Connected));
            Assert.That(local.IsLocal, Is.True);

            Assert.That(NetHud.TryToScoreboardEntry(row, 4, out var other), Is.True);
            Assert.That(other.IsLocal, Is.False);
            Assert.That(NetHud.TryToScoreboardEntry(row, null, out var unknown), Is.True);
            Assert.That(unknown.IsLocal, Is.False);
        }

        [Test]
        public void PlayerStatsWithAnIdTooLargeForAPlayerIdAreSkipped()
        {
            var row = new NetPlayerStats((ulong)int.MaxValue + 1, Team.Alpha, 0, 0);

            Assert.That(NetHud.TryToScoreboardEntry(row, null, out _), Is.False);
        }

        [Test]
        public void NegativeCountsFromTheWireAreShownAsZero()
        {
            var row = new NetPlayerStats(1, Team.Alpha, -1, -4);

            Assert.That(NetHud.TryToScoreboardEntry(row, null, out var entry), Is.True);
            Assert.That(entry.Kills, Is.Zero);
            Assert.That(entry.Deaths, Is.Zero);
        }

        [Test]
        public void WithoutASyncedPlayerListTheScoreboardIsEmpty()
        {
            // Edit Mode では試合の Awake が走らず Players が無い。そのときも HUD を作り続ける
            _netHud.BindMatch(_match);

            var sections = _netHud.Hud.Scoreboard.Sections;
            Assert.That(sections, Has.Count.EqualTo(2));
            Assert.That(sections[0].Rows, Is.Empty);
            Assert.That(sections[1].Rows, Is.Empty);
        }
    }
}
