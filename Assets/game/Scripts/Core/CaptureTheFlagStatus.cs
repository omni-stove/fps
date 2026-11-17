using System;

namespace Shooter.Core
{
    /// <summary>
    /// 旗取りの Runner が <see cref="ModeStatus.Detail"/> に入れる、旗取り固有の状態。作った時点の両陣営の旗の状態・運ぶ人・戻るまでの秒数と、
    /// この画面で操作しているプレイヤーが旗を運んでいるかを写して持つ。
    /// </summary>
    public sealed class CaptureTheFlagStatus
    {
        private readonly FlagState[] _states = new FlagState[2];
        private readonly int?[] _carriers = new int?[2];
        private readonly float?[] _returnSeconds = new float?[2];

        /// <param name="game">進めている試合。</param>
        /// <param name="localPlayerId">この画面で操作しているプレイヤー。いなければ null。</param>
        /// <exception cref="ArgumentNullException">game が null。</exception>
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

        /// <summary>この画面で操作しているプレイヤー。いなければ null。</summary>
        public int? LocalPlayerId { get; }

        /// <summary>この画面で操作しているプレイヤーの陣営。いないか試合に加わっていなければ null。</summary>
        public Side? LocalSide { get; }

        /// <summary>この画面で操作しているプレイヤーが運んでいる旗の陣営。運んでいなければ null。</summary>
        public Side? CarriedFlag { get; }

        /// <summary>この画面で操作しているプレイヤーが旗を運んでいれば true。</summary>
        public bool IsLocalCarrying => CarriedFlag.HasValue;

        /// <summary>flag の陣営の旗の状態。</summary>
        /// <exception cref="ArgumentOutOfRangeException">flag が Alpha でも Bravo でもない。</exception>
        public FlagState GetState(Side flag) => _states[Index(flag)];

        /// <summary>flag の陣営の旗を運んでいる人。運ばれていなければ null。</summary>
        /// <exception cref="ArgumentOutOfRangeException">flag が Alpha でも Bravo でもない。</exception>
        public int? GetCarrierId(Side flag) => _carriers[Index(flag)];

        /// <summary>flag の陣営の旗が落ちていれば、台に戻るまでの秒数。落ちていなければ null。</summary>
        /// <exception cref="ArgumentOutOfRangeException">flag が Alpha でも Bravo でもない。</exception>
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
