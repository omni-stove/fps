using System;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class MenuReturnReason
    {
        private MenuReturnReason(MenuReturnKind kind, int exitCode, DisconnectNotice disconnect, string detail)
        {
            Kind = kind;
            ExitCode = exitCode;
            Disconnect = disconnect;
            Detail = detail;
        }

        public MenuReturnKind Kind { get; }

        // メニューへ戻る理由に終了コードを持たせるのは、batchmode のプレイヤーはメニューへ戻らずそのまま終わるため。
        public int ExitCode { get; }

        public DisconnectNotice Disconnect { get; }

        public string Detail { get; }

        public static MenuReturnReason Disconnected(DisconnectNotice notice, int exitCode)
        {
            if (notice == null)
            {
                throw new ArgumentNullException(nameof(notice));
            }

            return new MenuReturnReason(MenuReturnKind.Disconnected, exitCode, notice, string.Empty);
        }

        public static MenuReturnReason OnlineFailed(int exitCode, string detail)
        {
            return new MenuReturnReason(MenuReturnKind.OnlineFailed, exitCode, null, detail ?? string.Empty);
        }

        public string Describe()
        {
            var text = "return=" + Kind + " exit=" + ExitCode.ToString(CultureInfo.InvariantCulture);
            if (Disconnect != null)
            {
                text += " " + Disconnect.Describe();
            }

            if (Detail.Length > 0)
            {
                text += " detail=\"" + Detail + "\"";
            }

            return text;
        }

        public override string ToString()
        {
            return Describe();
        }
    }
}
