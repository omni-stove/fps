using System.Collections.Generic;
using Fps.Core;
using UnityEngine.UIElements;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetHud"/> のうち、試合の勝敗を拠点で決めるとき (<see cref="NetTeamDeathmatch.ObjectiveStatus"/> が
    /// <see cref="MatchObjective.Domination"/>) に、拠点の持ち主・占領の進み・ポイントを出す処理。
    /// </summary>
    /// <remarks>
    /// ポイントはチームの点数の欄に、キル数に代えて出す。拠点ごとの状態は <see cref="NetControlPoint.Spawned"/> を毎フレーム番号の順に読み、
    /// <see cref="NetControlPointsView"/> に渡す (変わった拠点だけ描き直される)。拠点は試合と別の NetworkObject で、
    /// どちらが先に Spawn されるかは決まっていないので購読せずに読む。拠点制圧でない試合と試合が無い間は隠す。
    /// </remarks>
    public sealed partial class NetHud
    {
        private readonly List<NetControlPointState> _controlPointStates = new List<NetControlPointState>();
        private NetObjectiveStatus _objectiveStatus = NetObjectiveStatus.None;
        private NetControlPointsView _controlPointsView;

        /// <summary>届いている勝敗の決め方と拠点のポイント。試合が無ければ <see cref="NetObjectiveStatus.None"/>。</summary>
        public NetObjectiveStatus ObjectiveStatus => _objectiveStatus;

        /// <summary>拠点の状態を出すビュー。拠点制圧の試合が届くまでは null。</summary>
        public NetControlPointsView ControlPoints => _controlPointsView;

        /// <summary>試合を購読し終えたときに呼ぶ。<see cref="Match"/> は新しい試合で、試合が無くなったときは null。</summary>
        partial void ObjectiveMatchBound();

        /// <summary>試合の購読を外す直前に呼ぶ。<see cref="Match"/> はまだ外す前の試合 (無ければ null)。</summary>
        partial void ObjectiveMatchUnbinding();

        /// <summary>毎フレーム呼ぶ。</summary>
        /// <param name="deltaSeconds">前のフレームからの秒数。</param>
        partial void ObjectiveTick(float deltaSeconds);

        /// <summary>プレイヤー・試合の購読をすべて外した後に呼ぶ (破棄するとき)。</summary>
        partial void ObjectiveReleased();

        /// <summary>チームの点数の欄に出す値を決める。拠点制圧の試合なら拠点のポイントに差し替える。</summary>
        /// <param name="alpha">Alpha の欄に出す値。初めはキル数の点数。</param>
        /// <param name="bravo">Bravo の欄に出す値。初めはキル数の点数。</param>
        partial void ObjectiveOverrideScores(ref int alpha, ref int bravo);

        partial void ObjectiveMatchBound()
        {
            if (Match == null)
            {
                _objectiveStatus = NetObjectiveStatus.None;
            }
            else
            {
                _objectiveStatus = Match.ObjectiveStatusVariable.Value;
                Match.ObjectiveStatusVariable.OnValueChanged += HandleObjectiveStatusChanged;
            }

            ShowScores();
            RefreshControlPoints(NetControlPoint.Spawned);
        }

        partial void ObjectiveMatchUnbinding()
        {
            // 破棄済みの試合でも NetworkVariable は C# のオブジェクトとして残っているので、ReferenceEquals で外す
            if (!ReferenceEquals(Match, null))
            {
                Match.ObjectiveStatusVariable.OnValueChanged -= HandleObjectiveStatusChanged;
            }
        }

        partial void ObjectiveTick(float deltaSeconds)
        {
            RefreshControlPoints(NetControlPoint.Spawned);
        }

        partial void ObjectiveReleased()
        {
            _objectiveStatus = NetObjectiveStatus.None;
            if (_controlPointsView != null)
            {
                _controlPointsView.Clear();
            }
        }

        partial void ObjectiveOverrideScores(ref int alpha, ref int bravo)
        {
            if (_objectiveStatus.IsDomination)
            {
                alpha = _objectiveStatus.PointsOf(Team.Alpha);
                bravo = _objectiveStatus.PointsOf(Team.Bravo);
            }
        }

        /// <summary>
        /// points (番号の順) の状態を拠点のビューに渡す。試合が無いか拠点制圧の試合でなければ隠す。破棄された拠点は飛ばす。
        /// Play 中は毎フレーム <see cref="NetControlPoint.Spawned"/> を渡す。
        /// </summary>
        internal void RefreshControlPoints(IReadOnlyList<NetControlPoint> points)
        {
            if (Match == null || !_objectiveStatus.IsDomination)
            {
                if (_controlPointsView != null)
                {
                    _controlPointsView.Clear();
                }

                return;
            }

            _controlPointStates.Clear();
            if (points != null)
            {
                foreach (var point in points)
                {
                    if (point != null)
                    {
                        _controlPointStates.Add(point.State);
                    }
                }
            }

            EnsureControlPointsView().Show(_controlPointStates);
        }

        // 拠点制圧のときだけ要るビューなので、Hud.AddViews では付けずに初めて要るときに付ける
        private NetControlPointsView EnsureControlPointsView()
        {
            if (_controlPointsView != null)
            {
                return _controlPointsView;
            }

            _controlPointsView = GetComponent<NetControlPointsView>();
            if (_controlPointsView == null)
            {
                _controlPointsView = gameObject.AddComponent<NetControlPointsView>();
            }

            _controlPointsView.SetTeamColors(NetPlayer.AlphaColor, NetPlayer.BravoColor);

            // 木を作ったときの Bind には間に合っていないので、今の木があればここで要素を渡す (作り直したときは Hud が渡す)
            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (_controlPointsView.Element == null && root != null && root.childCount > 0)
            {
                _controlPointsView.Bind(root);
            }

            return _controlPointsView;
        }

        private void HandleObjectiveStatusChanged(NetObjectiveStatus previous, NetObjectiveStatus current)
        {
            _objectiveStatus = current;
            ShowScores();
            RefreshControlPoints(NetControlPoint.Spawned);
        }
    }
}
