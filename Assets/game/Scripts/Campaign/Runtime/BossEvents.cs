using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // CombatEvents に置かないのは、ボスを持たないゲームの戦闘の出来事にボスの型を残さないため。
    public static class BossEvents
    {
        // 静的な初期化で 1 度だけ登録するのは、ドメインの再読み込みを切った Play でも CombatEvents.ClearSubscribers がここの購読を外すようにするため。
        static BossEvents()
        {
            CombatEvents.RegisterClear(ClearSubscribers);
        }

        // 戦闘の結果 (体力・判断) は BossController が先に変え終えてから流す。演出の有無で戦いが変わらないように。
        public static event Action<BossPhaseChangedInfo> BossPhaseChanged;

        public static void Raise(BossPhaseChangedInfo info)
        {
            CombatEvents.Notify(BossPhaseChanged, info);
        }

        private static void ClearSubscribers()
        {
            BossPhaseChanged = null;
        }
    }

    public readonly struct BossPhaseChangedInfo
    {
        public BossPhaseChangedInfo(GameObject boss, Damageable damageable, string bossId, BossPhaseChange change)
        {
            Boss = boss;
            Damageable = damageable;
            BossId = bossId ?? string.Empty;
            PreviousIndex = change.PreviousIndex;
            Index = change.Index;
            PreviousPhaseId = change.Previous != null ? change.Previous.Id : string.Empty;
            PhaseId = change.Current != null ? change.Current.Id : string.Empty;
            HealthFraction = change.HealthFraction;
        }

        public GameObject Boss { get; }

        // HUD の体力バーが Health を読むのに使う。Boss から引き直させないよう一緒に渡す。
        public Damageable Damageable { get; }

        public string BossId { get; }

        public int PreviousIndex { get; }

        public int Index { get; }

        public string PreviousPhaseId { get; }

        public string PhaseId { get; }

        public float HealthFraction { get; }

        // 復活などで最初の段階へ戻ったとき。演出が「段階が進んだ」と取り違えないよう見分けられるようにする。
        public bool IsReset => Index < PreviousIndex;
    }
}
