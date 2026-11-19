using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 道筋と中継点はマップで決まるので持たず、PayloadCart に置く。
    // 台車の速さや時間は payload.json と同じ形の JSON で持ち、ルールの JSON (NetMatchFile) の payload と同じ値を書けるようにする。
    [CreateAssetMenu(fileName = "Payload", menuName = "Shooter/Modes/Payload")]
    public sealed class PayloadDefinition : ScriptableObject
    {
        [SerializeField, Min(0), Tooltip("台車を押す側 (攻め側) の陣営の ID。0 = Alpha, 1 = Bravo。もう一方が守り側")]
        private int _attackerSideId = PayloadRunner.DefaultAttackerSideId;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = PayloadRunner.DefaultRespawnSeconds;

        [SerializeField, Tooltip("台車の調整値の JSON (payload.json の形)。空なら入口 (GameContent) のもの、それも無ければ Resources/Tuning/payload を読む")]
        private TextAsset _tuning;

        public int AttackerSideId
        {
            get => _attackerSideId;
            set => _attackerSideId = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public TextAsset Tuning
        {
            get => _tuning;
            set => _tuning = value;
        }

        // 空なら null を返し、入口の調整値を使わせる。
        public PayloadSpec ReadSpec()
        {
            return _tuning != null
                ? KitTuning.Read(_tuning.text, $"TextAsset '{_tuning.name}'", PayloadJson.Parse, PayloadSpec.Default)
                : null;
        }

        public Payload CreatePayload(PayloadRoute route, PayloadSpec fallback)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (fallback == null)
            {
                throw new ArgumentNullException(nameof(fallback));
            }

            try
            {
                return PayloadRunner.CreatePayload(ReadSpec() ?? fallback, route, _attackerSideId, _respawnSeconds);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new ArgumentOutOfRangeException(e.ParamName, e.ActualValue, $"payload definition '{SourceName}' is invalid: {e.Message}");
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed payload definition)" : name;
    }
}
