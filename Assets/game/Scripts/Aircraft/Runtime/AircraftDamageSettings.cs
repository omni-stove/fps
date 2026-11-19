using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftDamageSettings
    {
        [SerializeField, Tooltip("飛び方に効く部位。Id は機体の VehiclePart の Id と合わせる")]
        private Part[] _parts = DefaultParts();

        [SerializeField, Range(0f, 1f), Tooltip("片方の翼が壊れ切ったときに、その側に残る揚力の倍率")]
        private float _minLift = AircraftDamageSpec.DefaultMinLift;

        [SerializeField, Range(0f, 1f), Tooltip("尾翼が壊れ切ったときに残る舵の効きの倍率")]
        private float _minControl = AircraftDamageSpec.DefaultMinControl;

        [SerializeField, Range(0f, 1f), Tooltip("エンジンが壊れ切ったときに残る推力の倍率")]
        private float _minThrust = AircraftDamageSpec.DefaultMinThrust;

        public Part[] Parts
        {
            get => _parts;
            set => _parts = value ?? Array.Empty<Part>();
        }

        public float MinLift
        {
            get => _minLift;
            set => _minLift = value;
        }

        public float MinControl
        {
            get => _minControl;
            set => _minControl = value;
        }

        public float MinThrust
        {
            get => _minThrust;
            set => _minThrust = value;
        }

        public AircraftDamageSpec ToSpec()
        {
            var parts = _parts ?? Array.Empty<Part>();
            var specs = new AircraftDamagePartSpec[parts.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                var part = parts[i] ?? throw new ArgumentException($"damage parts[{i}] is empty");
                specs[i] = new AircraftDamagePartSpec(part.Id, part.Effect, part.Durability);
            }

            return new AircraftDamageSpec(specs, _minLift, _minControl, _minThrust);
        }

        public void CopyFrom(AircraftDamageSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _parts = new Part[spec.Parts.Count];
            for (var i = 0; i < _parts.Length; i++)
            {
                var part = spec.Parts[i];
                _parts[i] = new Part(part.Id, part.Effect, part.Durability);
            }

            _minLift = spec.MinLift;
            _minControl = spec.MinControl;
            _minThrust = spec.MinThrust;
        }

        private static Part[] DefaultParts()
        {
            var defaults = AircraftDamageSpec.DefaultParts();
            var parts = new Part[defaults.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = new Part(defaults[i].Id, defaults[i].Effect, defaults[i].Durability);
            }

            return parts;
        }

        [Serializable]
        public sealed class Part
        {
            [SerializeField, Tooltip("部位の Id (VehiclePart の Id)。1 機の中で重ならない")]
            private string _id = string.Empty;

            [SerializeField, Tooltip("壊れると落ちる倍率 (左の揚力・右の揚力・舵・推力)")]
            private AircraftDamageEffect _effect = AircraftDamageEffect.LeftLift;

            [SerializeField, Min(1), Tooltip("部位の耐久。命中で受けたダメージ (部位の倍率を掛けた後) だけ減り、0 で壊れる")]
            private int _durability = AircraftDamageSpec.DefaultWingDurability;

            public Part()
            {
            }

            public Part(string id, AircraftDamageEffect effect, int durability)
            {
                _id = id;
                _effect = effect;
                _durability = durability;
            }

            public string Id => _id;

            public AircraftDamageEffect Effect => _effect;

            public int Durability => _durability;
        }
    }
}
