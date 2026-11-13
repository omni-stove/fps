using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Net.Tests
{
    /// <summary>
    /// 壊せる建物のネット同期 (<see cref="NetDestructibleStructure"/>) の bitset の書き込みと反映、
    /// <see cref="NetTeamDeathmatch"/> が試合の開始時にルールで建物の破壊を切り替える処理を、ネットワークを起動せずに確かめる。
    /// NetworkList の代わりに <see cref="ListWords"/> を渡す。
    /// 建物は 1m の Cube を 2x2 に並べる。添字は下段が 0 (左)・1 (右)、上段が 2 (左)・3 (右) で、下段だけ地面に接する。
    /// </summary>
    public sealed class NetDestructibleStructureTests
    {
        // 他のテストの物と重ならないよう、原点から離して置く
        private static readonly Vector3 _origin = new Vector3(-300f, 0f, 300f);

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

        [Test]
        public void WriteFillsEmptyWords()
        {
            var words = new ListWords();

            Assert.That(NetDestructibleStructure.WriteBitset(new ulong[] { 5UL, 1UL }, words), Is.True);
            Assert.That(words.Items, Is.EqualTo(new[] { 5UL, 1UL }));
        }

        [Test]
        public void WriteOnlyTouchesChangedWords()
        {
            var words = new ListWords(1UL, 2UL, 3UL);

            Assert.That(NetDestructibleStructure.WriteBitset(new ulong[] { 1UL, 6UL, 3UL }, words), Is.True);
            Assert.That(words.Items, Is.EqualTo(new[] { 1UL, 6UL, 3UL }));
            Assert.That(words.SetIndices, Is.EqualTo(new[] { 1 }));
            Assert.That(words.ClearCount, Is.Zero);
        }

        [Test]
        public void WriteOfTheSameBitsetChangesNothing()
        {
            var words = new ListWords(1UL, 2UL);

            Assert.That(NetDestructibleStructure.WriteBitset(new ulong[] { 1UL, 2UL }, words), Is.False);
            Assert.That(words.SetIndices, Is.Empty);
            Assert.That(words.ClearCount, Is.Zero);
        }

        [Test]
        public void WriteReplacesWordsOfAnotherLength()
        {
            var words = new ListWords(9UL, 9UL, 9UL);

            Assert.That(NetDestructibleStructure.WriteBitset(new ulong[] { 4UL }, words), Is.True);
            Assert.That(words.Items, Is.EqualTo(new[] { 4UL }));
            Assert.That(words.ClearCount, Is.EqualTo(1));
        }

        [Test]
        public void ReadCopiesTheWords()
        {
            var words = new ListWords(7UL, 0UL);

            var bitset = NetDestructibleStructure.ReadBitset(words);
            words[0] = 1UL;

            Assert.That(bitset, Is.EqualTo(new[] { 7UL, 0UL }));
        }

        [Test]
        public void CountDestroyedCountsSetBits()
        {
            Assert.That(NetDestructibleStructure.CountDestroyed(new ulong[0]), Is.Zero);
            Assert.That(NetDestructibleStructure.CountDestroyed(new ulong[] { 0b1011UL, 1UL << 63 }), Is.EqualTo(4));
            Assert.That(NetDestructibleStructure.CountDestroyed(new[] { ulong.MaxValue }), Is.EqualTo(64));
        }

        [Test]
        public void LogShowsTheNameAndTheCount()
        {
            Assert.That(NetDestructibleStructure.FormatLog("House", 3), Is.EqualTo("[Net] structure=House destroyedCount=3"));
        }

        [Test]
        public void ServerDestructionReachesTheClientWithoutCollapseOnTheClient()
        {
            var server = CreateGrid("Server", Vector3.zero);
            server.HasAuthority = true;
            server.DestructionEnabled = true;
            var client = CreateGrid("Client", new Vector3(10f, 0f, 0f));
            client.HasAuthority = false;

            // 下段を両方壊すと上段が崩落する (計算するのはサーバーだけ)
            var words = new ListWords();
            server.PiecesDestroyed += _ => NetDestructibleStructure.WriteBitset(server.DestroyedBitset, words);
            Hit(server, 0);
            Hit(server, 1);
            Assert.That(NetDestructibleStructure.CountDestroyed(words.Items.ToArray()), Is.EqualTo(4));

            Assert.That(
                NetDestructibleStructure.TryApply(client, NetDestructibleStructure.ReadBitset(words), out var count, out var error),
                Is.True, error);

            Assert.That(count, Is.EqualTo(4));
            Assert.That(error, Is.Null);
            for (var i = 0; i < 4; i++)
            {
                Assert.That(client.Integrity.IsDestroyed(i), Is.True, $"piece {i}");
                Assert.That(client.Pieces[i].IsBroken, Is.True, $"piece {i}");
            }
        }

        [Test]
        public void LateJoinerAppliesTheCurrentState()
        {
            var client = CreateGrid("LateJoiner", Vector3.zero);
            client.HasAuthority = false;

            // 途中参加で Spawn 時に届く状態は、部品 1 だけが壊れている
            var words = new ListWords(1UL << 1);

            Assert.That(NetDestructibleStructure.TryApply(client, NetDestructibleStructure.ReadBitset(words), out var count, out _), Is.True);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(client.Pieces[0].IsBroken, Is.False);
            Assert.That(client.Pieces[1].IsBroken, Is.True);
            Assert.That(client.Pieces[2].IsBroken, Is.False, "the client does not compute collapse");
        }

        [Test]
        public void ClearedBitsRestoreThePieces()
        {
            var client = CreateGrid("Restored", Vector3.zero);
            client.HasAuthority = false;
            Assert.That(NetDestructibleStructure.TryApply(client, new[] { 0b0011UL }, out _, out _), Is.True);

            Assert.That(NetDestructibleStructure.TryApply(client, new[] { 0UL }, out var count, out _), Is.True);

            Assert.That(count, Is.Zero);
            Assert.That(client.Pieces[0].IsBroken, Is.False);
            Assert.That(client.Pieces[1].IsBroken, Is.False);
        }

        [Test]
        public void BitsetOfAnotherLengthIsRejectedWithoutChanges()
        {
            var client = CreateGrid("WrongLength", Vector3.zero);
            client.HasAuthority = false;

            Assert.That(NetDestructibleStructure.TryApply(client, new[] { 1UL, 0UL }, out var count, out var error), Is.False);

            Assert.That(count, Is.Zero);
            Assert.That(error, Does.Contain("2 words"));
            Assert.That(client.Pieces[0].IsBroken, Is.False);
        }

        [Test]
        public void BitsBeyondThePiecesAreRejectedWithoutChanges()
        {
            var client = CreateGrid("ExtraBits", Vector3.zero);
            client.HasAuthority = false;

            Assert.That(NetDestructibleStructure.TryApply(client, new[] { 0b1_0001UL }, out _, out var error), Is.False);

            Assert.That(error, Does.Contain("beyond"));
            Assert.That(client.Pieces[0].IsBroken, Is.False);
        }

        [Test]
        public void MissingBitsetIsRejected()
        {
            var client = CreateGrid("Missing", Vector3.zero);

            Assert.That(NetDestructibleStructure.TryApply(client, null, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void UnassignedRulesLeaveDestructionOff()
        {
            var a = CreateGrid("A", Vector3.zero);
            var b = CreateGrid("B", new Vector3(10f, 0f, 0f));
            a.DestructionEnabled = true;

            Assert.That(NetTeamDeathmatch.TryLoadRules(null, out var rules, out var usedDefault, out _), Is.True);
            Assert.That(usedDefault, Is.True);
            var spec = new TeamDeathmatchSpec(10, 300f, 3f, rules);

            Assert.That(spec.Rules.Destruction, Is.EqualTo(Destruction.Off));
            Assert.That(NetTeamDeathmatch.ApplyDestructionRule(new[] { a, b }, spec.Rules), Is.EqualTo(2));
            Assert.That(a.DestructionEnabled, Is.False);
            Assert.That(b.DestructionEnabled, Is.False);
        }

        [Test]
        public void RulesAssetTurnsDestructionOn()
        {
            var a = CreateGrid("A", Vector3.zero);
            var b = CreateGrid("B", new Vector3(10f, 0f, 0f));
            var asset = ScriptableObject.CreateInstance<GameRulesDefinition>();
            _created.Add(asset);
            asset.name = "DestructionRules";
            asset.Destruction = Destruction.On;

            Assert.That(NetTeamDeathmatch.TryLoadRules(asset, out var rules, out var usedDefault, out var error), Is.True, error);
            Assert.That(usedDefault, Is.False);
            var spec = new TeamDeathmatchSpec(10, 300f, 3f, rules);

            Assert.That(spec.Rules.Destruction, Is.EqualTo(Destruction.On));
            Assert.That(NetTeamDeathmatch.ApplyDestructionRule(new[] { a, b }, spec.Rules), Is.EqualTo(2));
            Assert.That(a.DestructionEnabled, Is.True);
            Assert.That(b.DestructionEnabled, Is.True);
        }

        [Test]
        public void DestroyedStructuresAreSkipped()
        {
            var alive = CreateGrid("Alive", Vector3.zero);
            var gone = CreateGrid("Gone", new Vector3(10f, 0f, 0f));
            Object.DestroyImmediate(gone.gameObject);

            Assert.That(
                NetTeamDeathmatch.ApplyDestructionRule(new[] { alive, gone, null }, OnRules()),
                Is.EqualTo(1));
            Assert.That(alive.DestructionEnabled, Is.True);
        }

        private static GameRules OnRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.Killer,
                false,
                1f,
                Destruction.On);
        }

        private DestructibleStructure CreateGrid(string name, Vector3 offset)
        {
            var root = new GameObject(name);
            _created.Add(root);
            root.transform.position = _origin + offset;
            var structure = root.AddComponent<DestructibleStructure>();

            // 破片はこのテストの対象でなく、出すとシーンに DebrisPool が作られて残るため
            structure.SpawnDebris = false;
            DestructiblePiece.Create(root.transform, "BottomLeft", new Vector3(-0.5f, 0.5f, 0f), Vector3.one, 0, grounded: true);
            DestructiblePiece.Create(root.transform, "BottomRight", new Vector3(0.5f, 0.5f, 0f), Vector3.one, 1, grounded: true);
            DestructiblePiece.Create(root.transform, "TopLeft", new Vector3(-0.5f, 1.5f, 0f), Vector3.one, 2);
            DestructiblePiece.Create(root.transform, "TopRight", new Vector3(0.5f, 1.5f, 0f), Vector3.one, 3);
            structure.Build();
            return structure;
        }

        private static void Hit(DestructibleStructure structure, int piece)
        {
            var target = structure.Pieces[piece];
            structure.ApplyStructureHit(target.Collider, target.transform.position, DestructiblePiece.DefaultMaxHealth);
        }

        /// <summary>NetworkList の代わり。書き換えた添字と Clear の回数を記録する。</summary>
        private sealed class ListWords : NetDestructibleStructure.IBitsetWords
        {
            public ListWords(params ulong[] words)
            {
                Items.AddRange(words);
            }

            public List<ulong> Items { get; } = new List<ulong>();

            public List<int> SetIndices { get; } = new List<int>();

            public int ClearCount { get; private set; }

            public int Count => Items.Count;

            public ulong this[int index]
            {
                get => Items[index];
                set
                {
                    Items[index] = value;
                    SetIndices.Add(index);
                }
            }

            public void Clear()
            {
                Items.Clear();
                ClearCount++;
            }

            public void Add(ulong word)
            {
                Items.Add(word);
            }
        }
    }
}
