using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    /// <summary>
    /// ネット対戦のクライアントで、自分が倒れてから戻るまでと、ラウンドの合間 (<see cref="RoundPhase.Preparing"/>) に味方を見る観戦の中身。
    /// スナップショットから他のプレイヤーごとに <see cref="RemoteTrack"/> とチーム・生死を持ち、見る相手は
    /// 生きている (ダウン中も含む) 味方からオフラインと同じ <see cref="SpectateTargets"/> で選ぶ。
    /// 見る位置は相手の補間した位置と向きに付けた目 (<see cref="TryGetView"/>) で、カメラに写すのは持ち主 (NetV2SpectatePart)。
    /// </summary>
    /// <remarks>
    /// 倒れている間の観戦は、試合のルールのやられ演出 (<see cref="MatchRulesSync.DeathCamera"/>) が <see cref="DeathCamera.Ally"/> のときだけ始める。
    /// 見ていた相手が倒れたり抜けたりしたら、知らせを送らずに次の味方へ移る (サーバーも同じ <see cref="SpectateTargets"/> の決まりで移るため)。
    /// 見られる味方がいなければ、状態 (<see cref="Status"/>) は相手無しで出し、<see cref="TryGetView"/> は false を返す。
    /// </remarks>
    public sealed class ClientSpectate
    {
        private readonly int _localPlayerId;
        private readonly StanceGeometry _stance;
        private readonly Func<IMessage, bool> _send;
        private readonly SpectateRequestMessage _request = new SpectateRequestMessage();
        private readonly Dictionary<int, Remote> _remotes = new Dictionary<int, Remote>();
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private readonly List<SpectateCandidate> _candidates = new List<SpectateCandidate>();
        private readonly InterpolationClock _clock;
        private int _newestTick = -1;
        private bool _hasSelf;
        private Team _team;
        private bool _selfDead;
        private DeathCamera _deathCamera = DeathCamera.None;
        private ulong? _target;

        /// <param name="localPlayerId">自分のプレイヤーの Id (<see cref="MatchClient.PlayerId"/>)。</param>
        /// <param name="tickRate">サーバーの tick の頻度 (<see cref="MatchClient.TickRate"/>)。相手の姿の補間に使う。</param>
        /// <param name="stance">体の寸法 (<see cref="NetMotorSettings.Stance"/>)。相手の姿勢ごとの目の高さに使う。</param>
        /// <param name="send">サーバーへ確実に届く経路で送る口。つながっていなければ送らずに false を返す。</param>
        /// <exception cref="ArgumentNullException">stance か send が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">tickRate が正でない。</exception>
        public ClientSpectate(int localPlayerId, int tickRate, StanceGeometry stance, Func<IMessage, bool> send)
        {
            _localPlayerId = localPlayerId;
            _stance = stance ?? throw new ArgumentNullException(nameof(stance));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _clock = new InterpolationClock(tickRate);
        }

        /// <summary>今の観戦の状態。観戦していなければ null。</summary>
        public SpectateStatus? Status { get; private set; }

        /// <summary>見ている相手の Id。観戦していないか、見られる味方がいなければ null。</summary>
        public int? TargetId => Status?.TargetId;

        /// <summary>
        /// スナップショットの自分の行からチームと生死を、他の行から姿とチーム・生死を読む。並びから消えた人は忘れる。
        /// 試合のルールのやられ演出もここで読む。
        /// </summary>
        /// <exception cref="ArgumentNullException">snapshot が null。</exception>
        public void Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Tick > _newestTick)
            {
                _newestTick = snapshot.Tick;
            }

            _deathCamera = snapshot.Match.Rules.DeathCamera;
            _hasSelf = false;
            _present.Clear();
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId == _localPlayerId)
                {
                    _hasSelf = true;
                    _team = row.Team;
                    _selfDead = row.Health <= 0 && !row.Downed.IsDowned;
                    continue;
                }

                _present.Add(row.PlayerId);
                if (!_remotes.TryGetValue(row.PlayerId, out var remote))
                {
                    remote = new Remote();
                    _remotes.Add(row.PlayerId, remote);
                }

                remote.Team = row.Team;
                remote.IsAlive = row.Health > 0 || row.Downed.IsDowned;
                remote.Track.Record(snapshot.Tick, RemotePose.From(row));
            }

            _gone.Clear();
            foreach (var id in _remotes.Keys)
            {
                if (!_present.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (var id in _gone)
            {
                _remotes.Remove(id);
            }
        }

        /// <summary>
        /// deltaSeconds だけ補間を進め、観戦を始めるか・続けるか・やめるかを決めて <see cref="Status"/> を更新する。
        /// 見ていた相手を見られなくなっていれば次の味方へ移る。
        /// </summary>
        /// <param name="deltaSeconds">前のフレームからの秒数。0 以上の有限値。</param>
        /// <param name="round">ラウンドで進むモードの今の進み。ラウンドの無いモードや分からないときは null。</param>
        /// <returns>状態が変わったら true。</returns>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds が負か有限でない。</exception>
        public bool Update(float deltaSeconds, RoundPhase? round)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be zero or more and finite");
            }

            if (_newestTick >= 0)
            {
                _clock.Advance(deltaSeconds, _newestTick);
            }

            var reason = ReasonNow(round);
            if (!reason.HasValue)
            {
                _target = null;
                return SetStatus(null);
            }

            return Pick(0, reason.Value);
        }

        /// <summary>
        /// 見る相手を step の向きへ替え、サーバーへ <see cref="SpectateRequestMessage"/> で知らせる。
        /// 観戦していないか、見られる味方がいなければ替えず、知らせも送らない。
        /// </summary>
        /// <returns>替えて知らせを送れたら true。</returns>
        /// <exception cref="ArgumentOutOfRangeException">step が定義に無い値。</exception>
        public bool Step(SpectateStep step)
        {
            if (!Enum.IsDefined(typeof(SpectateStep), step))
            {
                throw new ArgumentOutOfRangeException(nameof(step), step, "step must be a defined SpectateStep");
            }

            if (!Status.HasValue || !Status.Value.TargetId.HasValue)
            {
                return false;
            }

            Pick(step == SpectateStep.Next ? 1 : -1, Status.Value.Reason);
            _request.Step = step;
            return _send(_request);
        }

        /// <summary>
        /// 見ている相手の今表示する目の位置と向き (Y 軸まわり、ラジアン)。目の高さは相手の姿勢 (立ち・しゃがみ・伏せ) の <see cref="StanceShape.EyeY"/>。
        /// </summary>
        /// <returns>観戦していないか、見られる味方がいないか、相手の姿の記録が無ければ false。</returns>
        public bool TryGetView(out Vector3 eye, out float yaw)
        {
            eye = default;
            yaw = 0f;
            var target = TargetId;
            if (!target.HasValue || !_remotes.TryGetValue(target.Value, out var remote)
                || !remote.Track.TrySample(_clock.RenderTick, out var pose))
            {
                return false;
            }

            eye = pose.Position + new Vector3(0f, _stance.Shape(pose.Stance).EyeY, 0f);
            yaw = pose.Yaw;
            return true;
        }

        /// <summary>観戦をやめ、覚えている相手と姿をすべて捨てる。</summary>
        /// <returns>観戦していたら true。</returns>
        public bool Reset()
        {
            _remotes.Clear();
            _newestTick = -1;
            _hasSelf = false;
            _target = null;
            return SetStatus(null);
        }

        private SpectateReason? ReasonNow(RoundPhase? round)
        {
            if (!_hasSelf)
            {
                return null;
            }

            if (round == RoundPhase.Preparing)
            {
                return SpectateReason.BetweenRounds;
            }

            return _selfDead && _deathCamera == DeathCamera.Ally ? SpectateReason.Dead : (SpectateReason?)null;
        }

        private bool Pick(int step, SpectateReason reason)
        {
            _candidates.Clear();
            foreach (var pair in _remotes)
            {
                _candidates.Add(new SpectateCandidate((ulong)pair.Key, pair.Value.Team, pair.Value.IsAlive));
            }

            _target = SpectateTargets.Pick(_candidates, (ulong)_localPlayerId, _team, _target, step);
            var count = 0;
            for (var i = 0; i < _candidates.Count; i++)
            {
                var candidate = _candidates[i];
                if (candidate.Team == _team && candidate.IsAlive)
                {
                    count++;
                }
            }

            return SetStatus(new SpectateStatus(_target.HasValue ? (int)_target.Value : (int?)null, count, reason));
        }

        private bool SetStatus(SpectateStatus? status)
        {
            if (Status == status)
            {
                return false;
            }

            Status = status;
            return true;
        }

        private sealed class Remote
        {
            public RemoteTrack Track { get; } = new RemoteTrack();

            public Team Team { get; set; }

            public bool IsAlive { get; set; }
        }
    }
}
