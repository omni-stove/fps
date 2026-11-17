using System;

namespace Shooter.NetV2
{
    // Shooter.NetEos はプラグインがあるビルドでだけコンパイルされるので、起動時にここへ入れてもらい、無いビルドでは null のまま -transport eos を断る。
    public static class NetV2Eos
    {
        public static IEosClientTransportFactory Factory { get; set; }

        // ボイスはサーバーの試合ロビーの RTC 部屋で話し、部屋にはロビーのメンバーしか入れない。試合ロビーに入るのはロビーを探して
        // つないだとき (クイックマッチ・参加コード) だけなので、通信路が UDP か EOS かではなく、入ったロビーがあるかで決め、
        // 無ければ null を返してボイスの部品を作らせない。
        public static IVoiceSession CreateVoiceSession(string matchLobbyId, Action<string> log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (string.IsNullOrEmpty(matchLobbyId))
            {
                log(VoiceLog.Off("the match has no lobby (connected without the quick match or a join code)"));
                return null;
            }

            var factory = Factory;
            if (factory == null)
            {
                log(VoiceLog.Off("EOS is not in this build"));
                return null;
            }

            return factory.CreateVoiceSession(log);
        }

        // UDP でつなぐときも、ロビーを探すのに EOS へログインしていればその Id を載せる。サーバーは試合の記録で人を EOS の Id で引くため。
        public static string LocalProductUserId()
        {
            return Factory?.LocalProductUserId;
        }
    }
}
