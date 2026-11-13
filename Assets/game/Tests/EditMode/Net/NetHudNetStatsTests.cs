using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// <see cref="NetHud"/> の回線の表示 (F3) が、保存した設定に従って出し入れされ、切り替えを保存すること。
    /// ネットワークは起動しないので、Ping・ロス・tick はどれも分からない (--) まま出る。
    /// </summary>
    public sealed class NetHudNetStatsTests
    {
        private const string _storeKey = "Fps.Tests.NetHudNetStats";
        private const string _unknownText = "Ping -- ms  ロス --%  tick --";

        private GameObject _playerObject;
        private NetPlayer _player;
        private NetHud _netHud;
        private ControlSettings _settingsBefore;
        private ControlSettingsStore _store;

        [SetUp]
        public void SetUp()
        {
            _settingsBefore = AppliedControlSettings.Current;
            _store = new ControlSettingsStore(_storeKey);
            _store.Delete();

            _playerObject = new GameObject("TestNetPlayer");
            _playerObject.AddComponent<NetworkObject>();
            _player = _playerObject.AddComponent<NetPlayer>();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyHud();
            if (_playerObject != null)
            {
                Object.DestroyImmediate(_playerObject);
            }

            _store.Delete();
            AppliedControlSettings.Apply(_settingsBefore);
        }

        private NetHud CreateHud(bool showNetStats)
        {
            AppliedControlSettings.Apply(_settingsBefore.WithShowNetStats(showNetStats));
            _netHud = NetHud.Create(_player);
            _netHud.NetStatsSettingsStore = _store;
            return _netHud;
        }

        private void DestroyHud()
        {
            if (_netHud == null)
            {
                return;
            }

            // Edit Mode では OnDestroy が走らないので、設定の購読を他のテストへ持ち越さない
            _netHud.UnbindAll();
            var settings = _netHud.Hud.OwnedPanelSettings;
            Object.DestroyImmediate(_netHud.gameObject);
            if (settings != null)
            {
                Object.DestroyImmediate(settings);
            }

            _netHud = null;
        }

        [Test]
        public void IsHiddenWhenTheSettingsSaySo()
        {
            var hud = CreateHud(false);

            Assert.That(hud.NetStats, Is.Not.Null);
            Assert.That(hud.NetStats, Is.SameAs(hud.GetComponent<NetStatsView>()));
            Assert.That(hud.IsNetStatsShown, Is.False);
            Assert.That(hud.NetStats.IsShown, Is.False);
            Assert.That(hud.NetStats.Text, Is.Empty);
        }

        [Test]
        public void StartsShownWhenTheSettingsSaySo()
        {
            var hud = CreateHud(true);

            Assert.That(hud.IsNetStatsShown, Is.True);
            Assert.That(hud.NetStats.IsShown, Is.True);
            Assert.That(hud.NetStats.Text, Is.EqualTo(_unknownText));
            Assert.That(hud.NetStats.Level, Is.EqualTo(NetPingLevel.Good));
        }

        [Test]
        public void ToggleShowsHidesAndSaves()
        {
            var hud = CreateHud(false);

            hud.ToggleNetStats();

            Assert.That(hud.IsNetStatsShown, Is.True);
            Assert.That(hud.NetStats.Text, Is.EqualTo(_unknownText));
            Assert.That(AppliedControlSettings.Current.ShowNetStats, Is.True);
            Assert.That(new ControlSettingsStore(_storeKey).Load().ShowNetStats, Is.True, "kept after a restart");

            hud.ToggleNetStats();

            Assert.That(hud.IsNetStatsShown, Is.False);
            Assert.That(hud.NetStats.IsShown, Is.False);
            Assert.That(AppliedControlSettings.Current.ShowNetStats, Is.False);
            Assert.That(new ControlSettingsStore(_storeKey).Load().ShowNetStats, Is.False);
        }

        [Test]
        public void ToggleKeepsTheOtherSettings()
        {
            var hud = CreateHud(false);
            var before = AppliedControlSettings.Current;

            hud.ToggleNetStats();

            Assert.That(new ControlSettingsStore(_storeKey).Load(), Is.EqualTo(before.WithShowNetStats(true)));
        }

        [Test]
        public void FollowsSettingsAppliedElsewhere()
        {
            var hud = CreateHud(false);

            AppliedControlSettings.Apply(AppliedControlSettings.Current.WithShowNetStats(true));
            Assert.That(hud.NetStats.IsShown, Is.True);

            AppliedControlSettings.Apply(AppliedControlSettings.Current.WithShowNetStats(false));
            Assert.That(hud.NetStats.IsShown, Is.False);
        }

        [Test]
        public void StopsFollowingTheSettingsAfterRelease()
        {
            var hud = CreateHud(false);
            var view = hud.NetStats;

            hud.UnbindAll();
            AppliedControlSettings.Apply(AppliedControlSettings.Current.WithShowNetStats(true));

            Assert.That(view.IsShown, Is.False);
        }

        [Test]
        public void RefreshesOncePerSecondWhileShown()
        {
            var hud = CreateHud(true);

            hud.Tick(NetHud.NetStatsRefreshSeconds * 0.5f);
            Assert.That(hud.NetStats.Text, Is.EqualTo(_unknownText));

            hud.Tick(NetHud.NetStatsRefreshSeconds);
            Assert.That(hud.NetStats.IsShown, Is.True);
            Assert.That(hud.NetStats.Text, Is.EqualTo(_unknownText), "nothing is known without a network");
        }

        [Test]
        public void PingIsUnknownBeforeThePlayerSpawns()
        {
            var hud = CreateHud(true);

            Assert.That(hud.ReadNetStatsPing(), Is.Null);
        }

        [Test]
        public void TheServerAnswersAtMostTheProbeBudget()
        {
            var answered = 0;
            for (var i = 0; i < 100; i++)
            {
                if (_player.TakeNetStatsProbeBudget(10.0))
                {
                    answered++;
                }
            }

            Assert.That(answered, Is.EqualTo((int)NetPlayer.MaxNetStatsProbesPerSecond));
            Assert.That(_player.TakeNetStatsProbeBudget(10.0), Is.False);
            Assert.That(_player.TakeNetStatsProbeBudget(10.1), Is.True, "the budget refills over time");
        }

        [Test]
        public void TheViewColorsByPingLevel()
        {
            var hud = CreateHud(false);
            var view = hud.NetStats;

            view.Show(NetStatsFormat.Text(320, 0, 1), NetPingLevel.Bad);
            Assert.That(view.Level, Is.EqualTo(NetPingLevel.Bad));
            Assert.That(view.Text, Is.EqualTo("Ping 320 ms  ロス 0%  tick 1"));

            view.Hide();
            Assert.That(view.Level, Is.EqualTo(NetPingLevel.Good));
            Assert.That(view.Text, Is.Empty);
        }
    }
}
