using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Protocol;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.NetV2
{
    /// <summary>
    /// サーバーから届いた壊れる建物の状態 (<see cref="StructureStateMessage"/>) を、マップのシーンの見た目 (<see cref="DestructibleStructure"/>) と
    /// 予測の当たり (<see cref="ClientWorld"/>) に写す。
    /// </summary>
    /// <remarks>
    /// サーバーは参加した直後に全部の建物の状態を送るので、マップのシーンを読み終える前に届く。届いた状態を建物ごとに残し、
    /// シーンに入ったときに (<see cref="Enter"/>) まとめて写す。
    /// 建物の番号は書き出し (MapCollisionExporter) と同じく、有効・無効を問わずシーンの並び順で数える。
    /// </remarks>
    internal sealed class NetV2Structures
    {
        private readonly Dictionary<int, ulong[]> _states = new Dictionary<int, ulong[]>();
        private readonly List<DestructibleStructure> _scene = new List<DestructibleStructure>();
        private readonly UnityEngine.Object _context;
        private ClientWorld _world;

        public NetV2Structures(UnityEngine.Object context)
        {
            _context = context;
        }

        /// <summary>scene の建物と、予測の世界 world に今までに届いた状態を写す。</summary>
        public void Enter(Scene scene, ClientWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _scene.Clear();
            foreach (var root in scene.GetRootGameObjects())
            {
                _scene.AddRange(root.GetComponentsInChildren<DestructibleStructure>(true));
            }

            Debug.Log(NetV2Client.LogTag + " structures in scene " + scene.name + ": " + _scene.Count.ToString(CultureInfo.InvariantCulture)
                + " (collision " + world.StructureCount.ToString(CultureInfo.InvariantCulture) + ")", _context);
            var message = new StructureStateMessage();
            foreach (var pair in _states)
            {
                message.StructureId = pair.Key;
                message.SetWords(pair.Value);
                Show(message);
            }
        }

        public void Leave()
        {
            _world = null;
            _scene.Clear();
        }

        public void Receive(StructureStateMessage message)
        {
            var words = new ulong[message.WordCount];
            message.CopyWordsTo(words);
            _states[message.StructureId] = words;
            if (_world != null)
            {
                Show(message);
            }
        }

        private void Show(StructureStateMessage message)
        {
            if (!_world.Apply(message, out var changed))
            {
                Debug.LogWarning(NetV2Client.LogTag + " the map collision has no structure " + message.StructureId.ToString(CultureInfo.InvariantCulture)
                    + "; export the map again with make export-server-data", _context);
            }

            var id = message.StructureId;
            if (id >= _scene.Count || _scene[id] == null)
            {
                Debug.LogWarning(NetV2Client.LogTag + " the scene has no structure " + id.ToString(CultureInfo.InvariantCulture), _context);
                return;
            }

            if (!TryApply(_scene[id], message, out var error))
            {
                Debug.LogError(NetV2Client.LogTag + " cannot show structure " + id.ToString(CultureInfo.InvariantCulture) + ": " + error, _context);
                return;
            }

            Debug.Log(NetV2Client.LogTag + " structure " + id.ToString(CultureInfo.InvariantCulture) + " collision pieces changed="
                + changed.ToString(CultureInfo.InvariantCulture), _context);
        }

        // 受信の中で例外を投げないよう、長さの合わない状態と部品の設定の誤りは理由にして返す
        private static bool TryApply(DestructibleStructure structure, StructureStateMessage message, out string error)
        {
            try
            {
                var expected = structure.Integrity.BitsetLength;
                var bitset = new ulong[expected];
                for (var i = 0; i < message.WordCount; i++)
                {
                    if (i < expected)
                    {
                        bitset[i] = message[i];
                    }
                    else if (message[i] != 0UL)
                    {
                        error = "the server has " + message.WordCount + " words but the structure has " + expected;
                        return false;
                    }
                }

                structure.ApplyDestroyedBitset(bitset);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                error = e.Message;
                return false;
            }

            error = null;
            return true;
        }
    }
}
