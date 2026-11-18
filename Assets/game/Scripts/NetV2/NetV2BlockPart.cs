using System;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    public sealed class NetV2BlockPart : INetV2ClientPart
    {
        private readonly VoiceRoster _roster;
        private readonly bool _appliesRoster;
        private readonly ClientBlocks _blocks;
        private readonly Control _control;
        private bool _started;
        private bool _disposed;

        // ボイスの部品が無い試合でも、名簿 (VoiceRosterMessage) が届けば試合をまたいで覚えられるよう、そのときはこちらが名簿を受ける。
        public NetV2BlockPart(MatchClient client, VoiceRoster roster, BlockListStore store, bool appliesRoster, Action<string> log)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _appliesRoster = appliesRoster;
            var list = store.Load();
            log(BlockLog.Loaded(list.BlockedCount, list.MutedCount));
            _blocks = new ClientBlocks(list, _roster, () => client.PlayerId, store.Save, log);
            _control = new Control(this);
            _blocks.Changed += _control.Raise;
        }

        public ClientBlocks Blocks => _blocks;

        public IBlockControl BlockControl => _control;

        public bool Handle(IMessage message)
        {
            if (!_appliesRoster || !(message is VoiceRosterMessage roster))
            {
                return false;
            }

            if (!_disposed)
            {
                _roster.Apply(roster);
            }

            return true;
        }

        // 部品はつなぐ前に作られ、試合に入ったことは最初のスナップショットで分かるので、そこで操る口を出す。
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null || _started)
            {
                return;
            }

            _started = true;
            GameSignals.BlockControl.Publish(_control);
        }

        public void Tick(float deltaSeconds)
        {
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.BlockControl.ClearIfCurrent(_control);
            _blocks.Changed -= _control.Raise;
            _blocks.Dispose();
        }

        // 部品そのものを渡すと UI から Handle や Dispose まで呼べてしまうので、操る口だけの包みを渡す。
        private sealed class Control : IBlockControl
        {
            private readonly NetV2BlockPart _part;

            public Control(NetV2BlockPart part)
            {
                _part = part;
            }

            public event Action Changed;

            public bool IsBlocked(int participantId)
            {
                return !_part._disposed && _part._blocks.IsBlocked(participantId);
            }

            public bool IsMuted(int participantId)
            {
                return !_part._disposed && _part._blocks.IsMuted(participantId);
            }

            public void SetBlocked(int participantId, bool blocked)
            {
                if (!_part._disposed)
                {
                    _part._blocks.SetBlocked(participantId, blocked);
                }
            }

            public void SetMuted(int participantId, bool muted)
            {
                if (!_part._disposed)
                {
                    _part._blocks.SetMuted(participantId, muted);
                }
            }

            public void Raise()
            {
                Changed?.Invoke();
            }
        }
    }
}
