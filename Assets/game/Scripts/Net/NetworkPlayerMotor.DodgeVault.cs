using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、回避と乗り越え。
    /// </summary>
    /// <remarks>
    /// 回避は Dodge の押下 (<see cref="MotorInput.Dodge"/>) を、乗り越えは Jump をそのまま tick の入力に乗せ、予測とサーバーで同じ
    /// <see cref="NetMotorSimulation"/> を通す。状態 (<see cref="MotorState.Dodge"/>・<see cref="MotorState.Vault"/>) は確定状態に乗せて返し、
    /// 転がっているか・乗り越えているかが食い違えば巻き戻す。
    /// 使えるかは試合のルール (<see cref="NetMatchRules.Dodge"/>・<see cref="NetMatchRules.Vault"/>) で決める。回避が Off ならオーナーは Dodge を送らず、
    /// サーバーは届いても捨てる。乗り越えはクライアントから送らず、サーバーと予測がそれぞれの知るルールを <see cref="MotorInput.VaultAllowed"/> に入れる。
    /// オーナーは回避を始めた tick でしゃがみとダッシュを解き (押し直すまで戻らない)、回避・乗り越えの最中は覗き込みを解く (オフラインと同じ)。
    /// サーバーは始まり・終わりのたび <c>[Net] dodge owner=.. tick=.. dodging=..</c>・<c>[Net] vault owner=.. tick=.. vaulting=..</c> を出し、
    /// 他人のクライアントは位置と同じ遅れで当てた値が変わったとき同じログを出す。
    /// </remarks>
    public sealed partial class NetworkPlayerMotor
    {
        public const string DodgeActionPath = InputActionPaths.Dodge;

        private InputAction _dodgeAction;
        private bool _dodgePressLatched;

        // 他人のプレイヤーの回避・乗り越え。しゃがみと同じく tick で切り替わる bool として持つ
        private StanceHistory _remoteDodgeHistory;
        private StanceHistory _remoteVaultHistory;
        private bool _remoteDodging;
        private bool _remoteVaulting;

        /// <summary>試合のルールで回避が使えるか (<see cref="CoverEnabled"/> と同じく同期されたルールを読む)。試合が無ければ false。</summary>
        public static bool DodgeEnabled
        {
            get
            {
                var match = NetTeamDeathmatch.Current;
                return match != null && match.SyncedRules.DodgeEnabled;
            }
        }

        /// <summary>試合のルールで乗り越えが使えるか (<see cref="CoverEnabled"/> と同じく同期されたルールを読む)。試合が無ければ false。</summary>
        public static bool VaultEnabled
        {
            get
            {
                var match = NetTeamDeathmatch.Current;
                return match != null && match.SyncedRules.VaultEnabled;
            }
        }

        /// <summary>
        /// このインスタンスで今回避の最中か。サーバー (ホストを含む) は確定状態、予測するオーナーは最新の予測、
        /// 他人のクライアントは届いた確定状態を位置と同じ遅れで当てた値。生成前は false。
        /// サーバーでは射撃を捨てる判断 (<see cref="NetTeamDeathmatch.ServerFire"/>) に、オーナーでは射撃を送らない判断に使う。
        /// </summary>
        public bool IsDodging
        {
            get
            {
                switch (_role)
                {
                    case NetworkPlayerRole.HostOwner:
                    case NetworkPlayerRole.ServerSimulated:
                        return _serverState.IsDodging;
                    case NetworkPlayerRole.Predicted:
                        return _prediction != null && _prediction.Current.IsDodging;
                    case NetworkPlayerRole.Interpolated:
                        return _remoteDodging;
                    default:
                        return false;
                }
            }
        }

        /// <summary>このインスタンスで今乗り越えの最中か。どの値を読むかは <see cref="IsDodging"/> と同じ。</summary>
        public bool IsVaulting
        {
            get
            {
                switch (_role)
                {
                    case NetworkPlayerRole.HostOwner:
                    case NetworkPlayerRole.ServerSimulated:
                        return _serverState.IsVaulting;
                    case NetworkPlayerRole.Predicted:
                        return _prediction != null && _prediction.Current.IsVaulting;
                    case NetworkPlayerRole.Interpolated:
                        return _remoteVaulting;
                    default:
                        return false;
                }
            }
        }

        /// <summary>回避の最中が変わったときのログ。例: <c>[Net] dodge owner=1 tick=42 dodging=true</c>。</summary>
        public static string FormatDodgeLog(ulong ownerClientId, int tick, bool dodging)
        {
            return "[Net] dodge owner=" + ownerClientId.ToString(CultureInfo.InvariantCulture)
                + " tick=" + tick.ToString(CultureInfo.InvariantCulture)
                + " dodging=" + (dodging ? "true" : "false");
        }

        /// <summary>乗り越えの最中が変わったときのログ。例: <c>[Net] vault owner=1 tick=42 vaulting=true</c>。</summary>
        public static string FormatVaultLog(ulong ownerClientId, int tick, bool vaulting)
        {
            return "[Net] vault owner=" + ownerClientId.ToString(CultureInfo.InvariantCulture)
                + " tick=" + tick.ToString(CultureInfo.InvariantCulture)
                + " vaulting=" + (vaulting ? "true" : "false");
        }

        /// <summary>
        /// ルールでの使える・使えないをサーバーの入力に当てる。回避が Off なら Dodge の押下を捨て、乗り越えはルールどおりにする
        /// (クライアントが何を送ってきても、サーバーの知るルールで決める)。
        /// </summary>
        internal static MotorInput ApplyMoveActionRules(MotorInput input, bool dodgeEnabled, bool vaultEnabled)
        {
            if (!dodgeEnabled)
            {
                input = input.WithoutDodge();
            }

            return input.WithVaultAllowed(vaultEnabled);
        }

        // サーバーへはルールで使えるときだけ送る。押したフレームは次の tick まで覚えておく
        private bool SampleDodge()
        {
            var pressed = _dodgePressLatched;
            _dodgePressLatched = false;
            return pressed && DodgeEnabled;
        }

        private void LatchDodgePress()
        {
            if (_dodgeAction != null && _dodgeAction.WasPressedThisFrame())
            {
                _dodgePressLatched = true;
            }
        }

        // オーナーで tick を進めた後に呼ぶ。回避を始めた tick でしゃがみとダッシュを解き、最中は覗き込みを解く (PlayerController・GameBootstrap と同じ)
        private void ApplyOwnerMoveActionEffects(MotorState previous, MotorState current)
        {
            if (current.IsDodging && !previous.IsDodging)
            {
                ReleaseCrouch(CrouchInput.ReasonDodge);
                ReleaseSprint(SprintInput.ReasonDodge);
            }

            if (!current.IsDodging && !current.IsVaulting)
            {
                return;
            }

            if (_ads == null)
            {
                _ads = GetComponent<AimDownSights>();
            }

            if (_ads != null && _ads.isActiveAndEnabled && _ads.IsInitialized && _ads.IsAiming)
            {
                _ads.Release();
            }
        }

        private void ReportServerMoveActions(int tick, MotorState previous, MotorState current)
        {
            if (previous.IsDodging != current.IsDodging)
            {
                Debug.Log(FormatDodgeLog(OwnerClientId, tick, current.IsDodging), this);
            }

            if (previous.IsVaulting != current.IsVaulting)
            {
                Debug.Log(FormatVaultLog(OwnerClientId, tick, current.IsVaulting), this);
            }
        }

        private void ResetRemoteMoveActions()
        {
            var remote = _role == NetworkPlayerRole.Interpolated;
            _remoteDodgeHistory = remote ? new StanceHistory() : null;
            _remoteVaultHistory = remote ? new StanceHistory() : null;
            _remoteDodging = false;
            _remoteVaulting = false;
        }

        private void RecordRemoteMoveActions(int tick, bool dodging, bool vaulting)
        {
            if (_remoteDodgeHistory == null || _remoteVaultHistory == null)
            {
                return;
            }

            _remoteDodgeHistory.Record(tick, dodging);
            _remoteVaultHistory.Record(tick, vaulting);
        }

        // 他人のプレイヤーの回避・乗り越えを位置と同じ遅れで当てる。変わったときだけログを出す
        private void ShowRemoteMoveActions(float tick)
        {
            if (_remoteDodgeHistory == null || _remoteVaultHistory == null)
            {
                return;
            }

            if (_remoteDodgeHistory.TrySample(tick, out var dodging) && dodging != _remoteDodging)
            {
                _remoteDodging = dodging;
                Debug.Log(FormatDodgeLog(OwnerClientId, Mathf.FloorToInt(tick), dodging), this);
            }

            if (_remoteVaultHistory.TrySample(tick, out var vaulting) && vaulting != _remoteVaulting)
            {
                _remoteVaulting = vaulting;
                Debug.Log(FormatVaultLog(OwnerClientId, Mathf.FloorToInt(tick), vaulting), this);
            }
        }
    }
}
