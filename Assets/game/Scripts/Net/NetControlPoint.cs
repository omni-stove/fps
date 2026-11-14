using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net
{
    /// <summary>
    /// シーンに置いた拠点 1 つの占領の状態 (<see cref="NetControlPointState"/>: 占領しているチーム・進み・押し合い) をネットワークで同期する。
    /// 番号と範囲は同じ GameObject の <see cref="ControlPointZone"/> が持ち、足元の円盤もそれが出す。
    /// </summary>
    /// <remarks>
    /// 占領の判定はサーバーの <see cref="NetTeamDeathmatch"/> が、試合のルールが <see cref="MatchObjective.Domination"/> のときだけ行い、
    /// 結果を <see cref="ServerSetState"/> で書く。クライアントは値が届いたときと Spawn 時 (途中参加) に円盤を持ち主の色で塗るだけ。
    /// HUD は <see cref="Spawned"/> から番号の順に状態を読む。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(ControlPointZone))]
    public sealed class NetControlPoint : NetworkBehaviour
    {
        private static readonly List<NetControlPoint> _spawned = new List<NetControlPoint>();

        private readonly NetworkVariable<NetControlPointState> _state =
            new NetworkVariable<NetControlPointState>(NetControlPointState.Neutral);

        private ControlPointZone _zone;
        private bool _registered;

        /// <summary>このプロセスで Spawn されている拠点。<see cref="Index"/> の昇順に並ぶ。</summary>
        public static IReadOnlyList<NetControlPoint> Spawned => _spawned;

        /// <summary><see cref="Spawned"/> の顔ぶれが変わるたびに増える。HUD はこれで並べ直すかを決める。</summary>
        public static int SpawnedVersion { get; private set; }

        /// <summary>番号と範囲を持つ拠点。</summary>
        public ControlPointZone Zone => _zone != null ? _zone : _zone = GetComponent<ControlPointZone>();

        /// <summary>拠点の番号 (HUD では 0 = A)。<see cref="Zone"/> が無ければ -1。</summary>
        public int Index => Zone != null ? Zone.Index : -1;

        /// <summary>届いている (サーバーでは書いた) 占領の状態。</summary>
        public NetControlPointState State => _state.Value;

        /// <summary>HUD とテストが OnValueChanged を購読する同期値。</summary>
        internal NetworkVariable<NetControlPointState> StateVariable => _state;

        private void Awake()
        {
            _zone = GetComponent<ControlPointZone>();
        }

        public override void OnNetworkSpawn()
        {
            if (Zone == null)
            {
                Debug.LogError("[Net] " + name + " has no ControlPointZone; run Fps.Editor.NetSceneBuilder.Build", this);
            }

            _state.OnValueChanged += HandleStateChanged;
            Register();
            ApplyMarker(_state.Value);
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= HandleStateChanged;
            Unregister();
        }

        public override void OnDestroy()
        {
            Unregister();
            base.OnDestroy();
        }

        /// <summary>
        /// サーバーで占領の状態を書く。今の値と同じなら書かずに false を返す。Spawn 前は書けないので何もせず false を返す
        /// (サーバーは毎フレーム書き直すので、Spawn した後のフレームで追いつく)。
        /// </summary>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        internal bool ServerSetState(NetControlPointState state)
        {
            if (!IsSpawned)
            {
                return false;
            }

            if (!IsServer)
            {
                throw new InvalidOperationException("control point state can only be written on the server");
            }

            if (_state.Value.Equals(state))
            {
                return false;
            }

            _state.Value = state;
            return true;
        }

        /// <summary>
        /// 読み込まれているシーンから、有効な拠点を集める。非アクティブなものは Spawn されないので入れない。
        /// 並びは見つかった順 (番号で並べるのは <see cref="ControlPointMap"/> の役目)。
        /// </summary>
        internal static List<NetControlPoint> CollectInLoadedScenes()
        {
            var points = new List<NetControlPoint>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    points.AddRange(root.GetComponentsInChildren<NetControlPoint>(false));
                }
            }

            return points;
        }

        /// <summary>
        /// points の範囲から <see cref="ControlPointMap"/> を作り、番号の順に並べた拠点を byIndex に入れる。
        /// 拠点が無い・範囲が不正・番号が重なるか抜けているなら false を返し、error に理由を入れる。
        /// </summary>
        internal static bool TryBuildMap(
            IReadOnlyList<NetControlPoint> points, out ControlPointMap map, out NetControlPoint[] byIndex, out string error)
        {
            map = null;
            byIndex = null;
            if (points == null || points.Count == 0)
            {
                error = "the scene has no NetControlPoint";
                return false;
            }

            var areas = new ControlPointArea[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point == null || point.Zone == null)
                {
                    error = "control point " + (point != null ? point.name : "(destroyed)") + " has no ControlPointZone";
                    return false;
                }

                try
                {
                    areas[i] = point.Zone.Area;
                }
                catch (ArgumentException e)
                {
                    error = "control point " + point.name + " has an invalid area: " + e.Message;
                    return false;
                }
            }

            try
            {
                map = new ControlPointMap(areas);
            }
            catch (ArgumentException e)
            {
                error = e.Message;
                return false;
            }

            byIndex = new NetControlPoint[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                byIndex[areas[i].Index] = points[i];
            }

            error = null;
            return true;
        }

        private void HandleStateChanged(NetControlPointState previous, NetControlPointState current)
        {
            ApplyMarker(current);
        }

        private void ApplyMarker(NetControlPointState state)
        {
            var zone = Zone;
            if (zone != null && zone.Owner != state.Owner)
            {
                zone.ShowOwner(state.Owner);
            }
        }

        private void Register()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            var index = _spawned.Count;
            while (index > 0 && _spawned[index - 1].Index > Index)
            {
                index--;
            }

            _spawned.Insert(index, this);
            SpawnedVersion++;
        }

        private void Unregister()
        {
            if (!_registered)
            {
                return;
            }

            _registered = false;
            _spawned.Remove(this);
            SpawnedVersion++;
        }
    }
}
