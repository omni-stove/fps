using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 機体の SimpleAircraftMotor と同じ GameObject に置く。モーターは IAircraftConditionSource を毎ステップ引き直すので、付けるだけで効く。
    [DisallowMultipleComponent]
    public sealed class AircraftPartDamage : MonoBehaviour, IAircraftConditionSource, INetVehicleParts
    {
        [SerializeField, Tooltip("部位の耐久と効き先を読む機体のアセット (Damage の欄)。空なら既定値")]
        private AircraftDefinition _definition;

        [SerializeField, Tooltip("部位への命中を受ける乗り物。空なら自分か親から探す")]
        private Vehicle _vehicle;

        private AircraftDamageSpec _configuredSpec;
        private AircraftDamage _damage;
        private Vehicle _attached;
        private Damageable _attachedBody;

        public AircraftDefinition Definition
        {
            get => _definition;
            set
            {
                _definition = value;
                _configuredSpec = null;
                _damage = null;
            }
        }

        public Vehicle Vehicle => _attached;

        // Configure で渡した値をアセットより優先するのは、テストやコードから組んだ機体がアセット無しでも値を差し替えられるようにするため。
        public AircraftDamage Damage => _damage ??= new AircraftDamage(_configuredSpec ?? BuildSpec());

        public AircraftDamageSpec Spec => Damage.Spec;

        public AircraftCondition Condition => Damage.Condition;

        // つないだ乗り物でなく最後に渡された乗り物の名前を使うのは、NetV2 が命中の受け口を外した (Detach) 後のログも乗り物の名前で出すため。
        private string VehicleName => _vehicle != null ? _vehicle.name : name;

        private void OnEnable()
        {
            var vehicle = _vehicle != null ? _vehicle : GetComponentInParent<Vehicle>();
            if (vehicle != null)
            {
                Attach(vehicle);
            }
        }

        private void OnDisable()
        {
            Detach();
        }

        private void OnValidate()
        {
            _damage = null;
        }

        // 渡した値をシリアライズしないのは、残す値の置き場をアセット (Definition) だけにし、シーンとアセットで値が食い違わないようにするため。
        public void Configure(AircraftDamageSpec spec)
        {
            _configuredSpec = spec ?? throw new ArgumentNullException(nameof(spec));
            _damage = new AircraftDamage(spec);
        }

        // Edit Mode では OnEnable が走らないので、組み立てる側やテストが直接つなげるようにする。
        public void Attach(Vehicle vehicle)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            Detach();
            _vehicle = vehicle;
            _attached = vehicle;
            _attached.PartDamaged += OnPartDamaged;
            _attachedBody = vehicle.Damageable;
            _attachedBody.HealthReset += OnHealthReset;
        }

        public void Detach()
        {
            if (_attached != null)
            {
                _attached.PartDamaged -= OnPartDamaged;
            }

            if (_attachedBody != null)
            {
                _attachedBody.HealthReset -= OnHealthReset;
            }

            _attached = null;
            _attachedBody = null;
        }

        public void Repair()
        {
            Damage.Repair();
        }

        // 命中の流れを通さずに部位を削る (落下や衝突で翼を傷めるなど) 呼び出し口。命中と同じく壊れた知らせとログを出す。
        public bool ApplyPartDamage(string partId, int damage, Vector3 position)
        {
            if (!Damage.TryApply(partId, damage, out var result))
            {
                return false;
            }

            if (result.Broke)
            {
                ReportBroken(result, position);
            }

            return true;
        }

        // 届いた中に無い部位は撃たれていないので、耐久のまま (修理された) に戻す。
        // 壊れた知らせ (AircraftEvents) を出さないのは、届くのは受けた量の合計だけなので、途中から入った人にも過去の破損を出し直してしまうため。
        public void ApplyServerTaken(Func<string, int> takenOf)
        {
            if (takenOf == null)
            {
                throw new ArgumentNullException(nameof(takenOf));
            }

            var damage = Damage;
            var before = damage.Condition;
            foreach (var part in damage.Spec.Parts)
            {
                damage.SetRemaining(part.Id, Math.Max(0, part.Durability - takenOf(part.Id)));
            }

            if (damage.Condition != before)
            {
                Debug.Log(AircraftLog.NetCondition(VehicleName, damage.Condition), this);
            }
        }

        private void OnPartDamaged(Vehicle vehicle, VehiclePart part, int damage)
        {
            ApplyPartDamage(part.PartId, damage, part.transform.position);
        }

        // 乗り物の体力を満たし直す (修理・出し直し) ときは、部位も直す。
        private void OnHealthReset(Health _)
        {
            Damage.Repair();
        }

        private void ReportBroken(AircraftPartDamageResult result, Vector3 position)
        {
            var condition = Damage.Condition;
            Debug.Log(AircraftLog.PartBroken(VehicleName, result.PartId, result.Effect, condition), this);
            AircraftEvents.RaisePartBroken(new AircraftPartBrokenInfo(gameObject, result.PartId, result.Effect, position, condition));
        }

        // 命中の途中で呼ばれるので、アセットの誤りで例外を投げると撃った側の処理まで止まる。誤りはログに出し、部位が飛び方に効かない機体として続ける。
        private AircraftDamageSpec BuildSpec()
        {
            if (_definition == null)
            {
                return AircraftDamageSpec.Default;
            }

            try
            {
                return _definition.ToDamageSpec();
            }
            catch (ArgumentException e)
            {
                Debug.LogError(AircraftLog.InvalidAircraftDamage(name, e.Message), this);
                return new AircraftDamageSpec(Array.Empty<AircraftDamagePartSpec>());
            }
        }
    }
}
