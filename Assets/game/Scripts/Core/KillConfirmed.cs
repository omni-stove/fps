using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class KillConfirmed
    {
        // 小刻みな Tick を足し合わせると消える時刻にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        public event Action<KillConfirmedTag> TagExpired;

        private readonly List<KillConfirmedTag> _tags = new List<KillConfirmedTag>();
        private readonly List<KillConfirmedTag> _expired = new List<KillConfirmedTag>();
        private readonly Dictionary<Side, int> _confirmed = new Dictionary<Side, int>();
        private readonly Dictionary<Side, int> _denied = new Dictionary<Side, int>();
        private int _nextTagId = 1;

        public KillConfirmedSpec Spec { get; }

        // 参加者の追加とキルの報告をラップせず ModeSession をそのまま見せるのは、シーンの Runner が ModeSession に対して行う形に合わせるため。
        public ModeSession Session { get; }

        public IReadOnlyList<KillConfirmedTag> Tags => _tags;

        public KillConfirmed(KillConfirmedSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new TagClock(this) },
                new IndividualRespawn(spec.RespawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(spec.ScoreLimit), new TimeLimitWin() });
            Session.Ended += OnSessionEnded;
        }

        public int GetConfirmed(Side side) => _confirmed.TryGetValue(side, out var count) ? count : 0;

        public int GetDenied(Side side) => _denied.TryGetValue(side, out var count) ? count : 0;

        public KillConfirmedTag FindTag(int tagId)
        {
            foreach (var tag in _tags)
            {
                if (tag.Id == tagId)
                {
                    return tag;
                }
            }

            return null;
        }

        public bool CanDropTag(int ownerId)
        {
            RequirePlayer(ownerId, nameof(ownerId));
            return Session.State == GameModeState.Playing;
        }

        public KillConfirmedTag DropTag(int ownerId)
        {
            if (!CanDropTag(ownerId))
            {
                return null;
            }

            var tag = new KillConfirmedTag(_nextTagId++, ownerId, Session.GetSide(ownerId), Spec.TagLifetimeSeconds);
            _tags.Add(tag);
            return tag;
        }

        public KillConfirmedResult Collect(int tagId, int collectorId)
        {
            RequirePlayer(collectorId, nameof(collectorId));

            if (Session.State != GameModeState.Playing)
            {
                return KillConfirmedResult.NotPlaying;
            }

            var tag = FindTag(tagId);
            if (tag == null)
            {
                return KillConfirmedResult.Gone;
            }

            if (Session.IsDown(collectorId))
            {
                return KillConfirmedResult.CollectorDown;
            }

            Remove(tag);
            var side = Session.GetSide(collectorId);
            if (side == tag.OwnerSide)
            {
                _denied[side] = GetDenied(side) + 1;
                return KillConfirmedResult.Denied;
            }

            _confirmed[side] = GetConfirmed(side) + 1;
            Session.AddScore(side, Spec.PointsPerConfirm);
            return KillConfirmedResult.Confirmed;
        }

        public bool Discard(int tagId)
        {
            var tag = FindTag(tagId);
            if (tag == null)
            {
                return false;
            }

            Remove(tag);
            return true;
        }

        // オンラインのクライアントが、サーバーで数えた確定・阻止の数をそのまま写すための口。点数 (Session) は動かさない。
        public void SetCounts(Side side, int confirmed, int denied)
        {
            if (!Session.Layout.Contains(side))
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side is not part of this team layout");
            }

            if (confirmed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(confirmed), confirmed, "confirmed must not be negative");
            }

            if (denied < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(denied), denied, "denied must not be negative");
            }

            _confirmed[side] = confirmed;
            _denied[side] = denied;
        }

        // オンラインのクライアントが、サーバーが落としたタグを同じ Id で写すための口。持ち主は Session に居なくてよい。
        // 消える時刻はサーバーが数えて消えたタグを Discard させるので、残り秒は寿命の満タンのまま置く。
        public KillConfirmedTag ShowTag(int tagId, int ownerId, Side ownerSide)
        {
            if (tagId < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tagId), tagId, "tagId must be at least 1");
            }

            if (!Session.Layout.Contains(ownerSide))
            {
                throw new ArgumentOutOfRangeException(nameof(ownerSide), ownerSide, "ownerSide is not part of this team layout");
            }

            var existing = FindTag(tagId);
            if (existing != null)
            {
                if (existing.OwnerId != ownerId || existing.OwnerSide != ownerSide)
                {
                    throw new InvalidOperationException("tag " + tagId + " is already shown for another owner; discard it first");
                }

                return existing;
            }

            var tag = new KillConfirmedTag(tagId, ownerId, ownerSide, Spec.TagLifetimeSeconds);
            _tags.Add(tag);

            // 後から DropTag しても写したタグと Id が重ならないようにする。
            if (tagId >= _nextTagId)
            {
                _nextTagId = tagId == int.MaxValue ? tagId : tagId + 1;
            }

            return tag;
        }

        public void Reset()
        {
            ClearTags();
            _confirmed.Clear();
            _denied.Clear();
            _nextTagId = 1;
            Session.Reset();
        }

        private void Advance(float seconds)
        {
            if (!(seconds > 0f) || _tags.Count == 0)
            {
                return;
            }

            _expired.Clear();
            foreach (var tag in _tags)
            {
                tag.Remaining -= seconds;
                if (tag.Remaining <= _snap)
                {
                    _expired.Add(tag);
                }
            }

            foreach (var tag in _expired)
            {
                Remove(tag);
            }

            foreach (var tag in _expired)
            {
                TagExpired?.Invoke(tag);

                // ハンドラの中で Reset されたら、もう無いタグを知らせない。
                if (Session.State != GameModeState.Playing)
                {
                    break;
                }
            }

            _expired.Clear();
        }

        private void Remove(KillConfirmedTag tag)
        {
            tag.Remaining = 0d;
            _tags.Remove(tag);
        }

        private void ClearTags()
        {
            foreach (var tag in _tags)
            {
                tag.Remaining = 0d;
            }

            _tags.Clear();
        }

        private void OnSessionEnded(Side? winner)
        {
            ClearTags();
        }

        private void RequirePlayer(int playerId, string parameterName)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(parameterName, playerId, "no player with this id");
            }
        }

        private sealed class TagClock : IScoreRule
        {
            private readonly KillConfirmed _owner;

            public TagClock(KillConfirmed owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsForAssist(ModeSession session, int assisterId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side) => 0d;

            public void Advance(ModeSession session, float seconds) => _owner.Advance(seconds);
        }
    }
}
