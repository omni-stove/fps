using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class ReplayLibraryTests
    {
        private static readonly DateTime _older = new DateTime(2026, 11, 17, 20, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime _newer = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);

        private readonly List<string> _played = new List<string>();
        private string _directory;
        private bool _playResult;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _played.Clear();
            _playResult = true;

            // 本物の保存先 (persistentDataPath) を触らないよう、テストごとに空のフォルダを使う
            _directory = Path.Combine(Path.GetTempPath(), "ShooterReplayLibraryTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void ListsTheReplaysNewestFirstWithTheirMapAndLength()
        {
            Write("old", "Arena", _older, 30, 600);
            Write("new", "Harbor", _newer, 60, 120);
            File.WriteAllText(Path.Combine(_directory, "notes.txt"), "not a replay");

            var library = NewLibrary();
            var changed = 0;
            library.Changed += () => changed++;
            library.Refresh();

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(library.Entries, Is.EqualTo(new[]
            {
                new ReplayEntry("new", "Harbor", _newer, 2f),
                new ReplayEntry("old", "Arena", _older, 20f),
            }));
        }

        [Test]
        public void ABrokenFileIsSkippedAndTheRestAreListed()
        {
            Write("good", "Arena", _older, 30, 30);
            File.WriteAllBytes(Path.Combine(_directory, "broken" + ReplayFile.Extension), new byte[] { 1, 2, 3, 4 });

            var library = NewLibrary();
            library.Refresh();

            Assert.That(library.Entries.Select(entry => entry.Id), Is.EqualTo(new[] { "good" }));
        }

        [Test]
        public void PlayHandsTheFileOfAListedIdToThePlayer()
        {
            var path = Write("match", "Arena", _older, 30, 30);
            var library = NewLibrary();
            library.Refresh();

            Assert.That(library.Play("match"), Is.True);
            Assert.That(_played, Is.EqualTo(new[] { path }));

            Assert.That(library.Play("../match"), Is.False, "ids outside the list are not opened");
            Assert.That(library.Play(null), Is.False);

            _playResult = false;
            Assert.That(library.Play("match"), Is.False, "a refused file is reported as not played");
        }

        [Test]
        public void APlayerThatCannotReadTheFileIsReportedAsNotPlayed()
        {
            Write("match", "Arena", _older, 30, 30);
            var library = new ReplayLibrary(_directory, path => throw new InvalidDataException("protocol 20"));
            library.Refresh();

            Assert.That(library.Play("match"), Is.False);
        }

        [Test]
        public void DeleteRemovesTheFileAndRefreshesTheList()
        {
            var path = Write("match", "Arena", _older, 30, 30);
            Write("keep", "Arena", _newer, 30, 30);
            var library = NewLibrary();
            library.Refresh();
            var changed = 0;
            library.Changed += () => changed++;

            Assert.That(library.Delete("match"), Is.True);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(library.Entries.Select(entry => entry.Id), Is.EqualTo(new[] { "keep" }));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(library.Delete("match"), Is.False, "it is gone from the list");
        }

        [Test]
        public void ANewRecordingMakesRoomByDeletingTheOldestAndNeverOverwrites()
        {
            Write("a", "Arena", _older, 30, 30);
            Write("b", "Arena", _older.AddHours(1), 30, 30);
            Write("c", "Arena", _newer, 30, 30);
            var library = new ReplayLibrary(_directory, Play, 3);

            var path = library.NewRecordingPath(_newer);

            Assert.That(Path.GetFileName(path), Is.EqualTo("20261118-080000" + ReplayFile.Extension));
            Assert.That(library.Entries.Select(entry => entry.Id), Is.EqualTo(new[] { "c", "b" }), "the oldest goes so the new one fits");

            File.WriteAllBytes(path, Array.Empty<byte>());
            Assert.That(Path.GetFileName(library.NewRecordingPath(_newer)), Is.EqualTo("20261118-080000-2" + ReplayFile.Extension));
            Assert.Throws<ArgumentException>(() => library.NewRecordingPath(DateTime.Now));
        }

        [Test]
        public void PublishingPutsTheLibraryInGameSignalsUntilItIsDisposed()
        {
            var library = NewLibrary();
            library.Publish();
            Assert.That(GameSignals.Replays.Value, Is.SameAs(library));

            library.Dispose();
            Assert.That(GameSignals.Replays.Value, Is.Null);
        }

        [Test]
        public void AMissingFolderIsAnEmptyList()
        {
            var library = new ReplayLibrary(Path.Combine(_directory, "nothing-here"), Play);
            library.Refresh();
            Assert.That(library.Entries, Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayLibrary(_directory, Play, 0));
            Assert.Throws<ArgumentNullException>(() => new ReplayLibrary(_directory, null));
        }

        private ReplayLibrary NewLibrary()
        {
            return new ReplayLibrary(_directory, Play);
        }

        private bool Play(string path)
        {
            _played.Add(path);
            return _playResult;
        }

        private string Write(string id, string map, DateTime recordedAt, int tickRate, int durationTicks)
        {
            var path = Path.Combine(_directory, id + ReplayFile.Extension);
            using (var stream = File.Create(path))
            using (var writer = new ReplayWriter(stream, new ReplayHeader(21, "1.0.0", map, recordedAt, tickRate, 1)))
            {
                writer.Append(0, ReplayRecordKind.Connected, 1, 0, default);
                writer.Finish(durationTicks);
            }

            return path;
        }
    }
}
