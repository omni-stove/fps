namespace Fps.Server
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return ServerProgram.Run(args, new IServerModule[] { new MatchRulesModule() }, ServerRuntime.Process());
        }
    }
}
