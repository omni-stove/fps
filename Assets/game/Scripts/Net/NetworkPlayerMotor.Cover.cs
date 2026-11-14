using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、カバー。
    /// </summary>
    /// <remarks>
    /// カバーはしゃがみと同じく tick の入力 (<see cref="MotorInput.Cover"/>・<see cref="MotorInput.Aim"/>) に乗せ、予測とサーバーで同じ
    /// <see cref="NetMotorSimulation"/> を通す。状態 (<see cref="MotorState.Cover"/>) は確定状態に乗せて返し、段階か壁の高さが食い違えば巻き戻す。
    /// 使えるかは試合のルール (<see cref="NetMatchRules.Cover"/>) で決め、Off ならオーナーは Cover を送らず、サーバーは届いても捨てる。
    /// サーバーはカバーの状態が変わるたび <c>[Net] cover owner=.. tick=.. phase=.. height=..</c> を出す。
    /// 他人のクライアントは張り付いているかと壁の高さを位置と同じ遅れで当て、リグの姿勢 (<see cref="PoseCoverHeight"/>) に使う。
    /// </remarks>
    public sealed partial class NetworkPlayerMotor
    {
        public const string CoverActionPath = InputActionPaths.Cover;
        public const string AimActionPath = InputActionPaths.Aim;

        private InputAction _coverAction;
        private InputAction _aimAction;
        private bool _coverPressLatched;
        private AimDownSights _ads;

        // 他人のプレイヤーのカバー。張り付いているかと低い壁かを、しゃがみと同じく tick で切り替わる bool として持つ
        private StanceHistory _remoteInCoverHistory;
        private StanceHistory _remoteLowCoverHistory;
        private CoverHeight? _remoteCoverHeight;

        /// <summary>
        /// 試合のルールでカバーが使えるか。試合 (<see cref="NetTeamDeathmatch.Current"/>) の <see cref="NetTeamDeathmatch.SyncedRules"/> を読む
        /// (サーバーでは自分が書いた値、クライアントでは届いた値)。試合が無ければ false。
        /// </summary>
        public static bool CoverEnabled
        {
            get
            {
                var match = NetTeamDeathmatch.Current;
                return match != null && match.SyncedRules.CoverEnabled;
            }
        }

        /// <summary>
        /// このインスタンスの最新のカバーの状態。サーバー (ホストを含む) は確定状態、予測するオーナーは最新の予測。
        /// 他人のクライアントと生成前は <see cref="NetCoverState.Out"/> (他人の壁の法線などは届いても持たない)。
        /// ブラインドファイアの始点 (<see cref="NetBlindFire"/>) に使う。
        /// </summary>
        public NetCoverState Cover
        {
            get
            {
                switch (_role)
                {
                    case NetworkPlayerRole.HostOwner:
                    case NetworkPlayerRole.ServerSimulated:
                        return _serverState.Cover;
                    case NetworkPlayerRole.Predicted:
                        return _prediction != null ? _prediction.Current.Cover : NetCoverState.Out;
                    default:
                        return NetCoverState.Out;
                }
            }
        }

        /// <summary>
        /// リグの姿勢に使うカバーの壁の高さ。張り付いていなければ null。オーナーとサーバーは <see cref="Cover"/>、
        /// 他人のクライアントは届いた確定状態を位置と同じ遅れで当てた値。
        /// </summary>
        public CoverHeight? PoseCoverHeight
        {
            get
            {
                if (_role == NetworkPlayerRole.Interpolated)
                {
                    return _remoteCoverHeight;
                }

                var cover = Cover;
                return cover.IsInCover ? cover.Height : (CoverHeight?)null;
            }
        }

        /// <summary>体の半径 (m)。生成前で設定が不正なら CharacterController の値、それも無ければ 0。</summary>
        public float BodyRadius
        {
            get
            {
                if (_simulation != null)
                {
                    return _simulation.BodyRadius;
                }

                return _controller != null ? _controller.radius : 0f;
            }
        }

        /// <summary>
        /// カバーの状態が変わったときのログ。例: <c>[Net] cover owner=1 tick=42 phase=In height=Low</c>、カバーの外では
        /// <c>[Net] cover owner=1 tick=42 phase=Out</c>。
        /// </summary>
        public static string FormatCoverLog(ulong ownerClientId, int tick, CoverPhase phase, CoverHeight? height)
        {
            var log = "[Net] cover owner=" + ownerClientId.ToString(CultureInfo.InvariantCulture)
                + " tick=" + tick.ToString(CultureInfo.InvariantCulture)
                + " phase=" + phase;
            return phase == CoverPhase.Out || !height.HasValue ? log : log + " height=" + height.Value;
        }

        // サーバーへはルールで使えるときだけ送る。押したフレームは次の tick まで覚えておく
        private bool SampleCover()
        {
            var pressed = _coverPressLatched;
            _coverPressLatched = false;
            return pressed && CoverEnabled;
        }

        // カバー中に覗くか。オフラインの CoverController と同じく、ADS が動いていればその解釈 (押している間／切り替え) の後の値を使う
        private bool SampleAim()
        {
            if (_ads == null)
            {
                _ads = GetComponent<AimDownSights>();
            }

            if (_ads != null && _ads.isActiveAndEnabled && _ads.IsInitialized)
            {
                return _ads.IsAiming;
            }

            return _aimAction != null && _aimAction.IsPressed();
        }

        private void ReportServerCover(int tick, NetCoverState previous, NetCoverState current)
        {
            if (previous.Phase == current.Phase && (!current.IsInCover || previous.Height == current.Height))
            {
                return;
            }

            Debug.Log(FormatCoverLog(OwnerClientId, tick, current.Phase, current.IsInCover ? current.Height : (CoverHeight?)null), this);
        }

        private void ResetRemoteCover()
        {
            _remoteInCoverHistory = _role == NetworkPlayerRole.Interpolated ? new StanceHistory() : null;
            _remoteLowCoverHistory = _role == NetworkPlayerRole.Interpolated ? new StanceHistory() : null;
            _remoteCoverHeight = null;
        }

        private void RecordRemoteCover(int tick, NetCoverState cover)
        {
            if (_remoteInCoverHistory == null || _remoteLowCoverHistory == null)
            {
                return;
            }

            _remoteInCoverHistory.Record(tick, cover.IsInCover);
            _remoteLowCoverHistory.Record(tick, cover.IsInCover && cover.Height == CoverHeight.Low);
        }

        // 他人のプレイヤーのカバーを表示する。変わったときだけログを出す
        private void ShowRemoteCover(float tick)
        {
            if (_remoteInCoverHistory == null || _remoteLowCoverHistory == null
                || !_remoteInCoverHistory.TrySample(tick, out var inCover)
                || !_remoteLowCoverHistory.TrySample(tick, out var low))
            {
                return;
            }

            var height = inCover ? (low ? CoverHeight.Low : CoverHeight.High) : (CoverHeight?)null;
            if (height == _remoteCoverHeight)
            {
                return;
            }

            _remoteCoverHeight = height;
            Debug.Log(FormatCoverLog(OwnerClientId, Mathf.FloorToInt(tick), height.HasValue ? CoverPhase.In : CoverPhase.Out, height), this);
        }
    }
}
