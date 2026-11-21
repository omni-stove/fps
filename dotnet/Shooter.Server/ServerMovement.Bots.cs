using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // Bot の体。接続を持たないので、Bot のモジュールが加え・外し・入力を直に渡す
    internal sealed partial class ServerMovement
    {
        // Bot は接続を持たないので、Bot のモジュールが試合に加えた直後にここで体を作る。
        public void AddBot(ServerPlayer player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!player.IsBot)
            {
                throw new ArgumentException("player " + player.PlayerId + " is not a bot", nameof(player));
            }

            if (_byPlayer.ContainsKey(player.PlayerId))
            {
                throw new ArgumentException("bot " + player.PlayerId + " already moves", nameof(player));
            }

            Add(player);
        }

        public bool RemoveBot(int playerId)
        {
            return BotRoster.IsBotId(playerId) && _byPlayer.Remove(playerId);
        }

        // 人の入力の予算で縛らないのは、Bot はサーバーの tick ごとに 1 つしか作らないため。
        public bool ApplyBotInput(int playerId, InputFrame frame)
        {
            if (!BotRoster.IsBotId(playerId) || !_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            if (frame.Tick <= mover.LastProcessedTick || !CharacterMotorInput.IsValid(frame.Move, frame.Yaw))
            {
                throw new ArgumentException("bot " + playerId + " input for tick " + frame.Tick + " is stale or not finite", nameof(frame));
            }

            Apply(mover, frame);
            return true;
        }
    }
}
