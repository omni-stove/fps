using System;

namespace Shooter.Core
{
    public sealed class CaptureTheFlagStatus
    {
        private readonly FlagState[] _states = new FlagState[2];
        private readonly int?[] _carriers = new int?[2];
        private readonly float?[] _returnSeconds = new float?[2];

        public CaptureTheFlagStatus(CaptureTheFlag game, int? localPlayerId)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            foreach (var side in CaptureTheFlag.FlagSides)
            {
                _states[side.Id] = game.GetFlagState(side);
                _carriers[side.Id] = game.GetCarrierId(side);
                _returnSeconds[side.Id] = game.GetReturnRemaining(side);
            }

            LocalPlayerId = localPlayerId;
            if (localPlayerId.HasValue && game.Session.HasPlayer(localPlayerId.Value))
            {
                LocalSide = game.Session.GetSide(localPlayerId.Value);
                CarriedFlag = game.GetCarriedFlag(localPlayerId.Value);
            }
        }

        public int? LocalPlayerId { get; }

        public Side? LocalSide { get; }

        public Side? CarriedFlag { get; }

        public bool IsLocalCarrying => CarriedFlag.HasValue;

        public FlagState GetState(Side flag) => _states[Index(flag)];

        public int? GetCarrierId(Side flag) => _carriers[Index(flag)];

        public float? GetReturnSeconds(Side flag) => _returnSeconds[Index(flag)];

        private static int Index(Side flag)
        {
            if (flag != Side.Alpha && flag != Side.Bravo)
            {
                throw new ArgumentOutOfRangeException(nameof(flag), flag, "flag must be Alpha or Bravo");
            }

            return flag.Id;
        }
    }
}
