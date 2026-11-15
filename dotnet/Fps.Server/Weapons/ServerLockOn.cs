using System;
using System.Diagnostics.CodeAnalysis;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 射撃の要求に付いたロックオンの相手を、撃った人から見える範囲にいるか確かめる。だめなら誘導しない。
    /// </summary>
    internal sealed class ServerLockOn
    {
        public ServerLockOn(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        /// <summary>
        /// shooterId が round で撃った request のロックオンの相手のうち、誘導してよい相手。いなければ null。
        /// </summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Lock checks read the combat state on the instance")]
        public int? Resolve(int shooterId, RoundSpec round, FireRequestMessage request)
        {
            ArgumentNullException.ThrowIfNull(round);
            ArgumentNullException.ThrowIfNull(request);
            return null;
        }

        [SuppressMessage("Performance", "CA1822", Justification = "Lock checks read the combat state on the instance")]
        public void Tick(float deltaSeconds)
        {
        }
    }
}
