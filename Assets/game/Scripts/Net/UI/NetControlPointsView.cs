using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;

namespace Fps.Net
{
    /// <summary>
    /// オンラインの拠点制圧の、拠点ごとの状態を HUD に出す (描き方は <see cref="ControlPointsView"/>)。
    /// <see cref="NetHud"/> が拠点制圧の試合のときだけ、Netcode で届いた <see cref="NetControlPointState"/> を <see cref="Show"/> で渡す。
    /// </summary>
    public sealed class NetControlPointsView : ControlPointsView
    {
        private readonly List<NetControlPointState> _netStates = new List<NetControlPointState>();
        private readonly List<ControlPointStatus> _statuses = new List<ControlPointStatus>();

        /// <summary>出している拠点の状態 (番号の順)。出していなければ空。</summary>
        public IReadOnlyList<NetControlPointState> States => _netStates;

        /// <summary>拠点の状態を番号の順に渡して出す。空なら <see cref="Clear"/> と同じ。</summary>
        public void Show(IReadOnlyList<NetControlPointState> states)
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            _netStates.Clear();
            _statuses.Clear();
            for (var i = 0; i < states.Count; i++)
            {
                _netStates.Add(states[i]);
                _statuses.Add(states[i].ToStatus());
            }

            Show(_statuses);
        }

        public override void Clear()
        {
            _netStates.Clear();
            base.Clear();
        }
    }
}
