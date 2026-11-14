namespace Fps.Server
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return ServerProgram.Run(args, ServerModules.Create, ServerRuntime.Process());
        }
    }
}
