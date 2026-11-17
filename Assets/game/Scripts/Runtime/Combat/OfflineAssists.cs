using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    /// <summary>
    /// オフラインの場面に 1 つだけ持つ <see cref="DamageLedger"/>。<see cref="Damageable"/> が受けたダメージを記録し、倒れたときにアシストを引く。
    /// 調整値は <see cref="KitTuning.Assist"/>。Play の開始時とシーンを読み替えたときに空に戻す。
    /// </summary>
    public static class OfflineAssists
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static DamageLedger _ledger;

        /// <summary>
        /// 場面の台帳。調整値が読み直された (入口を替えた・Play を始め直した) ときは、新しい値で作り直す。
        /// 作り直すと前の記録は消えるが、調整値を替えるのは試合の外なので、数え方の違う記録を持ち越さない方を取る。
        /// </summary>
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

        /// <summary>今の時計 (秒)。台帳の時刻はこれで揃える。</summary>
        public static double Now => Time.timeAsDouble;

        /// <summary>
        /// victim が受けた記録を消す。まだ台帳が無ければ何もしない。
        /// 台帳が無いときに作らないのは、体力を満たすたび (Awake を含む) に調整値を読み、オフライン Id を振ってしまわないため。
        /// </summary>
        public static void Forget(GameObject victim)
        {
            if (_ledger == null || victim == null)
            {
                return;
            }

            _ledger.Clear(CombatEvents.OfflineId(victim));
        }

        /// <summary>すべての記録を消す。次に <see cref="Ledger"/> を読んだときに調整値から作り直す。</summary>
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
