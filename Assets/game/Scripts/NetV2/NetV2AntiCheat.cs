using System;
using Shooter.Core;
using Shooter.Protocol;
using UnityEngine;

namespace Shooter.NetV2
{
    /// <summary>
    /// クライアントが EAC のパケットを .NET のサーバーと送受信する口。
    /// EAC の SDK の側 (<see cref="IAntiCheatClientSession"/>) は作り方 (<see cref="CreateSession"/>) を登録してもらう形にする。
    /// EOS のプラグインに依存するアセンブリ (Shooter.NetEos) を、このアセンブリが参照しないため。
    /// </summary>
    public static class NetV2AntiCheat
    {
        /// <summary>
        /// EAC の SDK の側を作る。EOS のプラグインがあるビルドで起動時に登録される。登録が無いまま EAC が有効なら、
        /// 守られないまま遊ばせずに起動を断る。
        /// </summary>
        public static Func<IAntiCheatClientSession> CreateSession { get; set; }

        /// <summary>
        /// サーバーとやり取りする <see cref="ClientAntiCheat"/> を作る。EAC を使うかは Netcode のクライアントと同じ
        /// <see cref="AntiCheatSettings.TryResolve"/> で決めたものを受け取る。
        /// </summary>
        public static bool TryCreate(
            AntiCheatSettings settings,
            MatchClient client,
            Action<string> remove,
            out ClientAntiCheat antiCheat,
            out string error)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            antiCheat = null;
            IAntiCheatClientSession session = null;
            if (settings.Enabled)
            {
                session = CreateSession?.Invoke();
                if (session == null)
                {
                    error = "this build has no EOS anti-cheat client; build with the EOS plugin or pass " + AntiCheatSettings.NoEacFlag;
                    return false;
                }
            }

            antiCheat = new ClientAntiCheat(
                settings.Enabled,
                session,
                message => client.Send(message, TransportChannel.Reliable),
                line => Debug.Log(line),
                remove);
            error = null;
            return true;
        }

        /// <summary>始められなかったときに添える。Netcode のクライアント (<c>EosAntiCheat</c>) と同じ文にする。</summary>
        public static string LaunchHint => "; launch the game through the EAC bootstrapper (start_protected_game) or pass " + AntiCheatSettings.NoEacFlag;
    }
}
