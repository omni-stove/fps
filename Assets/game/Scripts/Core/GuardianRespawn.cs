using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ガーディアン (VIP の護衛) のリスポーン。陣営ごとに 1 人のリーダーを持ち、リーダーが倒れていない陣営の人は
    /// 内側の部品 (<see cref="Inner"/>) のとおりに戻る。リーダーが倒れた (か抜けた) 陣営は、リーダーを含めて誰も戻らなくなる。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>リーダーは <see cref="ChooseLeaders"/> (試合開始時) か <see cref="SetLeader"/> で決める。リーダーのいない陣営の人は内側の部品のとおりに戻る。</item>
    /// <item>リーダーが倒れたら、同じ陣営で待っていた人の待ちも取り消す (その人たちはその場で <see cref="ModeSession.IsEliminated"/> になる)。</item>
    /// <item>リーダーが倒れた陣営は試合の終わりまで戻れない。<see cref="Reset"/> でリーダーごと消し、次の試合で選び直す。</item>
    /// <item><see cref="CancelAll"/> (試合の終わり) ではリーダーと倒れたかどうかを残し、結果の表示に使えるようにする。</item>
    /// </list>
    /// </remarks>
    public sealed class GuardianRespawn : IRespawnPolicy
    {
        private readonly IRespawnPolicy _inner;
        private readonly Dictionary<Side, int> _leaders = new Dictionary<Side, int>();
        private readonly HashSet<Side> _fallen = new HashSet<Side>();

        /// <summary>陣営のリーダーが決まったときに呼ばれる。引数は陣営と playerId。</summary>
        public event Action<Side, int> LeaderChosen;

        /// <summary>陣営のリーダーが倒れたか抜けたときに 1 度だけ呼ばれる。引数は陣営と playerId。この時点でその陣営は戻れない。</summary>
        public event Action<Side, int> LeaderFell;

        /// <param name="inner">リーダーが倒れていない陣営の人の戻し方。</param>
        public GuardianRespawn(IRespawnPolicy inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>リーダーが倒れていない陣営の人の戻し方。</summary>
        public IRespawnPolicy Inner => _inner;

        public float? SecondsUntilNextWave => _inner.SecondsUntilNextWave;

        /// <summary>リーダーのいる陣営 (ID の昇順)。</summary>
        public IReadOnlyList<Side> LeaderSides
        {
            get
            {
                var sides = new List<Side>(_leaders.Keys);
                sides.Sort();
                return sides;
            }
        }

        /// <summary>side のリーダーの playerId。リーダーがいなければ null。倒れた後と抜けた後も残る。</summary>
        public int? GetLeader(Side side)
        {
            return _leaders.TryGetValue(side, out var id) ? id : (int?)null;
        }

        /// <summary>playerId がどれかの陣営のリーダーなら true。</summary>
        public bool IsLeader(int playerId)
        {
            foreach (var id in _leaders.Values)
            {
                if (id == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>side のリーダーが倒れたか抜けたなら true。リーダーがいなければ false。</summary>
        public bool HasLeaderFallen(Side side) => _fallen.Contains(side);

        /// <summary>side の人が倒れたら戻れるなら true (リーダーがいないか、リーダーが倒れていない)。</summary>
        public bool CanRespawn(Side side) => !_fallen.Contains(side);

        /// <summary>
        /// session の陣営のうち、人がいてリーダーがまだいない陣営ごとに、その陣営の人 (playerId の昇順) から pick の返す番号の人をリーダーにする。
        /// pick には陣営の人数が渡り、0 以上その数未満を返すこと (外れたら <see cref="InvalidOperationException"/>)。
        /// 決めた陣営ごとに <see cref="LeaderChosen"/> を出す。
        /// </summary>
        public void ChooseLeaders(ModeSession session, Func<int, int> pick)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (pick == null)
            {
                throw new ArgumentNullException(nameof(pick));
            }

            var ids = session.GetPlayerIds();
            foreach (var side in session.Sides)
            {
                if (_leaders.ContainsKey(side))
                {
                    continue;
                }

                var members = new List<int>();
                foreach (var id in ids)
                {
                    if (session.GetSide(id) == side)
                    {
                        members.Add(id);
                    }
                }

                if (members.Count == 0)
                {
                    continue;
                }

                var index = pick(members.Count);
                if (index < 0 || index >= members.Count)
                {
                    throw new InvalidOperationException($"pick returned {index} for {members.Count} member(s) of {side}");
                }

                Assign(side, members[index]);
            }
        }

        /// <summary>
        /// playerId をその陣営のリーダーにする。陣営にもうリーダーがいれば <see cref="InvalidOperationException"/>。
        /// playerId がもう倒れていれば、その場でリーダーが倒れたものとして扱う。
        /// </summary>
        public void SetLeader(ModeSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var side = session.GetSide(playerId);
            if (_leaders.ContainsKey(side))
            {
                throw new InvalidOperationException($"{side} already has a leader (player {_leaders[side]})");
            }

            Assign(side, playerId);
            if (session.IsDown(playerId))
            {
                Fall(session, side, playerId);
            }
        }

        public bool WillReturn(ModeSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return CanRespawn(session.GetSide(playerId)) && _inner.WillReturn(session, playerId);
        }

        public bool BeginWait(ModeSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var side = session.GetSide(playerId);
            if (_leaders.TryGetValue(side, out var leader) && leader == playerId && !_fallen.Contains(side))
            {
                Fall(session, side, playerId);
                return false;
            }

            return CanRespawn(side) && _inner.BeginWait(session, playerId);
        }

        /// <summary>playerId の待ちを取り消す (抜けた)。抜けたのがリーダーなら、その陣営はリーダーが倒れたのと同じく戻れなくなる。</summary>
        public void Cancel(int playerId)
        {
            _inner.Cancel(playerId);
            foreach (var pair in _leaders)
            {
                if (pair.Value == playerId && _fallen.Add(pair.Key))
                {
                    // 抜けた後は session から陣営の人を引けないので、待っている人は Tick で止める。
                    LeaderFell?.Invoke(pair.Key, playerId);
                    return;
                }
            }
        }

        public void CancelAll() => _inner.CancelAll();

        /// <summary>待ちと時刻に加え、リーダーとリーダーが倒れたことも消す。次の試合では <see cref="ChooseLeaders"/> で選び直す。</summary>
        public void Reset()
        {
            _inner.Reset();
            _leaders.Clear();
            _fallen.Clear();
        }

        public void Tick(ModeSession session, float elapsedSeconds, Action<int> respawn)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (respawn == null)
            {
                throw new ArgumentNullException(nameof(respawn));
            }

            _inner.Tick(session, elapsedSeconds, id =>
            {
                if (session.HasPlayer(id) && CanRespawn(session.GetSide(id)))
                {
                    respawn(id);
                }
            });
        }

        public float? GetRemaining(int playerId) => _inner.GetRemaining(playerId);

        private void Assign(Side side, int playerId)
        {
            _leaders[side] = playerId;
            LeaderChosen?.Invoke(side, playerId);
        }

        private void Fall(ModeSession session, Side side, int leaderId)
        {
            _fallen.Add(side);
            foreach (var id in session.GetPlayerIds())
            {
                if (session.GetSide(id) == side)
                {
                    _inner.Cancel(id);
                }
            }

            LeaderFell?.Invoke(side, leaderId);
        }
    }
}
