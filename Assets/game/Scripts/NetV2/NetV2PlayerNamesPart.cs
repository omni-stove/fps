using Shooter.Protocol;

namespace Shooter.NetV2
{
    public sealed class NetV2PlayerNamesPart : INetV2ClientPart
    {
        private readonly ClientPlayerNames _names;
        private bool _disposed;

        // 名簿を外から渡せるのは、HUD とキルカメラが部品の並びを探さずに同じ名簿を引けるようにするため
        public NetV2PlayerNamesPart(ClientPlayerNames names = null)
        {
            _names = names ?? new ClientPlayerNames();
        }

        public ClientPlayerNames Names => _names;

        // 片付けた後に届いた名簿も他の部品には使い道がないので、受け取ったことにして捨てる。
        public bool Handle(IMessage message)
        {
            if (!(message is PlayerNamesMessage names))
            {
                return false;
            }

            if (!_disposed)
            {
                _names.Apply(names);
            }

            return true;
        }

        public void Apply(SnapshotMessage snapshot)
        {
        }

        public void Tick(float deltaSeconds)
        {
        }

        // プレイヤー Id はつなぐたびに振り直されるので、試合を出たら名簿を空にして、握っている側にも古い名前を捨てさせる。
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _names.Clear();
        }
    }
}
