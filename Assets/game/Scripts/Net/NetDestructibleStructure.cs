using System;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// 同じ GameObject の <see cref="DestructibleStructure"/> の壊れた部品をネットワークで同期する。
    /// サーバーだけがダメージと崩落を計算し (<see cref="DestructibleStructure.HasAuthority"/> = true)、部品が壊れるたびに
    /// <see cref="DestructibleStructure.DestroyedBitset"/> を NetworkList に書く。クライアントは値が届いたときと Spawn 時 (途中参加) に
    /// <see cref="DestructibleStructure.ApplyDestroyedBitset"/> で反映するだけで、崩落の計算はしない。
    /// </summary>
    /// <remarks>
    /// 弾で壊れるかどうか (<see cref="DestructibleStructure.DestructionEnabled"/>) は試合のルールで決まるので、
    /// ここではなく <see cref="NetTeamDeathmatch"/> が試合の開始時に切り替える。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DestructibleStructure))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetDestructibleStructure : NetworkBehaviour
    {
        /// <summary>NetworkList と、テストで置き換える List の両方に bitset を読み書きするための最小の操作。</summary>
        internal interface IBitsetWords
        {
            int Count { get; }

            ulong this[int index] { get; set; }

            void Clear();

            void Add(ulong word);
        }

        // NetworkList は Awake で作る (フィールド初期化子だとエディタで破棄されずに残ることがある)
        private NetworkList<ulong> _destroyed;
        private IBitsetWords _words;
        private DestructibleStructure _structure;

        // クライアントが最後に反映した壊れた部品の数。同じ数のログを繰り返さないため
        private int _lastLoggedCount = -1;

        /// <summary>同期している建物。</summary>
        public DestructibleStructure Structure => _structure != null ? _structure : _structure = GetComponent<DestructibleStructure>();

        private void Awake()
        {
            _destroyed = new NetworkList<ulong>();
            _words = new NetworkListWords(_destroyed);
            _structure = GetComponent<DestructibleStructure>();
        }

        public override void OnNetworkSpawn()
        {
            var structure = Structure;
            if (structure == null)
            {
                Debug.LogError("[Net] " + name + " has no DestructibleStructure; run Fps.Editor.NetSceneBuilder.Build", this);
                return;
            }

            structure.HasAuthority = IsServer;
            _lastLoggedCount = -1;

            if (IsServer)
            {
                structure.PiecesDestroyed += HandlePiecesDestroyed;

                // Spawn より前に壊れていた部品があれば、それも配る
                ServerSync();
                return;
            }

            _destroyed.OnListChanged += HandleListChanged;

            // 途中参加: Spawn の時点で届いている状態を反映する。長さが合わなければシーンが食い違っている
            if (!TryApply(structure, ReadBitset(_words), out var count, out var error))
            {
                Debug.LogError("[Net] cannot apply destroyed pieces of " + name + ": " + error
                    + "; the server and the client may be running different scenes", this);
                return;
            }

            LogIfChanged(count);
        }

        public override void OnNetworkDespawn()
        {
            if (_structure != null)
            {
                _structure.PiecesDestroyed -= HandlePiecesDestroyed;
            }

            _destroyed.OnListChanged -= HandleListChanged;
        }

        /// <summary>
        /// bitset を words に書く。要素数が違えば全部入れ直し、同じなら値の違う要素だけを書き換える (同期する量を減らすため)。
        /// 書き換えた要素があれば true。
        /// </summary>
        internal static bool WriteBitset(ulong[] bitset, IBitsetWords words)
        {
            if (bitset == null)
            {
                throw new ArgumentNullException(nameof(bitset));
            }

            if (words == null)
            {
                throw new ArgumentNullException(nameof(words));
            }

            if (words.Count != bitset.Length)
            {
                words.Clear();
                foreach (var word in bitset)
                {
                    words.Add(word);
                }

                return true;
            }

            var changed = false;
            for (var i = 0; i < bitset.Length; i++)
            {
                if (words[i] != bitset[i])
                {
                    words[i] = bitset[i];
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>words の今の値を bitset (<see cref="DestructibleStructure.DestroyedBitset"/> の形) として写す。</summary>
        internal static ulong[] ReadBitset(IBitsetWords words)
        {
            if (words == null)
            {
                throw new ArgumentNullException(nameof(words));
            }

            var bitset = new ulong[words.Count];
            for (var i = 0; i < bitset.Length; i++)
            {
                bitset[i] = words[i];
            }

            return bitset;
        }

        /// <summary>
        /// サーバーから届いた bitset を structure に反映し、壊れている部品の数を count に返す。
        /// 要素数が建物の部品数に合わない・部品数以降のビットが立っている・部品の設定が不正、のどれかなら
        /// 何も変えずに false と理由を返す (受信のコールバックの中で例外を投げないため)。
        /// </summary>
        internal static bool TryApply(DestructibleStructure structure, ulong[] bitset, out int count, out string error)
        {
            if (structure == null)
            {
                throw new ArgumentNullException(nameof(structure));
            }

            count = 0;
            if (bitset == null)
            {
                error = "no bitset";
                return false;
            }

            try
            {
                // 組み立てに失敗したときに例外を投げるのは Integrity なので、長さを比べる前に触れる
                var expected = structure.Integrity.BitsetLength;
                if (bitset.Length != expected)
                {
                    error = "bitset has " + bitset.Length + " words but the structure needs " + expected;
                    return false;
                }

                structure.ApplyDestroyedBitset(bitset);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                error = e.Message;
                return false;
            }

            count = CountDestroyed(bitset);
            error = null;
            return true;
        }

        /// <summary>bitset の立っているビットの数 (壊れている部品の数)。</summary>
        internal static int CountDestroyed(ulong[] bitset)
        {
            if (bitset == null)
            {
                throw new ArgumentNullException(nameof(bitset));
            }

            var count = 0;
            foreach (var word in bitset)
            {
                var w = word;
                while (w != 0)
                {
                    w &= w - 1;
                    count++;
                }
            }

            return count;
        }

        /// <summary>壊れた部品の数のログ。サーバーでもクライアントでも同じ書式で出し、突き合わせられるようにする。</summary>
        internal static string FormatLog(string structureName, int destroyedCount)
        {
            return "[Net] structure=" + structureName + " destroyedCount=" + destroyedCount;
        }

        private void HandlePiecesDestroyed(Fps.Core.StructureDamageResult result)
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            ServerSync();
        }

        private void ServerSync()
        {
            ulong[] bitset;
            try
            {
                bitset = _structure.DestroyedBitset;
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError("[Net] cannot sync destroyed pieces of " + name + ": " + e.Message, this);
                return;
            }

            if (WriteBitset(bitset, _words))
            {
                LogIfChanged(CountDestroyed(bitset));
            }
        }

        private void HandleListChanged(NetworkListEvent<ulong> change)
        {
            if (IsServer || _structure == null)
            {
                return;
            }

            int expected;
            try
            {
                expected = _structure.Integrity.BitsetLength;
            }
            catch (InvalidOperationException)
            {
                // 部品の設定の誤りは Spawn 時の TryApply でログに出している
                return;
            }

            var bitset = ReadBitset(_words);

            // 入れ直しの途中 (Clear の直後など) は長さが合わないので、全部届くまで待つ
            if (bitset.Length != expected)
            {
                return;
            }

            if (!TryApply(_structure, bitset, out var count, out var error))
            {
                Debug.LogError("[Net] cannot apply destroyed pieces of " + name + ": " + error, this);
                return;
            }

            LogIfChanged(count);
        }

        private void LogIfChanged(int count)
        {
            if (count == _lastLoggedCount)
            {
                return;
            }

            _lastLoggedCount = count;
            Debug.Log(FormatLog(name, count), this);
        }

        private sealed class NetworkListWords : IBitsetWords
        {
            private readonly NetworkList<ulong> _list;

            public NetworkListWords(NetworkList<ulong> list)
            {
                _list = list;
            }

            public int Count => _list.Count;

            public ulong this[int index]
            {
                get => _list[index];
                set => _list[index] = value;
            }

            public void Clear()
            {
                _list.Clear();
            }

            public void Add(ulong word)
            {
                _list.Add(word);
            }
        }
    }
}
