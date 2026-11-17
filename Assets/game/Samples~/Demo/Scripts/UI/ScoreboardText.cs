using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// スコアボードの行に出す文。言葉の入る名前と接続状態は表のキー <c>hud.scoreboard.*</c> の <see cref="LocalizedText"/> で返し、
    /// <see cref="ScoreboardView"/> が今の言語で文にする。
    /// </summary>
    public static class ScoreboardText
    {
        /// <summary>この画面のプレイヤー自身の名前。</summary>
        public static LocalizedText LocalPlayerName => new LocalizedText("hud.scoreboard.you");

        /// <summary><see cref="ScoreboardConnection.Connected"/> の表示。</summary>
        public static LocalizedText ConnectedText => new LocalizedText("hud.scoreboard.connected");

        /// <summary><see cref="ScoreboardConnection.Disconnected"/> の表示。</summary>
        public static LocalizedText DisconnectedText => new LocalizedText("hud.scoreboard.disconnected");

        /// <summary>接続状態の列の見出し。</summary>
        public static LocalizedText ConnectionCaption => new LocalizedText("hud.scoreboard.connectionCaption");

        /// <summary>オンラインの参加者の名前。</summary>
        /// <example><c>PlayerName(3)</c> は <c>Player 3</c></example>
        public static LocalizedText PlayerName(int playerId)
        {
            return new LocalizedText("hud.scoreboard.player", playerId);
        }

        /// <summary>オフラインの標的の名前。number は 1 から数える。</summary>
        /// <example><c>TargetName(2)</c> は <c>標的 2</c></example>
        /// <exception cref="ArgumentOutOfRangeException">number が 1 未満。</exception>
        public static LocalizedText TargetName(int number)
        {
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be 1 or more");
            }

            return new LocalizedText("hud.scoreboard.target", number);
        }

        /// <summary>
        /// Bot の名前。キルフィード (<see cref="BotRoster.KillFeedName"/>) と同じ印を付け、言語によらない。
        /// </summary>
        /// <example><c>BotName(1)</c> は <c>[BOT] Bot 1</c></example>
        /// <exception cref="ArgumentOutOfRangeException">number が 1 未満。</exception>
        public static LocalizedText BotName(int number)
        {
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be 1 or more");
            }

            return LocalizedText.Literal(BotRoster.Tag + " Bot " + number.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>行の名前の文。空の名前は <c>default</c> (空の文)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">name の <see cref="ScoreboardName.Kind"/> が ScoreboardNameKind の値でない。</exception>
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

        /// <summary>キル数・デス数の欄の文字列。</summary>
        /// <exception cref="ArgumentOutOfRangeException">count が負。</exception>
        public static string Count(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            return count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>接続状態の欄の文。<see cref="ScoreboardConnection.None"/> は <c>default</c> (空の文)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">connection が ScoreboardConnection の値でない。</exception>
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
