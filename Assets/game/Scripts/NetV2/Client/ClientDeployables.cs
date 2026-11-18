using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 置いた物はサーバーが毎回のスナップショットにすべて載せるので、そろったスナップショットに載っていない物は消えたとみなす。
    // 置く要求は置き場所の見本と同じ地点を送るだけで、置けたかは次のスナップショットで知る (手元では先に出さない)。
    public sealed class ClientDeployables
    {
        private readonly Func<IMessage, bool> _send;
        private readonly Action<string> _log;
        private readonly DeployRequestMessage _request = new DeployRequestMessage();
        private readonly Dictionary<int, Shown> _shown = new Dictionary<int, Shown>();
        private readonly HashSet<int> _listed = new HashSet<int>();
        private readonly List<int> _scratch = new List<int>();
        private IClientDeployableViews _views;

        public ClientDeployables(Func<IMessage, bool> send, Action<string> log = null)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log;
        }

        public bool IsAttached => _views != null;

        public int Count => _shown.Count;

        public void Attach(IClientDeployableViews views)
        {
            Detach();
            _views = views ?? throw new ArgumentNullException(nameof(views));
        }

        public void Detach()
        {
            foreach (var shown in _shown.Values)
            {
                shown.View?.Remove();
            }

            _shown.Clear();
            _views = null;
        }

        public bool Request(string typeId, Vector3 position, float yaw)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                throw new ArgumentException("typeId must not be empty", nameof(typeId));
            }

            if (!IsFinite(position))
            {
                throw new ArgumentException("position must be finite", nameof(position));
            }

            if (float.IsNaN(yaw) || float.IsInfinity(yaw))
            {
                throw new ArgumentException("yaw must be finite", nameof(yaw));
            }

            _request.TypeId = typeId;
            _request.Position = position;
            _request.Yaw = yaw;
            var sent = _send(_request);
            _log?.Invoke(ClientDeployLog.Requested(typeId, position, yaw, sent));
            return sent;
        }

        public bool TryGet(int deployableId, out ClientDeployable deployable)
        {
            if (_shown.TryGetValue(deployableId, out var shown))
            {
                deployable = shown.State;
                return true;
            }

            deployable = default;
            return false;
        }

        public void Apply(SnapshotMessage snapshot, int localPlayerId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!IsAttached || !snapshot.IsComplete)
            {
                return;
            }

            _listed.Clear();
            var rows = snapshot.Deployables;
            for (var i = 0; i < rows.Count; i++)
            {
                ref var row = ref rows[i];
                _listed.Add(row.DeployableId);
                var state = new ClientDeployable(
                    row.DeployableId, row.Kind, row.TypeId ?? "", row.Team, row.HasOwner ? row.OwnerId : (int?)null, row.Position, row.Yaw, row.Health);
                var isOwn = row.HasOwner && row.OwnerId == localPlayerId;
                if (_shown.TryGetValue(row.DeployableId, out var shown))
                {
                    // 同じ Id で種類や持ち主が替わったら別の物なので、見た目から作り直す
                    if (shown.State.SameIdentity(state) && shown.IsOwn == isOwn)
                    {
                        Update(shown, state);
                        continue;
                    }

                    Forget(row.DeployableId);
                }

                var view = _views.Show(state.Kind, state.TypeId, state.Team, isOwn, state.Position, state.Yaw);
                view?.SetHealth(state.Health);
                _shown.Add(row.DeployableId, new Shown(view, state, isOwn));
                _log?.Invoke(ClientDeployLog.Shown(row.DeployableId, state.TypeId, isOwn));
            }

            _scratch.Clear();
            foreach (var id in _shown.Keys)
            {
                if (!_listed.Contains(id))
                {
                    _scratch.Add(id);
                }
            }

            foreach (var id in _scratch)
            {
                Forget(id);
            }

            _scratch.Clear();
        }

        private static void Update(Shown shown, ClientDeployable state)
        {
            var previous = shown.State;
            shown.State = state;
            if (shown.View == null)
            {
                return;
            }

            if (previous.Position != state.Position || !previous.Yaw.Equals(state.Yaw))
            {
                shown.View.Move(state.Position, state.Yaw);
            }

            if (previous.Health != state.Health)
            {
                shown.View.SetHealth(state.Health);
            }
        }

        private void Forget(int id)
        {
            var shown = _shown[id];
            _shown.Remove(id);
            shown.View?.Remove();
            _log?.Invoke(ClientDeployLog.Gone(id, shown.State.TypeId));
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }

        private sealed class Shown
        {
            public Shown(IClientDeployableView view, ClientDeployable state, bool isOwn)
            {
                View = view;
                State = state;
                IsOwn = isOwn;
            }

            public IClientDeployableView View { get; }

            public ClientDeployable State { get; set; }

            public bool IsOwn { get; }
        }
    }
}
