using System;
using Shooter.Core;
using Shooter.Runtime;
using CorePartyStatus = Shooter.Core.PartyStatus;

namespace Shooter.NetV2
{
    // PartySession は試合をまたいでパーティを持つので、試合ごとに作り直す NetV2Client の部品にせず、メニューからも使えるよう別に持つ。
    public sealed class NetV2PartyControl : IPartyControl, IDisposable
    {
        private readonly PartySession _session;
        private readonly Func<int?> _skillRating;
        private CorePartyStatus _status;
        private CorePartyStatus? _published;
        private bool _disposed;

        public NetV2PartyControl(PartySession session)
            : this(session, NetV2Client.OwnSkillRating)
        {
        }

        // 値を引く口を受け取るのは、試合の結果で変わったプロフィールの値をパーティのメンバーへ載せ直せるようにするため。
        public NetV2PartyControl(PartySession session, Func<int?> skillRating)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _skillRating = skillRating ?? throw new ArgumentNullException(nameof(skillRating));
            _status = ClientPartyStatus.ToShown(_session.Status);
            _session.StatusChanged += HandleStatusChanged;
            _published = _status;
            GameSignals.Party.Publish(_status);
            GameSignals.PartyControl.Publish(this);
            Current = this;
        }

        public event Action<CorePartyStatus> StatusChanged;

        // NetV2Client が接続の承認データにパーティの Id を載せ、クイックマッチでパーティの人数分の空きを探すために引く。
        public static NetV2PartyControl Current { get; private set; }

        public CorePartyStatus Status => _status;

        public PartySession Session => _session;

        public string PartyId => _disposed ? null : _session.PartyId;

        public int MatchPlayers => _disposed ? 1 : _session.MatchPlayers;

        // クイックマッチを始める直前に引くので、Tick を待たずに今の自分の値を入れてから平均を出す。
        public int? SkillAverage
        {
            get
            {
                var own = _skillRating();
                if (_disposed)
                {
                    return own;
                }

                _session.SetSkillRating(own);
                return _session.SkillAverage;
            }
        }

        public void Create()
        {
            ThrowIfDisposed();
            _session.Create();
        }

        public void Join(string inviteCode)
        {
            ThrowIfDisposed();
            _session.Join(inviteCode);
        }

        public void Leave()
        {
            ThrowIfDisposed();
            _session.Leave();
        }

        public void Tick(float now)
        {
            if (!_disposed)
            {
                _session.SetSkillRating(_skillRating());
                _session.Tick(now);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _session.StatusChanged -= HandleStatusChanged;
            if (Current == this)
            {
                Current = null;
            }

            GameSignals.PartyControl.ClearIfCurrent(this);
            if (_published.HasValue)
            {
                GameSignals.Party.ClearIfCurrent(_published);
                _published = null;
            }

            StatusChanged = null;
            _session.Dispose();
        }

        private void HandleStatusChanged(PartyStatus status)
        {
            if (_disposed || status == null)
            {
                return;
            }

            var shown = ClientPartyStatus.ToShown(status);
            if (shown == _status && _published.HasValue)
            {
                return;
            }

            _status = shown;
            _published = shown;
            GameSignals.Party.Publish(shown);
            StatusChanged?.Invoke(shown);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetV2PartyControl));
            }
        }
    }
}
