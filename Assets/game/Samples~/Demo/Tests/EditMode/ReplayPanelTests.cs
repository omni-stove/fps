using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class ReplayPanelTests
    {
        // 表示名の無いマップ (map.<Id> の文言が無い) は Id のまま出る
        private const string _map = "ReplayTestMap";

        private static readonly DateTime _recordedAt = new DateTime(2026, 11, 18, 3, 4, 0, DateTimeKind.Utc);

        private VisualElement _container;
        private FakeLibrary _library;
        private ReplayPanel _panel;
        private int _cancelled;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _container = new VisualElement();
            _library = new FakeLibrary();
            _cancelled = 0;
            _panel = null;
        }

        [TearDown]
        public void TearDown()
        {
            _panel?.Dispose();
            GameSignals.ResetAll();
        }

        [Test]
        public void ListsEachReplayWithItsMapLocalDateAndLength()
        {
            _library.Entries.Add(new ReplayEntry("a", _map, _recordedAt, 125.4f));
            _library.Entries.Add(new ReplayEntry("b", _map, _recordedAt.AddDays(-1), 59.6f));
            Open();

            var local = _recordedAt.ToLocalTime();
            var date = local.ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            var earlier = _recordedAt.AddDays(-1).ToLocalTime().ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(_panel.TitleText, Is.EqualTo("リプレイ"));
            Assert.That(_panel.EntryLines, Is.EqualTo(new[]
            {
                _map + "   " + date + "   2:05",
                _map + "   " + earlier + "   1:00",
            }));
            Assert.That(_panel.StatusText, Is.Empty);
            Assert.That(_panel.PlayButtonOf("a").text, Is.EqualTo("再生"));
            Assert.That(_panel.DeleteButtonOf("a").text, Is.EqualTo("削除"));
            Assert.That(_panel.BackButton.text, Is.EqualTo("戻る"));
            Assert.That(_container.ClassListContains(ReplayPanel.PanelClass), Is.True);
        }

        [Test]
        public void WithoutReplaysItSaysHowToGetOne()
        {
            Open();

            Assert.That(_panel.EntryLines, Is.Empty);
            Assert.That(_panel.StatusText, Is.EqualTo("リプレイはまだありません。オンラインの試合を遊ぶと記録されます。"));
        }

        [Test]
        public void WithoutALibraryItSaysReplaysAreUnavailableUntilOneIsPublished()
        {
            _panel = new ReplayPanel(_container);
            Assert.That(_panel.Library, Is.Null);
            Assert.That(_panel.StatusText, Is.EqualTo("このビルドではリプレイを使えません。"));

            _library.Entries.Add(new ReplayEntry("a", _map, _recordedAt, 10f));
            GameSignals.Replays.Publish(_library);

            Assert.That(_panel.Library, Is.SameAs(_library));
            Assert.That(_panel.EntryLines.Count, Is.EqualTo(1));
            Assert.That(_panel.StatusText, Is.Empty);
        }

        [Test]
        public void PlayAsksTheLibraryAndSaysSoWhenItCannot()
        {
            _library.Entries.Add(new ReplayEntry("a", _map, _recordedAt, 10f));
            Open();

            Assert.That(_panel.Play("a"), Is.True);
            Assert.That(_library.Played, Is.EqualTo(new[] { "a" }));

            _library.PlayResult = false;
            Assert.That(_panel.Play("a"), Is.False);
            Assert.That(_panel.StatusText, Is.EqualTo("このリプレイは再生できません。別の版のゲームで記録したものかもしれません。"));
        }

        [Test]
        public void DeleteNeedsASecondPressOnTheSameRow()
        {
            _library.Entries.Add(new ReplayEntry("a", _map, _recordedAt, 10f));
            _library.Entries.Add(new ReplayEntry("b", _map, _recordedAt, 10f));
            Open();

            Assert.That(_panel.Delete("a"), Is.False);
            Assert.That(_library.Deleted, Is.Empty);
            Assert.That(_panel.DeleteButtonOf("a").text, Is.EqualTo("本当に削除"));
            Assert.That(_panel.DeleteButtonOf("b").text, Is.EqualTo("削除"));

            Assert.That(_panel.Delete("b"), Is.False, "pressing another row moves the question there");
            Assert.That(_panel.DeleteButtonOf("a").text, Is.EqualTo("削除"));

            Assert.That(_panel.Delete("b"), Is.True);
            Assert.That(_library.Deleted, Is.EqualTo(new[] { "b" }));
            Assert.That(_panel.EntryLines.Count, Is.EqualTo(1), "the list is redrawn when the library changes");
            Assert.That(_panel.DeleteButtonOf("b"), Is.Null);
        }

        [Test]
        public void AFailedDeleteIsShown()
        {
            _library.Entries.Add(new ReplayEntry("a", _map, _recordedAt, 10f));
            _library.DeleteResult = false;
            Open();

            _panel.Delete("a");
            Assert.That(_panel.Delete("a"), Is.False);
            Assert.That(_panel.StatusText, Is.EqualTo("このリプレイを削除できませんでした。"));
        }

        [Test]
        public void CancelClosesThePanelAndDisposeLetsGo()
        {
            Open();
            Assert.That(_library.ChangedSubscribers, Is.EqualTo(1));

            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(1));

            _panel.Dispose();
            Assert.That(_library.ChangedSubscribers, Is.EqualTo(0));
            Assert.That(_container.childCount, Is.EqualTo(0));
            Assert.That(_container.ClassListContains(ReplayPanel.PanelClass), Is.False);
            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(1), "a closed panel does not ask to close again");

            GameSignals.Replays.Publish(new FakeLibrary());
            Assert.That(_panel.Library, Is.Null, "a closed panel no longer follows the published list");
            _panel = null;
        }

        private void Open()
        {
            _panel = new ReplayPanel(_container, _library);
            _panel.Cancelled += () => _cancelled++;
        }

        private sealed class FakeLibrary : IReplayLibrary
        {
            private Action _changed;

            public event Action Changed
            {
                add
                {
                    _changed += value;
                    ChangedSubscribers++;
                }

                remove
                {
                    _changed -= value;
                    ChangedSubscribers--;
                }
            }

            public int ChangedSubscribers { get; private set; }

            public List<ReplayEntry> Entries { get; } = new List<ReplayEntry>();

            IReadOnlyList<ReplayEntry> IReplayLibrary.Entries => Entries;

            public List<string> Played { get; } = new List<string>();

            public List<string> Deleted { get; } = new List<string>();

            public bool PlayResult { get; set; } = true;

            public bool DeleteResult { get; set; } = true;

            public bool Play(string replayId)
            {
                Played.Add(replayId);
                return PlayResult;
            }

            public bool Delete(string replayId)
            {
                if (!DeleteResult)
                {
                    return false;
                }

                Deleted.Add(replayId);
                Entries.RemoveAll(entry => entry.Id == replayId);
                _changed?.Invoke();
                return true;
            }
        }
    }
}
