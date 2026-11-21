using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // 誰がリーダーで立っているかはサーバー (GuardianRules) が決めてスナップショットの場の物 (ObjectiveItemKind.Leader) で送るので、
    // ここは ClientGuardianStatus で読んだ立っているリーダーの体に、オフラインの GuardianRunner と同じ目印 (GuardianLeaderMarker) を付けるだけにする。
    // 自分がリーダーのときは GuardianRunner と同じく自分の頭上には出さず、HUD で知らせる。
    // HUD の ClientGuardianStatus を借りずに自分で読むのは、HUD はマップを替えるたびに作り直され、部品は接続の間ずっと生きるため
    public sealed class NetV2GuardianLeaderPart : INetV2ClientPart
    {
        public const string RootName = "NetV2" + GuardianRunner.MarkerRootName;

        private readonly Func<int> _localPlayerId;
        private readonly Func<int, NetV2RemotePlayer> _remote;
        private readonly GameObject _prefab;
        private readonly Action<string> _log;
        private readonly Dictionary<int, Side> _wanted = new Dictionary<int, Side>();
        private readonly Dictionary<int, Shown> _shown = new Dictionary<int, Shown>();
        private readonly List<int> _scratch = new List<int>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private ClientGuardianStatus _status;
        private int _statusFor;
        private Team? _viewer;
        private Transform _root;
        private bool _disposed;

        // prefab は目印の見た目で、null ならオフラインと同じ仮の菱形を作る。remote は相手の体を引く口で、null なら今の NetV2Client から引く
        public NetV2GuardianLeaderPart(
            Func<int> localPlayerId,
            GameObject prefab = null,
            Func<int, NetV2RemotePlayer> remote = null,
            Action<string> log = null)
        {
            _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
            _prefab = prefab;
            _remote = remote ?? RemoteInCurrentClient;
            _log = log ?? (line => Debug.Log(line));
        }

        // Guardian の試合でなければ null
        public ClientGuardianStatus Status => _status != null && _status.Status != null ? _status : null;

        public int MarkerCount => _shown.Count;

        // NetV2ClientParts.Registered に並べる口。再生でも誰がリーダーかは見返せるので、試合中と同じく出す
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var client = context.Client;
            return new NetV2GuardianLeaderPart(() => client.PlayerId, context.LeaderMarkerVisual, log: context.Log);
        }

        public GuardianLeaderMarker MarkerOf(int playerId)
        {
            return _shown.TryGetValue(playerId, out var shown) && shown.Marker != null ? shown.Marker : null;
        }

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            // 自分の Id は受け入れられてから決まり、部品はその前に作られるので、Id が変わったら読み手を作り直す
            var localId = _localPlayerId();
            if (_status == null || _statusFor != localId)
            {
                _status = new ClientGuardianStatus(localId);
                _statusFor = localId;
            }

            _status.Apply(snapshot);
            var own = snapshot.Players.IndexOf(localId);
            _viewer = own >= 0 ? snapshot.Players[own].Team : (Team?)null;

            _wanted.Clear();
            if (_status.Status != null)
            {
                Want(Team.Alpha, localId);
                Want(Team.Bravo, localId);
            }

            Sync();
        }

        // 相手の体はスナップショットより後に現れたり、見た目の差し替えや出直しで作り直されたりするので、毎フレーム付け直す
        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            Sync();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _wanted.Clear();
            Sync();
            _status = null;
            if (_root != null)
            {
                DestroySafely(_root.gameObject);
                _root = null;
            }
        }

        private void Want(Team team, int localId)
        {
            var leader = _status.LeaderOf(team);
            if (leader.HasValue && leader.Value != localId && _status.IsStandingLeader(leader.Value))
            {
                _wanted[leader.Value] = Side.FromTeam(team);
            }
        }

        private void Sync()
        {
            _scratch.Clear();
            foreach (var pair in _shown)
            {
                var shown = pair.Value;

                // 体が消えたら目印は自分で消えるので、作り直した体へ付け直すよう手放す
                if (shown.Marker == null || shown.Remote == null || shown.Marker.Anchor == null
                    || !_wanted.TryGetValue(pair.Key, out var side) || shown.Marker.Side != side)
                {
                    _scratch.Add(pair.Key);
                }
            }

            foreach (var playerId in _scratch)
            {
                Remove(playerId);
            }

            _scratch.Clear();
            foreach (var pair in _wanted)
            {
                if (!_shown.TryGetValue(pair.Key, out var shown))
                {
                    var remote = _remote(pair.Key);
                    if (remote == null)
                    {
                        continue;
                    }

                    shown = new Shown(remote, CreateMarker(remote, pair.Value));
                    _shown.Add(pair.Key, shown);
                    _log(ClientGuardianLog.MarkerShown(pair.Key, pair.Value));
                }

                if (shown.Marker.ViewerTeam != _viewer)
                {
                    shown.Marker.ViewerTeam = _viewer;
                }

                // 隠れている相手の居場所を目印で明かさない
                var visible = !shown.Remote.IsConcealed;
                if (shown.Marker.IsShown != visible)
                {
                    shown.Marker.SetShown(visible);
                }
            }
        }

        private GuardianLeaderMarker CreateMarker(NetV2RemotePlayer remote, Side side)
        {
            if (_root == null)
            {
                _root = new GameObject(RootName).transform;
            }

            var marker = GuardianLeaderMarker.Create(remote.transform, side, () => BodyBounds(remote), _prefab);
            marker.ViewerTeam = _viewer;
            marker.transform.SetParent(_root, true);
            return marker;
        }

        private void Remove(int playerId)
        {
            if (!_shown.TryGetValue(playerId, out var shown))
            {
                return;
            }

            _shown.Remove(playerId);
            if (shown.Marker != null)
            {
                shown.Marker.Remove();
            }

            _log(ClientGuardianLog.MarkerRemoved(playerId));
        }

        // 頭の高さは体の見た目で決める。ルートには撃つ・燃えるなどの演出も付き、その囲みで目印が浮かないよう体 (NetV2RemotePlayer.Body) の下だけを見る
        private Bounds? BodyBounds(NetV2RemotePlayer remote)
        {
            if (remote == null || remote.Body == null || !remote.Body.gameObject.activeInHierarchy)
            {
                return null;
            }

            remote.Body.GetComponentsInChildren(false, _renderers);
            Bounds? bounds = null;
            foreach (var bodyRenderer in _renderers)
            {
                if (!bodyRenderer.enabled)
                {
                    continue;
                }

                if (bounds.HasValue)
                {
                    var grown = bounds.Value;
                    grown.Encapsulate(bodyRenderer.bounds);
                    bounds = grown;
                }
                else
                {
                    bounds = bodyRenderer.bounds;
                }
            }

            _renderers.Clear();
            return bounds;
        }

        // 破棄済みの Unity の参照も無いものとして扱うため、?. ではなく != null で確かめる
        private static NetV2RemotePlayer RemoteInCurrentClient(int playerId)
        {
            var client = NetV2Client.Current;
            if (client == null)
            {
                return null;
            }

            var body = client.RemoteObject(playerId);
            return body != null ? body.GetComponent<NetV2RemotePlayer>() : null;
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private sealed class Shown
        {
            public Shown(NetV2RemotePlayer remote, GuardianLeaderMarker marker)
            {
                Remote = remote;
                Marker = marker;
            }

            public NetV2RemotePlayer Remote { get; }

            public GuardianLeaderMarker Marker { get; }
        }
    }
}
