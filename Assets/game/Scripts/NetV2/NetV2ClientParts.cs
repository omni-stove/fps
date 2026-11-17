using System;
using System.Collections.Generic;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    /// <summary>
    /// <see cref="NetV2Client"/> に差し込む部品 (<see cref="INetV2ClientPart"/>) の一覧。機能の部品はここに足し、
    /// <see cref="NetV2Client"/> はつないだときに一度だけ作って、メッセージ・スナップショット・毎フレーム・後片付けを順に渡す。
    /// </summary>
    internal static class NetV2ClientParts
    {
        /// <param name="client">つなぐクライアント。部品はここから自分の Id やルールを読む。</param>
        /// <param name="send">サーバーへ確実に届く経路で送る口。つながっていなければ送らずに false を返す。</param>
        /// <exception cref="ArgumentNullException">client か send が null。</exception>
        public static List<INetV2ClientPart> Create(MatchClient client, Func<IMessage, bool> send)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (send == null)
            {
                throw new ArgumentNullException(nameof(send));
            }

            return new List<INetV2ClientPart>();
        }
    }
}
