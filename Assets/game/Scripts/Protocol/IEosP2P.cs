using System;

namespace Shooter.Protocol
{
    // サーバーは EOS の C# SDK を、クライアントは PlayEveryWare のプラグインを呼ぶので、SDK を呼ぶ部分だけをそれぞれが実装し、
    // つなぐ手順・メッセージの分割・切断の理由の運び方は EosMatchTransport の 1 か所に置く。
    public interface IEosP2P
    {
        bool TryBegin(bool acceptsIncoming, out string problem);

        void Tick();

        bool TryTakeNotice(out EosP2PNotice notice);

        bool TryReceivePacket(byte[] buffer, out string remoteUserId, out byte channel, out int length);

        // 相手との接続を勝手に開き直さない (接続は AcceptConnection の手順でだけ開く)。
        bool SendPacket(string remoteUserId, byte channel, bool reliable, ArraySegment<byte> packet, out string problem);

        void AcceptConnection(string remoteUserId);

        void CloseConnection(string remoteUserId);

        void CloseAll();
    }
}
