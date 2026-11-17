using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.NetV2
{
    // サーバーは参加した直後に全部の建物の状態を送るので、マップのシーンを読み終える前に届く。届いた状態を建物ごとに残し、
    // シーンに入ったときに (Enter) まとめて写す。
    // 建物の番号は書き出し (MapCollisionExporter) と同じく、有効・無効を問わずシーンの並び順で数える。
    public sealed class NetV2Structures
    {
        private readonly Dictionary<int, ulong[]> _states = new Dictionary<int, ulong[]>();
        private readonly List<DestructibleStructure> _scene = new List<DestructibleStructure>();
        private readonly UnityEngine.Object _context;
        private ClientWorld _world;

        public NetV2Structures(UnityEngine.Object context)
        {
            _context = context;
        }

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

        public int StateCount => _states.Count;

        // 建物の番号はマップごとに 0 から振るので、前のマップの状態を残すと新しいマップの別の建物に当たってしまう。
        public void ClearStates()
        {
            _states.Clear();
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
