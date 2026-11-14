using System.Collections.Generic;

namespace Fps.Server
{
    /// <summary>
    /// 起動引数を読んでからモジュールを決めるのは、<c>-register-lobby</c> のように引数があるときだけ足すモジュールがあるため。
    /// 足せないときは false と、ログに出す理由を返す (サーバーは設定の誤りとして終わる)。
    /// </summary>
    internal delegate bool ServerModuleFactory(
        ServerLaunch launch, ServerRuntime runtime, ServerRulesData rules, ICollection<IServerModule> modules, out string error);
}
