using System;

namespace Fps.Server
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return ServerProgram.Run(args, Array.Empty<IServerModule>(), Console.Out, Console.Error);
        }
    }
}
