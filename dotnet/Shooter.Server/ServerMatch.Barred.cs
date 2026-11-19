using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // 投票で外された人を、その試合の間だけ入れない。BAN の一覧 (ServerBanList) に載せないのは、
    // 一覧はファイルに書かれて試合をまたいで残り、管理者が外すまで消えないため。
    internal sealed partial class ServerMatch
    {
        private readonly HashSet<string> _barredKeys = new HashSet<string>(StringComparer.Ordinal);

        public int BarredCount => _barredKeys.Count;

        // 鍵 (通信路が確かめた ProductUserId があればそれ) で覚える。鍵の無い人は見分けられないので覚えずに false を返す
        public bool BarUntilNextMatch(string rejoinKey)
        {
            if (string.IsNullOrEmpty(rejoinKey))
            {
                return false;
            }

            _barredKeys.Add(rejoinKey);
            return true;
        }

        public bool IsBarred(string rejoinKey)
        {
            return !string.IsNullOrEmpty(rejoinKey) && _barredKeys.Contains(rejoinKey);
        }

        private void ForgetBarredForNextMatch()
        {
            if (_barredKeys.Count == 0)
            {
                return;
            }

            _log.WriteLine(VoteKickLog.Unbarred(_barredKeys.Count));
            _barredKeys.Clear();
        }
    }
}
