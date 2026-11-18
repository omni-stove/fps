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

        // 対戦のクライアントは CaptureTheFlag を持たないので、届いた旗の様子から同じ形を作る
        public CaptureTheFlagStatus(
            FlagState alphaState,
            int? alphaCarrierId,
            float? alphaReturnSeconds,
            FlagState bravoState,
            int? bravoCarrierId,
            float? bravoReturnSeconds,
            int? localPlayerId,
            Side? localSide)
        {
            Set(Side.Alpha, alphaState, alphaCarrierId, alphaReturnSeconds, "alpha");
            Set(Side.Bravo, bravoState, bravoCarrierId, bravoReturnSeconds, "bravo");
            if (localSide.HasValue && !localSide.Value.IsTeam)
            {
                throw new ArgumentOutOfRangeException(nameof(localSide), localSide.Value, "localSide must be Alpha or Bravo");
            }

            LocalPlayerId = localPlayerId;
            if (!localPlayerId.HasValue || !localSide.HasValue)
            {
                return;
            }

            LocalSide = localSide;
            foreach (var side in CaptureTheFlag.FlagSides)
            {
                if (_states[side.Id] == FlagState.Carried && _carriers[side.Id] == localPlayerId)
                {
                    CarriedFlag = side;
                }
            }
        }

        public int? LocalPlayerId { get; }

        public Side? LocalSide { get; }

        public Side? CarriedFlag { get; }

        public bool IsLocalCarrying => CarriedFlag.HasValue;

        public FlagState GetState(Side flag) => _states[Index(flag)];

        public int? GetCarrierId(Side flag) => _carriers[Index(flag)];

        public float? GetReturnSeconds(Side flag) => _returnSeconds[Index(flag)];

        private void Set(Side flag, FlagState state, int? carrierId, float? returnSeconds, string prefix)
        {
            if (state != FlagState.AtBase && state != FlagState.Carried && state != FlagState.Dropped)
            {
                throw new ArgumentOutOfRangeException(prefix + "State", state, "state is not a defined FlagState");
            }

            if (carrierId.HasValue != (state == FlagState.Carried))
            {
                throw new ArgumentException("a flag has a carrier exactly when it is carried", prefix + "CarrierId");
            }

            if (returnSeconds.HasValue != (state == FlagState.Dropped))
            {
                throw new ArgumentException("a flag has seconds until it returns exactly when it is dropped", prefix + "ReturnSeconds");
            }

            if (returnSeconds.HasValue && !(returnSeconds.Value >= 0f))
            {
                throw new ArgumentOutOfRangeException(prefix + "ReturnSeconds", returnSeconds.Value, "returnSeconds must be zero or positive");
            }

            _states[flag.Id] = state;
            _carriers[flag.Id] = carrierId;
            _returnSeconds[flag.Id] = returnSeconds;
        }

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
