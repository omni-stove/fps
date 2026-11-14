using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、キルカム (<see cref="DeathCamera.KillCam"/>) に渡す記録の切り出し。
    /// </summary>
    /// <remarks>
    /// 他人のクライアントで補間に使っている位置と向きの履歴 (<see cref="HitboxHistory"/>) を、今表示している tick までさかのぼって切り出す。
    /// 履歴は <see cref="HitboxHistory.DefaultCapacity"/> tick しか持たないので、再生できるのは長くてもその長さまで。
    /// 自分で動かしているプレイヤー・サーバーの本体は補間の履歴を持たないので、記録は無い。
    /// </remarks>
    public sealed partial class NetworkPlayerMotor
    {
        /// <summary>
        /// 今表示している時点までの seconds 秒の姿勢。目は体の位置から eyeOffset の所とする。
        /// 補間で表示している他人のプレイヤーでなければ、または履歴が足りなければ <see cref="KillCamClip.Empty"/>。
        /// </summary>
        internal KillCamClip CaptureKillCam(float seconds, Vector3 eyeOffset)
        {
            if (_role != NetworkPlayerRole.Interpolated || !_hasRemoteState || _remoteHistory == null || !(seconds > 0f) || float.IsInfinity(seconds))
            {
                return KillCamClip.Empty;
            }

            return KillCamClip.FromHistories(
                _remoteHistory,
                _remoteYawHistory,
                (float)_renderTick,
                TickRate,
                seconds,
                NetMath.ToNumerics(eyeOffset),
                transform.eulerAngles.y);
        }
    }
}
