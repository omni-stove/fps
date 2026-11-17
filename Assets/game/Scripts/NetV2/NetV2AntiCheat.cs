using System;
using Shooter.Core;
using Shooter.Protocol;
using UnityEngine;

namespace Shooter.NetV2
{
    // EAC の SDK の側 (IAntiCheatClientSession) を作り方 (CreateSession) で登録してもらうのは、
    // EOS のプラグインに依存するアセンブリ (Shooter.NetEos) を、このアセンブリが参照しないため。
    public static class NetV2AntiCheat
    {
        // 登録が無いまま EAC が有効なら、守られないまま遊ばせずに起動を断る。
        public static Func<IAntiCheatClientSession> CreateSession { get; set; }

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

        // Netcode のクライアント (EosAntiCheat) と同じ文にする。
        public static string LaunchHint => "; launch the game through the EAC bootstrapper (start_protected_game) or pass " + AntiCheatSettings.NoEacFlag;
    }
}
