using System;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、<see cref="GameRules.Objective"/> が <see cref="MatchObjective.Domination"/> のときに
    /// 拠点の占領をサーバーで判定し、キル数に代えて拠点のポイントで勝敗を決める処理。
    /// </summary>
    /// <remarks>
    /// 試合の開始時 (Session を作る前) にシーンの <see cref="NetControlPoint"/> を集めて <see cref="DominationObjective"/> を組む。
    /// 拠点が無い・番号が不正なら試合を始めない。Domination のときはキル数の上限 (<see cref="ScoreLimit"/>) では終わらせず、
    /// 毎フレーム、生きていてダウンしていない参加者の位置から誰がどの拠点にいるかを伝えて占領を進める。
    /// ポイントが <see cref="DominationScoreLimit"/> に届いたら Session を <see cref="TeamDeathmatchSession.Finish"/> で終え、
    /// 時間切れで Session が先に終わったときもポイントの多いほうを勝ちにする。
    /// 拠点ごとの状態は各 <see cref="NetControlPoint"/> に、目標と両チームのポイントは <see cref="ObjectiveStatus"/> に書いて配る。
    /// 差し込み口はどれもサーバーでだけ、試合が動いている (Session がある) 間に呼ぶ。
    /// </remarks>
    public sealed partial class NetTeamDeathmatch
    {
        [SerializeField, Min(1), Tooltip("拠点制圧 (ルールの Objective が Domination) のとき、どちらかのチームがこのポイントに届いたら終わる")]
        private int _dominationScoreLimit = DominationRunner.DefaultScoreLimit;

        [SerializeField, Min(0.01f), Tooltip("拠点制圧で、1 人で中立の拠点を占領し終えるまでの秒数")]
        private float _captureSeconds = DominationRunner.DefaultCaptureSeconds;

        [SerializeField, Min(0.01f), Tooltip("拠点制圧で、拠点 1 つを持っているチームに 1 秒あたり入るポイント")]
        private float _pointsPerSecond = DominationRunner.DefaultPointsPerSecond;

        [SerializeField, Min(1), Tooltip("拠点制圧で、占領の速さに数える人数の上限")]
        private int _maxCapturers = DominationSpec.DefaultMaxCapturers;

        private readonly NetworkVariable<NetObjectiveStatus> _objectiveStatus =
            new NetworkVariable<NetObjectiveStatus>(NetObjectiveStatus.None);

        // サーバーで拠点制圧を動かしている間だけある。_controlPoints は拠点の番号の順
        private DominationObjective _domination;
        private NetControlPoint[] _controlPoints;

        /// <summary><see cref="NetHud"/> が OnValueChanged を購読する同期値。</summary>
        internal NetworkVariable<NetObjectiveStatus> ObjectiveStatusVariable => _objectiveStatus;

        /// <summary>
        /// 勝敗を何で決めているかと、拠点で決めるときの両チームのポイント。サーバーが試合を始めた時点の値で、
        /// それまでは <see cref="NetObjectiveStatus.None"/>。
        /// </summary>
        public NetObjectiveStatus ObjectiveStatus => _objectiveStatus.Value;

        /// <summary>サーバーで動いている拠点制圧。拠点制圧の試合でない・クライアントでは null。</summary>
        internal DominationObjective ServerDomination => IsRunningOnServer ? _domination : null;

        /// <summary>拠点制圧で勝利に必要なポイント。1 以上。試合の開始時 (Spawn 時) に読む。</summary>
        public int DominationScoreLimit
        {
            get => _dominationScoreLimit;
            set => _dominationScoreLimit = value;
        }

        /// <summary>拠点制圧で、1 人で中立の拠点を占領し終えるまでの秒数。0 より大きい有限値。試合の開始時 (Spawn 時) に読む。</summary>
        public float CaptureSeconds
        {
            get => _captureSeconds;
            set => _captureSeconds = value;
        }

        /// <summary>拠点制圧で、拠点 1 つあたり 1 秒に入るポイント。0 より大きい有限値。試合の開始時 (Spawn 時) に読む。</summary>
        public float PointsPerSecond
        {
            get => _pointsPerSecond;
            set => _pointsPerSecond = value;
        }

        /// <summary>拠点制圧で、占領の速さに数える人数の上限。1 以上。試合の開始時 (Spawn 時) に読む。</summary>
        public int MaxCapturers
        {
            get => _maxCapturers;
            set => _maxCapturers = value;
        }

        /// <summary>
        /// Session を作る前に呼ぶ。rules が拠点制圧なら、シーンの拠点を集めて拠点制圧を組み、キル数で終わらないよう
        /// killScoreLimit を int.MaxValue にする。そうでなければ killScoreLimit は <see cref="ScoreLimit"/> のまま。
        /// 拠点が無い・不正、または設定の値が不正なら false を返し、error に理由を入れる。
        /// </summary>
        private bool TryPrepareObjective(GameRules rules, out int killScoreLimit, out string error)
        {
            _domination = null;
            _controlPoints = null;
            if (rules.Objective != MatchObjective.Domination)
            {
                killScoreLimit = _scoreLimit;
                error = null;
                return true;
            }

            killScoreLimit = int.MaxValue;
            var found = NetControlPoint.CollectInLoadedScenes();
            if (!NetControlPoint.TryBuildMap(found, out var map, out var points, out error))
            {
                return false;
            }

            try
            {
                _domination = new DominationObjective(
                    map, _captureSeconds, _pointsPerSecond, _dominationScoreLimit, _timeLimitSeconds, _maxCapturers);
            }
            catch (ArgumentOutOfRangeException e)
            {
                error = "invalid domination settings: " + e.Message;
                return false;
            }

            _controlPoints = points;
            return true;
        }

        /// <summary>サーバーで試合を始められた (Session を作った) ときに呼ぶ。<see cref="ServerRules"/> が読める。</summary>
        partial void ObjectiveServerStarted();

        /// <summary>サーバーの毎フレーム、Session を進めた直後に呼ぶ。</summary>
        /// <param name="deltaSeconds">前のフレームからの秒数。</param>
        partial void ObjectiveServerTick(float deltaSeconds);

        /// <summary>プレイヤーを試合に加えた直後に呼ぶ。</summary>
        /// <param name="playerId">加えたプレイヤーの Id (クライアント Id と同じ値)。</param>
        /// <param name="player">加えたプレイヤー。</param>
        partial void ObjectivePlayerJoined(int playerId, NetPlayer player);

        /// <summary>プレイヤーを試合から外した直後に呼ぶ (切断を含む)。</summary>
        /// <param name="playerId">外したプレイヤーの Id。</param>
        partial void ObjectivePlayerLeft(int playerId);

        /// <summary>プレイヤーが倒されたときに呼ぶ。自滅なら killerId と victimId が同じ。</summary>
        partial void ObjectivePlayerKilled(int killerId, int victimId);

        /// <summary>試合が終わって次の試合のために点数などを戻した直後に呼ぶ。</summary>
        partial void ObjectiveMatchRestarted();

        /// <summary>
        /// 試合が終わったとき、勝ったチームを同期・ログに使う前に呼ぶ。拠点制圧ならポイントで決めた勝者に差し替える。
        /// そうでなければ Session がキル数で決めた値のまま。
        /// </summary>
        /// <param name="winner">勝ったチーム。引き分けなら null。</param>
        partial void ObjectiveDecideWinner(ref Team? winner);

        /// <summary>試合が終わったときに呼ぶ。</summary>
        /// <param name="winner">勝ったチーム (<c>ObjectiveDecideWinner</c> の後の値)。引き分けなら null。</param>
        partial void ObjectiveMatchEnded(Team? winner);

        /// <summary>サーバーで試合を Despawn するとき、Session を手放す前に呼ぶ。</summary>
        partial void ObjectiveServerStopped();

        partial void ObjectiveServerStarted()
        {
            if (_domination == null)
            {
                _objectiveStatus.Value = new NetObjectiveStatus(ServerRules.Objective, 0, 0);
                return;
            }

            _domination.Mode.PointCaptured += HandlePointCaptured;
            _domination.Mode.PointNeutralized += HandlePointNeutralized;
            SyncObjective();
            Debug.Log(DominationLog.Tag + " net points=" + _domination.Mode.PointCount
                + " scoreLimit=" + _domination.Mode.Spec.ScoreLimit
                + " captureSeconds=" + _domination.Mode.Spec.CaptureSeconds
                + " pointsPerSecond=" + _domination.Mode.Spec.PointsPerSecond
                + " maxCapturers=" + _domination.Mode.Spec.MaxCapturers);
        }

        partial void ObjectiveServerTick(float deltaSeconds)
        {
            if (_domination == null)
            {
                return;
            }

            var playing = _session.State == GameModeState.Playing;
            if (playing)
            {
                LocatePlayers();
            }

            if (_domination.Tick(playing, deltaSeconds))
            {
                // 続く HandleEnded の ObjectiveDecideWinner が同じ勝者を選ぶ
                _session.Finish(_domination.DecideWinner());
            }

            SyncObjective();
        }

        partial void ObjectivePlayerJoined(int playerId, NetPlayer player)
        {
            if (_domination != null && _session.HasPlayer(playerId))
            {
                _domination.SetPlayer(playerId, _session.GetTeam(playerId));
            }
        }

        partial void ObjectivePlayerLeft(int playerId)
        {
            _domination?.RemovePlayer(playerId);
        }

        partial void ObjectivePlayerKilled(int killerId, int victimId)
        {
            if (_domination != null && _domination.HasPlayer(victimId))
            {
                _domination.Locate(victimId, null);
            }
        }

        partial void ObjectiveMatchRestarted()
        {
            if (_domination == null)
            {
                return;
            }

            _domination.Reset();
            SyncObjective();
            Debug.Log(DominationLog.Restart());
        }

        partial void ObjectiveDecideWinner(ref Team? winner)
        {
            if (_domination != null)
            {
                winner = _domination.DecideWinner();
            }
        }

        partial void ObjectiveMatchEnded(Team? winner)
        {
            if (_domination == null)
            {
                return;
            }

            SyncObjective();
            Debug.Log(DominationLog.State(_domination.Mode));
        }

        partial void ObjectiveServerStopped()
        {
            if (_domination != null)
            {
                _domination.Mode.PointCaptured -= HandlePointCaptured;
                _domination.Mode.PointNeutralized -= HandlePointNeutralized;
            }

            _domination = null;
            _controlPoints = null;
        }

        // 倒れている人とダウン中の人は拠点に数えない
        private void LocatePlayers()
        {
            foreach (var pair in _playerById)
            {
                var playerId = pair.Key;
                if (!_domination.HasPlayer(playerId))
                {
                    continue;
                }

                var counts = pair.Value != null
                    && _session.HasPlayer(playerId)
                    && _session.IsAlive(playerId)
                    && !_session.IsDowned(playerId);
                _domination.Locate(playerId, counts ? PositionOf(playerId) : (System.Numerics.Vector3?)null);
            }
        }

        private void SyncObjective()
        {
            var mode = _domination.Mode;
            var status = new NetObjectiveStatus(
                MatchObjective.Domination,
                DominationHudFormat.DisplayedScore(mode.GetScore(Team.Alpha)),
                DominationHudFormat.DisplayedScore(mode.GetScore(Team.Bravo)));
            if (!_objectiveStatus.Value.Equals(status))
            {
                _objectiveStatus.Value = status;
            }

            for (var i = 0; i < _controlPoints.Length; i++)
            {
                var point = _controlPoints[i];
                if (point != null)
                {
                    point.ServerSetState(new NetControlPointState(mode.GetOwner(i), mode.GetControl(i), mode.IsContested(i)));
                }
            }
        }

        private void HandlePointCaptured(int pointIndex, Team team)
        {
            Debug.Log(DominationLog.Captured(pointIndex, team));
        }

        private void HandlePointNeutralized(int pointIndex, Team previousOwner)
        {
            Debug.Log(DominationLog.Neutralized(pointIndex, previousOwner));
        }
    }
}
