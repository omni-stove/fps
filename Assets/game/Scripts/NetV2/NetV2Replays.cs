using System;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // 一覧 (Runtime の ReplayLibrary) は再生の口に通信路 (NetV2) が要り、Runtime からは作れないので、NetV2 の起動で作って GameSignals.Replays に出す。
    public static class NetV2Replays
    {
        public static ReplayLibrary Library { get; private set; }

        public static ReplayLibrary Install()
        {
            return Install(ReplayLibrary.DefaultDirectory, NetV2Client.BeginReplay);
        }

        public static ReplayLibrary Install(string directory, Func<string, bool> play)
        {
            if (Library != null)
            {
                return Library;
            }

            Library = new ReplayLibrary(directory, play);
            Library.Refresh();
            Library.Publish();
            return Library;
        }

        public static void Uninstall()
        {
            var library = Library;
            Library = null;
            library?.Dispose();
        }

        // 記録できなくても試合は遊べるので、ファイルを開けなければ記録せずに本物の通信路をそのまま返す。
        public static IMatchTransport Record(
            IMatchTransport transport, string clientVersion, Action<string> log, out ReplayRecorder recorder, out string path)
        {
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            recorder = null;
            path = null;
            var library = Library;
            if (library == null)
            {
                return transport;
            }

            string candidate = null;
            FileStream output = null;
            try
            {
                candidate = library.NewRecordingPath(DateTime.UtcNow);

                // 試合中に一覧を開いても読めるよう、書いている間も読むことは許す (ReplayLibrary は書きかけを飛ばす)
                output = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                recorder = new ReplayRecorder(transport, output, clientVersion, log);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                output?.Dispose();
                log(ReplayLog.CannotRecord(candidate ?? library.Directory, e.Message));
                recorder = null;
                return transport;
            }

            path = candidate;
            log(ReplayLog.RecordingTo(candidate));
            return recorder;
        }

        // 承認の前に終わった試合は再生できないファイルになるので消し、一覧を読み直して画面に新しいリプレイを出す。
        public static void Kept(ReplayRecorder recorder, string path, Action<string> log)
        {
            if (recorder == null || path == null)
            {
                return;
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            recorder.Finish();
            if (!recorder.HasRecording)
            {
                try
                {
                    File.Delete(path);
                    log(ReplayLog.DiscardedEmpty(path));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    log(ReplayLog.DiscardFailed(path, e.Message));
                }
            }

            Library?.Refresh();
        }

        // ドメインの再読み込みを切った Play でも、前の Play の一覧 (GameSignals から消えたもの) を使い回さない
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Library = null;
        }
    }
}
