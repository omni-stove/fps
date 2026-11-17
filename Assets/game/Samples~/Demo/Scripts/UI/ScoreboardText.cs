using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ScoreboardText
    {
        public static LocalizedText LocalPlayerName => new LocalizedText("hud.scoreboard.you");

        public static LocalizedText ConnectedText => new LocalizedText("hud.scoreboard.connected");

        public static LocalizedText DisconnectedText => new LocalizedText("hud.scoreboard.disconnected");

        public static LocalizedText ConnectionCaption => new LocalizedText("hud.scoreboard.connectionCaption");

        public static LocalizedText AssistsCaption => new LocalizedText("assist.scoreboard.caption");

        public static LocalizedText PlayerName(int playerId)
        {
            return new LocalizedText("hud.scoreboard.player", playerId);
        }

        public static LocalizedText TargetName(int number)
        {
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be 1 or more");
            }

            return new LocalizedText("hud.scoreboard.target", number);
        }

        // キルフィード (BotRoster.KillFeedName) と同じ名前に揃えるため、表を引かず言語によらない文にする。
        public static LocalizedText BotName(int number)
        {
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be 1 or more");
            }

            return LocalizedText.Literal(BotRoster.Tag + " Bot " + number.ToString(CultureInfo.InvariantCulture));
        }

        public static LocalizedText Name(ScoreboardName name)
        {
            switch (name.Kind)
            {
                case ScoreboardNameKind.Literal:
                    return name.IsEmpty ? default : LocalizedText.Literal(name.Text);
                case ScoreboardNameKind.LocalPlayer:
                    return LocalPlayerName;
                case ScoreboardNameKind.Player:
                    return PlayerName(name.Number);
                case ScoreboardNameKind.Target:
                    return TargetName(name.Number);
                case ScoreboardNameKind.Bot:
                    return BotName(name.Number);
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name.Kind, "name kind is not a valid ScoreboardNameKind");
            }
        }

        public static string Count(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            return count.ToString(CultureInfo.InvariantCulture);
        }

        public static LocalizedText ConnectionText(ScoreboardConnection connection)
        {
            switch (connection)
            {
                case ScoreboardConnection.None:
                    return default;
                case ScoreboardConnection.Connected:
                    return ConnectedText;
                case ScoreboardConnection.Disconnected:
                    return DisconnectedText;
                default:
                    throw new ArgumentOutOfRangeException(nameof(connection), connection, "connection is not a valid ScoreboardConnection");
            }
        }
    }
}
