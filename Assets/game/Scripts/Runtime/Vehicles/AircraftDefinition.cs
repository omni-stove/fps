using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Aircraft", menuName = "Shooter/Aircraft Definition")]
    public sealed class AircraftDefinition : ScriptableObject
    {
        [SerializeField, Min(0.01f), Tooltip("機体の質量 (kg)。Rigidbody に入れる。飛び方は変わらず、ぶつかったときの重さだけが変わる")]
        private float _mass = AircraftSpec.DefaultMass;

        [SerializeField, Min(0.01f), Tooltip("スロットルを開き切ったときの、前へ押す加速 (m/s²)")]
        private float _maxThrust = AircraftSpec.DefaultMaxThrust;

        [SerializeField, Tooltip("翼と舵が空気から受ける力の値")]
        private AircraftAeroSettings _aero = new AircraftAeroSettings();

        [SerializeField, Tooltip("視線の方へ機首を向ける当て方 (AircraftControlScheme) の値")]
        private AircraftAimSettings _aim = new AircraftAimSettings();

        [SerializeField, Tooltip("操縦桿で直接操る当て方 (AircraftStickControlScheme) の値")]
        private AircraftStickSettings _stick = new AircraftStickSettings();

        [SerializeField, Tooltip("地上を走るときの値")]
        private AircraftGroundSettings _ground = new AircraftGroundSettings();

        [SerializeField, Tooltip("壊れたときの飛び方の値")]
        private AircraftDamageSettings _damage = new AircraftDamageSettings();

        [SerializeField, Tooltip("舵面の動き・エンジン音・ベイパーの値 (AircraftEffects が読む)")]
        private AircraftEffectsSettings _effects = new AircraftEffectsSettings();

        [SerializeField, Tooltip("HUD に出す警告の値")]
        private AircraftWarningSettings _warning = new AircraftWarningSettings();

        public float Mass
        {
            get => _mass;
            set => _mass = value;
        }

        public float MaxThrust
        {
            get => _maxThrust;
            set => _maxThrust = value;
        }

        public AircraftAeroSettings Aero => _aero ??= new AircraftAeroSettings();

        public AircraftAimSettings Aim => _aim ??= new AircraftAimSettings();

        public AircraftStickSettings Stick => _stick ??= new AircraftStickSettings();

        public AircraftGroundSettings Ground => _ground ??= new AircraftGroundSettings();

        public AircraftDamageSettings Damage => _damage ??= new AircraftDamageSettings();

        public AircraftEffectsSettings Effects => _effects ??= new AircraftEffectsSettings();

        public AircraftWarningSettings Warning => _warning ??= new AircraftWarningSettings();

        public AircraftSpec ToSpec()
        {
            try
            {
                return new AircraftSpec(_mass, _maxThrust, Aero.ToSpec());
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public AircraftAimSpec ToAimSpec()
        {
            try
            {
                return Aim.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public AircraftStickSpec ToStickSpec()
        {
            try
            {
                return Stick.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public AircraftDamageSpec ToDamageSpec()
        {
            try
            {
                return Damage.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public AircraftEffectsSpec ToEffectsSpec()
        {
            try
            {
                return Effects.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public AircraftGroundSpec ToGroundSpec()
        {
            try
            {
                return Ground.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public AircraftWarningSpec ToWarningSpec()
        {
            try
            {
                return Warning.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public void CopyFrom(AircraftSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _mass = spec.Mass;
            _maxThrust = spec.MaxThrust;
            Aero.CopyFrom(spec.Aero);
        }

        // 値の誤りにアセットの名前を添えるのは、どの機体のアセットを直せばよいかを例外だけで分かるようにするため。
        private ArgumentException Invalid(ArgumentException inner)
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed aircraft)" : name;
            return new ArgumentException($"aircraft '{source}' is invalid: {inner.Message}", inner);
        }
    }
}
