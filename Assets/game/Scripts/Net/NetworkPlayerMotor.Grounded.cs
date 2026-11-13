using Fps.Core;
using Fps.Runtime;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、演出 (<see cref="CharacterAudio"/>) に見せる接地・しゃがみ・ダッシュ (<see cref="ICharacterMotionSource"/>)。
    /// 接地は役割ごとに、そのインスタンスが今表示している位置と同じ時点の値を返す: ホストとサーバーは確定状態、
    /// 予測するオーナーは最新の予測、他人のクライアントはサーバーから届いた接地を位置と同じ遅れで当てたもの。
    /// ダッシュはオーナーの入力にしか無く同期していないので、他人では分からないと答える (演出が速さから推す)。
    /// </summary>
    public sealed partial class NetworkPlayerMotor : ICharacterMotionSource
    {
        // 接地もしゃがみと同じくある tick で切り替わる値なので、補間せずに StanceHistory へ bool として記録する
        private StanceHistory _remoteGroundedHistory;
        private bool _remoteGrounded;

        /// <summary>立って歩くときの水平の速さ (m/s)。</summary>
        public float WalkSpeed => _walkSpeed;

        /// <summary>このインスタンスで今表示している位置で接地しているか。生成前は false。</summary>
        public bool IsGrounded
        {
            get
            {
                switch (_role)
                {
                    case NetworkPlayerRole.HostOwner:
                    case NetworkPlayerRole.ServerSimulated:
                        return _serverState.IsGrounded;
                    case NetworkPlayerRole.Predicted:
                        return _prediction != null && _prediction.Current.IsGrounded;
                    case NetworkPlayerRole.Interpolated:
                        return _remoteGrounded;
                    default:
                        return false;
                }
            }
        }

        /// <summary>オーナー (予測するクライアントとホスト) ならダッシュ中かを入れて true。それ以外は分からないので false。</summary>
        bool ICharacterMotionSource.TryGetSprinting(out bool sprinting)
        {
            sprinting = IsSprinting;
            return _role == NetworkPlayerRole.HostOwner || _role == NetworkPlayerRole.Predicted;
        }

        // 他人のクライアントで、補間の履歴を捨てて tick の接地から表示し直す (生成・テレポート)
        private void RestartRemoteGrounded(int tick, bool grounded)
        {
            _remoteGroundedHistory = new StanceHistory();
            _remoteGroundedHistory.Record(tick, grounded);
            _remoteGrounded = grounded;
        }

        private void RecordRemoteGrounded(int tick, bool grounded)
        {
            if (_remoteGroundedHistory == null)
            {
                RestartRemoteGrounded(tick, grounded);
                return;
            }

            _remoteGroundedHistory.Record(tick, grounded);
        }

        private void ShowRemoteGrounded(float renderTick)
        {
            if (_remoteGroundedHistory != null && _remoteGroundedHistory.TrySample(renderTick, out var grounded))
            {
                _remoteGrounded = grounded;
            }
        }

        private void ClearRemoteGrounded()
        {
            _remoteGroundedHistory = null;
            _remoteGrounded = false;
        }
    }
}
