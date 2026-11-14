using System;
using System.IO;

namespace Fps.Server
{
    internal sealed class ServerEventWriter
    {
        private readonly TextWriter _output;
        private readonly TextWriter _log;

        public ServerEventWriter(bool enabled, TextWriter output, TextWriter log)
        {
            Enabled = enabled;
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>false なら呼ぶ側は行を作らない。<c>-log-format text</c> で JSON を組み立てる手間をかけないため。</summary>
        public bool Enabled { get; private set; }

        public void Write(string line)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                _output.WriteLine(line);
                _output.Flush();
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException)
            {
                // 試合の進行の途中で呼ばれるので、出力の失敗で試合を止めない
                Enabled = false;
                _log.WriteLine("[Net] cannot write match events to stdout (" + e.Message + "); JSON event lines are turned off");
            }
        }
    }
}
