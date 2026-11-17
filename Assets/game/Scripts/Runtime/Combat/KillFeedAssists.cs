using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // モードの OfflineAssists は参加者の番号で数えるので、モードの外でも倒れる Damageable の KillFeedEntry には、OfflineId で数えるこの台帳を別に持つ。
    // MonoBehaviour にしないのは、どのシーンにも置く配線が要るため。
    public static class KillFeedAssists
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static DamageLedger _ledger;

        // 調整値が読み直されたら作り直す。前の記録は消えるが、調整値を替えるのは試合の外なので、数え方の違う記録を持ち越さない方を取る。
        public static DamageLedger Ledger
        {
            get
            {
                var spec = KitTuning.Assist;
                if (_ledger == null || !ReferenceEquals(_ledger.Spec, spec))
                {
                    _ledger = new DamageLedger(spec);
                }

                return _ledger;
            }
        }

        public static double Now => Time.timeAsDouble;

        // 台帳が無いときに作らないのは、体力を満たすたび (Awake を含む) に調整値を読み、オフライン Id を振ってしまわないため。
        public static void Forget(GameObject victim)
        {
            if (_ledger == null || victim == null)
            {
                return;
            }

            _ledger.Clear(CombatEvents.OfflineId(victim));
        }

        public static void Reset()
        {
            _ledger = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Reset();

            // 静的なイベントは Play をまたいで残るので、二重に付かないよう外してから付ける。
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 足すだけの読み込み (Additive) は今の場面の続きなので、記録を残す。
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
            {
                Reset();
            }
        }
    }
}
