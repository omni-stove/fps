using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// <see cref="ServerHost"/> に登録してサーバーの一生のフックを受け取る部品。試合のループ・戦闘・ルール・Bot・EOS・EAC などを
    /// それぞれ別のモジュールとして足す。どのフックも <see cref="ServerHost"/> の 1 つのスレッドから順に呼ばれる。
    /// </summary>
    internal interface IServerModule
    {
        /// <summary>ログに出す名前。</summary>
        string Name { get; }

        /// <summary>サーバーが動き出すとき、登録した順に 1 度だけ呼ばれる。投げるとサーバーは起動に失敗する。</summary>
        void Start(ServerContext context);

        /// <summary>サーバーの 1 tick ごとに、登録した順に呼ばれる。</summary>
        /// <param name="tick">1 から数えた tick の番号。</param>
        /// <param name="deltaSeconds">前の tick からの秒数。</param>
        void Tick(long tick, float deltaSeconds);

        /// <summary>接続が確立したとき、登録した順に呼ばれる。</summary>
        void OnConnected(ConnectionId connection);

        /// <summary>接続が切れたとき、登録した順に呼ばれる。</summary>
        void OnDisconnected(ConnectionId connection);

        /// <summary>
        /// サーバーが止まるとき、登録と逆の順に 1 度だけ呼ばれる。<see cref="Start"/> が終わったモジュールだけが呼ばれる。
        /// 投げても他のモジュールの停止は続く。
        /// </summary>
        void Stop();
    }
}
