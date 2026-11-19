using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // CombatEvents に置かないのは、飛行機を持たないゲームの戦闘の出来事に飛行機の型を残さず、飛行機ごと他のアセンブリへ出せるようにするため。
    public static class AircraftEvents
    {
        // 静的な初期化で 1 度だけ登録するのは、ドメインの再読み込みを切った Play でも CombatEvents.ClearSubscribers がここの購読を外すようにするため。
        static AircraftEvents()
        {
            CombatEvents.RegisterClear(ClearSubscribers);
        }

        public static event Action<AircraftPartBrokenInfo> PartBroken;

        public static void RaisePartBroken(AircraftPartBrokenInfo info)
        {
            CombatEvents.Notify(PartBroken, info);
        }

        private static void ClearSubscribers()
        {
            PartBroken = null;
        }
    }

    public readonly struct AircraftPartBrokenInfo
    {
        private readonly string _partId;

        public AircraftPartBrokenInfo(GameObject aircraft, string partId, AircraftDamageEffect effect, Vector3 position, AircraftCondition condition)
        {
            Aircraft = aircraft;
            _partId = partId ?? string.Empty;
            Effect = effect;
            Position = position;
            Condition = condition;
        }

        public GameObject Aircraft { get; }

        public string PartId => _partId ?? string.Empty;

        public AircraftDamageEffect Effect { get; }

        // 演出 (煙・火花) を壊れた部位から出せるよう、命中した部位の位置を渡す。
        public Vector3 Position { get; }

        public AircraftCondition Condition { get; }
    }
}
