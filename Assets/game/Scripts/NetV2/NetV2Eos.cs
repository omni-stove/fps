using System;
using Shooter.Core;

namespace Shooter.NetV2
{
    // Shooter.NetEos はプラグインがあるビルドでだけコンパイルされるので、起動時にここへ入れてもらい、無いビルドでは null のまま -transport eos を断る。
    public static class NetV2Eos
    {
        public static IEosClientTransportFactory Factory { get; set; }

        // ボイスは EOS のロビーの RTC 部屋で話すので、EOS の通信路でつないだときだけ作り、そうでなければ null を返してボイスの部品を作らせない。
        public static IVoiceSession CreateVoiceSession(NetTransportKind transport, Action<string> log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            var factory = Factory;
            if (transport != NetTransportKind.Eos || factory == null)
            {
                log(VoiceLog.Off(transport != NetTransportKind.Eos ? "the match is not on the EOS transport" : "EOS is not in this build"));
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
