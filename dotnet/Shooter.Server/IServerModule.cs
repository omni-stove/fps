using Shooter.Protocol;

namespace Shooter.Server
{
    // どのフックも ServerHost の 1 つのスレッドから順に呼ばれる。
    internal interface IServerModule
    {
        string Name { get; }

        void Start(ServerContext context);

        void Tick(long tick, float deltaSeconds);

        void OnConnected(ConnectionId connection);

        void OnDisconnected(ConnectionId connection);

        // message は受け取るたびに上書きして使い回すので、呼ばれている間だけ読み、持ち続けない。
        void OnMessage(ConnectionId connection, IMessage message);

        void Stop();
    }
}
