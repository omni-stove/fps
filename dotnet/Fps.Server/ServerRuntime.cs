using System;
using System.Collections.Generic;
using System.IO;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>プロセスの外とのつながり。テストで標準出力・環境変数・シグナル・マップの置き場所を差し替えるために 1 つにまとめる。</summary>
    internal sealed class ServerRuntime
    {
        public ServerRuntime(
            TextWriter output,
            TextWriter error,
            Func<string, string> environment,
            IStopSignal stopSignal,
            IReadOnlyList<string> dataSearchRoots,
            Func<IMatchTransport> createTransport)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            StopSignal = stopSignal ?? throw new ArgumentNullException(nameof(stopSignal));
            DataSearchRoots = dataSearchRoots ?? throw new ArgumentNullException(nameof(dataSearchRoots));
            CreateTransport = createTransport ?? throw new ArgumentNullException(nameof(createTransport));
        }

        public TextWriter Output { get; }

        public TextWriter Error { get; }

        public Func<string, string> Environment { get; }

        public IStopSignal StopSignal { get; }

        public IReadOnlyList<string> DataSearchRoots { get; }

        public Func<IMatchTransport> CreateTransport { get; }

        public static ServerRuntime Process()
        {
            return new ServerRuntime(
                Console.Out,
                Console.Error,
                System.Environment.GetEnvironmentVariable,
                new PosixStopSignal(),
                ServerMapData.DefaultSearchRoots(),
                () => new UdpMatchTransport());
        }
    }
}
