using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    // 画面は参加者 Id で操作するが、試合をまたいで覚えるのはボイスの名簿 (VoiceRoster) が引けるログインの Id にする。
    // 名簿に載っていない人 (EOS を使わない UDP の試合など) は覚える Id が無いので、この試合の間だけ参加者 Id で止める。
    // 通信路が確かめていない Id は誰でも名乗れ、ほかの人の Id を名乗った相手を止めるとその人まで試合をまたいで止まるので、名簿に載っていない人と同じに扱う。
    public sealed class ClientBlocks : IDisposable
    {
        private readonly BlockList _list;
        private readonly VoiceRoster _roster;
        private readonly Func<int> _localPlayerId;
        private readonly Action<BlockList> _save;
        private readonly Action<string> _log;
        private readonly HashSet<int> _matchBlocked = new HashSet<int>();
        private readonly HashSet<int> _matchMuted = new HashSet<int>();
        private readonly List<int> _promote = new List<int>();
        private bool _disposed;

        public ClientBlocks(BlockList list, VoiceRoster roster, Func<int> localPlayerId, Action<BlockList> save, Action<string> log)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _roster.Changed += HandleRosterChanged;
        }

        public event Action Changed;

        public BlockList List => _list;

        public bool IsBlocked(int participantId)
        {
            return Is(BlockKind.Block, participantId);
        }

        public bool IsMuted(int participantId)
        {
            return Is(BlockKind.Mute, participantId);
        }

        public bool IsMutedUser(string userId)
        {
            // 画面の IsMuted と同じく、確かめた Id のときだけ保存済みの一覧を当てる。確かめていない Id に当てると画面から外せない。
            if (!_roster.TryGetPlayerId(userId, out var participantId))
            {
                return false;
            }

            return IsMuted(participantId);
        }

        public bool SetBlocked(int participantId, bool blocked)
        {
            return Set(BlockKind.Block, participantId, blocked);
        }

        public bool SetMuted(int participantId, bool muted)
        {
            return Set(BlockKind.Mute, participantId, muted);
        }

        // 名簿は一人ずつ引く口しか持たないので、参加者 Id から逆に引く。同じ参加者 Id の人が 2 人いれば、どちらか分からないので引かない。
        public bool TryGetUserId(int participantId, out string userId)
        {
            userId = null;
            foreach (var candidate in _roster.ProductUserIds)
            {
                if (!_roster.TryGetPlayerId(candidate, out var id) || id != participantId)
                {
                    continue;
                }

                if (userId != null)
                {
                    userId = null;
                    return false;
                }

                userId = candidate;
            }

            return userId != null && _roster.IsVerified(userId) && BlockList.IsValidUserId(userId);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _roster.Changed -= HandleRosterChanged;
            _matchBlocked.Clear();
            _matchMuted.Clear();
            Changed = null;
        }

        private bool Is(BlockKind kind, int participantId)
        {
            if (MatchSet(kind).Contains(participantId))
            {
                return true;
            }

            if (!TryGetUserId(participantId, out var userId))
            {
                return false;
            }

            return kind == BlockKind.Block ? _list.IsBlocked(userId) : _list.IsMuted(userId);
        }

        private bool Set(BlockKind kind, int participantId, bool on)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ClientBlocks));
            }

            if (participantId < 0)
            {
                _log(BlockLog.RefusedInvalid(kind, participantId));
                return false;
            }

            if (on && participantId == _localPlayerId())
            {
                _log(BlockLog.RefusedSelf(kind, participantId));
                return false;
            }

            var matchSet = MatchSet(kind);
            bool changed;
            bool remembered;
            if (TryGetUserId(participantId, out var userId))
            {
                remembered = true;
                var removedFromMatch = matchSet.Remove(participantId);
                changed = (kind == BlockKind.Block ? _list.SetBlocked(userId, on) : _list.SetMuted(userId, on)) || removedFromMatch;
                if (changed)
                {
                    Save();
                }
            }
            else
            {
                remembered = false;
                changed = on ? matchSet.Add(participantId) : matchSet.Remove(participantId);
            }

            if (!changed)
            {
                return false;
            }

            _log(BlockLog.Changed(kind, participantId, on, remembered));
            Changed?.Invoke();
            return true;
        }

        // 試合の途中で名簿が届いた人は、この試合だけのつもりで止めた分を覚える側へ移す。
        private void HandleRosterChanged()
        {
            if (_disposed)
            {
                return;
            }

            var promoted = Promote(BlockKind.Block) | Promote(BlockKind.Mute);
            if (promoted)
            {
                Save();
            }

            Changed?.Invoke();
        }

        private bool Promote(BlockKind kind)
        {
            var matchSet = MatchSet(kind);
            if (matchSet.Count == 0)
            {
                return false;
            }

            _promote.Clear();
            _promote.AddRange(matchSet);
            var any = false;
            foreach (var participantId in _promote)
            {
                if (!TryGetUserId(participantId, out var userId))
                {
                    continue;
                }

                matchSet.Remove(participantId);
                if (kind == BlockKind.Block)
                {
                    _list.SetBlocked(userId, true);
                }
                else
                {
                    _list.SetMuted(userId, true);
                }

                any = true;
                _log(BlockLog.Remembered(kind, participantId));
            }

            return any;
        }

        // 保存の失敗で手元の切り替えまで取り消すと、この試合の間も止められなくなるので、ログに残して続ける。
        private void Save()
        {
            try
            {
                _save(_list);
            }
            catch (Exception e)
            {
                _log(BlockLog.SaveFailed(e.Message));
            }
        }

        private HashSet<int> MatchSet(BlockKind kind)
        {
            return kind == BlockKind.Block ? _matchBlocked : _matchMuted;
        }
    }
}
