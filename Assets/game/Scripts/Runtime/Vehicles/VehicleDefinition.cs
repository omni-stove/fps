using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 乗り物 1 種類の値 (Id・耐久・乗り込める距離・追うカメラの置き方) をアセットとして持つ。
    /// <see cref="Vehicle"/> に入れると、プレハブの Inspector の値の代わりにこれを使う。
    /// 座席の並びはシーンの <see cref="VehicleSeat"/> の側にあるので、<see cref="ToSpec"/> で座席の設定と合わせて <see cref="VehicleSpec"/> にする。
    /// 検証は <see cref="VehicleSpec"/> に任せ、失敗したらどのアセットの値かを添えて <see cref="VehicleDefinitionException"/> を投げる。
    /// </summary>
    [CreateAssetMenu(fileName = "Vehicle", menuName = "Fps/Vehicle Definition")]
    public sealed class VehicleDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("乗り物の Id。ゲームのデータがこの乗り物を指すのに使う")]
        private string _id = VehicleSpec.DefaultId;

        [SerializeField, Min(1), Tooltip("耐久")]
        private int _maxHealth = VehicleSpec.DefaultMaxHealth;

        [SerializeField, Min(0.01f), Tooltip("座席の乗る位置からこの距離 (m) までなら乗り込める")]
        private float _enterRange = VehicleSpec.DefaultEnterRange;

        [SerializeField, Min(0f), Tooltip("追うカメラを注視点からどれだけ離すか (m)")]
        private float _chaseDistance = VehicleSpec.DefaultChaseDistance;

        [SerializeField, Tooltip("追うカメラの注視点を、乗り物の原点からどれだけ上に置くか (m)")]
        private float _chaseHeight = VehicleSpec.DefaultChaseHeight;

        public string Id
        {
            get => _id;
            set => _id = value;
        }

        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        public float EnterRange
        {
            get => _enterRange;
            set => _enterRange = value;
        }

        public float ChaseDistance
        {
            get => _chaseDistance;
            set => _chaseDistance = value;
        }

        public float ChaseHeight
        {
            get => _chaseHeight;
            set => _chaseHeight = value;
        }

        /// <summary>この値と座席の並び seats で <see cref="VehicleSpec"/> を作る。値が不正なら <see cref="VehicleDefinitionException"/>。</summary>
        public VehicleSpec ToSpec(IReadOnlyList<VehicleSeatSpec> seats)
        {
            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            var source = string.IsNullOrEmpty(name) ? "(unnamed vehicle)" : name;
            try
            {
                return new VehicleSpec(_id, _maxHealth, seats, null, null, _enterRange, _chaseDistance, _chaseHeight);
            }
            catch (ArgumentException e)
            {
                throw new VehicleDefinitionException($"vehicle '{source}' is invalid: {e.Message}", e);
            }
        }

        /// <summary>spec の Id・耐久・乗り込める距離・追うカメラの値を写す (座席・武器・部位は持たないので写さない)。</summary>
        public void CopyFrom(VehicleSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _id = spec.Id;
            _maxHealth = spec.MaxHealth;
            _enterRange = spec.EnterRange;
            _chaseDistance = spec.ChaseDistance;
            _chaseHeight = spec.ChaseHeight;
        }
    }

    // ArgumentException から派生させるのは、VehicleSpec の不正な値を ArgumentException で受ける呼び出し側を変えずに済ませるため。
    public sealed class VehicleDefinitionException : ArgumentException
    {
        public VehicleDefinitionException(string message)
            : base(message)
        {
        }

        public VehicleDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
