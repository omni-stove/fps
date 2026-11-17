using System.Collections.Generic;

namespace Shooter.Server
{
    // 起動引数を読んでからモジュールを決めるのは、-register-lobby のように引数があるときだけ足すモジュールがあるため。
    internal delegate bool ServerModuleFactory(
        ServerLaunch launch, ServerRuntime runtime, ServerRulesData rules, ICollection<IServerModule> modules, out string error);
}
