using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// キルコンファームドのルール。Alpha と Bravo の 2 チームで戦い、倒しただけでは点が入らない。倒れた人はタグを落とし
    /// (<see cref="DropTag"/>)、敵がそのタグを拾えば拾った人のチームに <see cref="PointsPerConfirm"/> 点入る (確定)。
    /// 味方 (倒れた本人を含む) が拾えば点は入らずにタグが無くなる (阻止)。誰にも拾われないタグは
    /// <see cref="KillConfirmedSpec.TagLifetimeSeconds"/> 秒で消える。どちらかが ScoreLimit に届くか制限時間が切れたら終わる。
    /// </summary>
    /// <remarks>
    /// 中身は <see cref="ModeSession"/> に <see cref="TwoTeams"/>・タグの時計 (キルでも時間でも点を入れない <see cref="IScoreRule"/>)・
    /// <see cref="IndividualRespawn"/>・<see cref="ScoreLimitWin"/>・<see cref="TimeLimitWin"/> を組んだもの。
    /// 参加者は <see cref="Session"/> に直接加え、キルは Session に報告する (シーンの Runner が ModeSession に対して行う形に合わせるため)。
    /// タグを落とすのは倒れ方を問わない (倒した人が分からない死・味方に倒された死でも落とす)。試合が終わったら残ったタグはすべて消す
    /// (<see cref="TagExpired"/> は出さない)。
    /// </remarks>
    public sealed class KillConfirmed
    {
        /// <summary>敵のタグを 1 枚拾ったときに入る点。</summary>
        public const double PointsPerConfirm = 1d;

        // 小刻みな Tick を足し合わせると消える時刻にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        /// <summary>
        /// 拾われないタグが消えた瞬間に呼ばれる。呼ばれた時点でタグは <see cref="Tags"/> から外れている。
        /// 1 回の Tick で複数消えたら、落とされた順に呼ぶ。
        /// </summary>
        public event Action<KillConfirmedTag> TagExpired;

        private readonly List<KillConfirmedTag> _tags = new List<KillConfirmedTag>();
        private readonly List<KillConfirmedTag> _expired = new List<KillConfirmedTag>();
        private readonly Dictionary<Side, int> _confirmed = new Dictionary<Side, int>();
        private readonly Dictionary<Side, int> _denied = new Dictionary<Side, int>();
        private int _nextTagId = 1;

        public KillConfirmedSpec Spec { get; }

        /// <summary>進めているモード。参加者の追加・キルの報告・Tick はここに対して行う。</summary>
        public ModeSession Session { get; }

        /// <summary>落ちていて、まだ拾われも消えもしていないタグ。落とされた順。</summary>
        public IReadOnlyList<KillConfirmedTag> Tags => _tags;

        /// <param name="spec">試合の設定。</param>
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

        /// <summary>side が敵のタグを拾って確定させた数。</summary>
        public int GetConfirmed(Side side) => _confirmed.TryGetValue(side, out var count) ? count : 0;

        /// <summary>side が味方のタグを拾って阻止した数。</summary>
        public int GetDenied(Side side) => _denied.TryGetValue(side, out var count) ? count : 0;

        /// <summary>落ちている tagId のタグ。拾われたか消えたか、知らない番号なら null。</summary>
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

        /// <summary>今 ownerId がタグを落とせるか (試合が Playing のとき)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">ownerId が参加していない。</exception>
        public bool CanDropTag(int ownerId)
        {
            RequirePlayer(ownerId, nameof(ownerId));
            return Session.State == GameModeState.Playing;
        }

        /// <summary>
        /// ownerId が倒れてタグを落としたことにし、落ちたタグを返す。Playing でなければ何もせず null。
        /// Session へのキルの報告とは別に呼ぶ (前でも後でもよい)。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">ownerId が参加していない。</exception>
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

        /// <summary>
        /// collectorId が tagId のタグを拾う。敵のタグなら collectorId の陣営に <see cref="PointsPerConfirm"/> 点を足して
        /// (得点上限に届けばその陣営の勝ちで終わる) <see cref="KillConfirmedResult.Confirmed"/>、味方のタグなら点を足さずに
        /// <see cref="KillConfirmedResult.Denied"/>。どちらでもタグは無くなる。
        /// Playing でない・タグが無い・collectorId が倒れているときは何も変えずに理由を返す (この順に調べる)。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">collectorId が参加していない。</exception>
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
            Session.AddScore(side, PointsPerConfirm);
            return KillConfirmedResult.Confirmed;
        }

        /// <summary>
        /// tagId のタグを、点も阻止も数えずに無くす (参加者でない者に拾われたなど)。<see cref="TagExpired"/> は出さない。
        /// 無くしたら true、もう無ければ false。
        /// </summary>
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

        /// <summary>
        /// 次の試合のために、落ちているタグ・確定数・阻止数・タグの番号を消してから <see cref="ModeSession.Reset"/> する。
        /// <see cref="TagExpired"/> は出さない。
        /// </summary>
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

        /// <summary>キルでも時間でも点を入れず、Session の時間でタグを古くする。</summary>
        private sealed class TagClock : IScoreRule
        {
            private readonly KillConfirmed _owner;

            public TagClock(KillConfirmed owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side) => 0d;

            public void Advance(ModeSession session, float seconds) => _owner.Advance(seconds);
        }
    }
}
