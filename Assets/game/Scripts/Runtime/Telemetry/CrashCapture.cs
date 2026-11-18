using System;
using System.Threading.Tasks;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // AppDomain.UnhandledException ではなく Unity のログの受け口で拾うのは、Unity が MonoBehaviour の中の例外を捕まえてログにだけ出し、AppDomain まで届かないため。
    // 受け口は Threaded の方を使い、ほかのスレッドで出た例外も、メインスレッドの次のフレームを待たずに書く。
    public sealed class CrashCapture : IDisposable
    {
        // 書き込みの失敗をログに出すと、その行がまた受け口に届くので、同じスレッドで書いている間に来たものは拾わない。
        [ThreadStatic]
        private static bool _capturing;

        private readonly TelemetryRecorder _recorder;

        public CrashCapture(TelemetryRecorder recorder)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        public bool IsAttached { get; private set; }

        public void Attach()
        {
            if (IsAttached)
            {
                return;
            }

            Application.logMessageReceivedThreaded += HandleLog;
            TaskScheduler.UnobservedTaskException += HandleUnobservedTask;
            IsAttached = true;
        }

        public void Detach()
        {
            if (!IsAttached)
            {
                return;
            }

            Application.logMessageReceivedThreaded -= HandleLog;
            TaskScheduler.UnobservedTaskException -= HandleUnobservedTask;
            IsAttached = false;
        }

        public void Dispose()
        {
            Detach();
        }

        public bool Capture(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception || _capturing)
            {
                return false;
            }

            _capturing = true;
            try
            {
                SplitCondition(condition, out var exceptionType, out var message);
                return _recorder.RecordCrash(exceptionType, message, stackTrace);
            }
            finally
            {
                _capturing = false;
            }
        }

        public bool Capture(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            if (_capturing)
            {
                return false;
            }

            _capturing = true;
            try
            {
                return _recorder.RecordCrash(exception);
            }
            finally
            {
                _capturing = false;
            }
        }

        // Unity は例外を "型の短い名前: 文" の 1 行にして渡すので、型の部分を切り出す。空白を含む頭は型ではなく文の一部とみなす。
        public static void SplitCondition(string condition, out string exceptionType, out string message)
        {
            var text = condition ?? string.Empty;
            var colon = text.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0)
            {
                var head = text.Substring(0, colon);
                if (head.IndexOfAny(new[] { ' ', '\t', '\n', '\r' }) < 0)
                {
                    exceptionType = head;
                    message = text.Substring(colon + 2);
                    return;
                }
            }

            if (text.Length > 0 && text.IndexOf(' ') < 0 && text.EndsWith("Exception", StringComparison.Ordinal))
            {
                exceptionType = text;
                message = string.Empty;
                return;
            }

            exceptionType = string.Empty;
            message = text;
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            Capture(condition, stackTrace, type);
        }

        // 誰も待たなかったタスクの例外は、Unity のログに出ないまま捨てられるので、ここで拾う
        private void HandleUnobservedTask(object sender, UnobservedTaskExceptionEventArgs args)
        {
            var exception = args.Exception?.Flatten();
            if (exception == null)
            {
                return;
            }

            foreach (var inner in exception.InnerExceptions)
            {
                Capture(inner);
            }
        }
    }
}
