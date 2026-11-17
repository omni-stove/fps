using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "CoopInteractable", menuName = "Shooter/Coop Interactable")]
    public sealed class CoopInteractableDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("同時に押している必要のある人数")]
        private int _requiredCount = CoopHoldSpec.DefaultRequiredCount;

        [SerializeField, Min(0f), Tooltip("揃って押し続ける秒数")]
        private float _holdSeconds = CoopHoldSpec.DefaultHoldSeconds;

        [SerializeField, Tooltip("人数が欠けたときに経過を 0 に戻すか。外すと経過を残して止める")]
        private bool _resetOnInterrupt = CoopHoldSpec.DefaultResetOnInterrupt;

        [SerializeField, Tooltip("済んだら扉の板を動かす量 (板の親の座標)")]
        private Vector3 _openOffset = CoopDoor.DefaultOpenOffset;

        [SerializeField, Min(0f), Tooltip("扉が開き切るまでの秒数。0 ならすぐ開く")]
        private float _openSeconds = CoopDoor.DefaultOpenSeconds;

        public int RequiredCount
        {
            get => _requiredCount;
            set => _requiredCount = value;
        }

        public float HoldSeconds
        {
            get => _holdSeconds;
            set => _holdSeconds = value;
        }

        public bool ResetOnInterrupt
        {
            get => _resetOnInterrupt;
            set => _resetOnInterrupt = value;
        }

        public Vector3 OpenOffset
        {
            get => _openOffset;
            set => _openOffset = value;
        }

        public float OpenSeconds
        {
            get => _openSeconds;
            set => _openSeconds = value;
        }

        public CoopHoldSpec ToSpec()
        {
            try
            {
                return new CoopHoldSpec(_requiredCount, _holdSeconds, _resetOnInterrupt);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"coop interactable definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public void ValidateDoor()
        {
            if (!(_openSeconds >= 0f) || float.IsInfinity(_openSeconds))
            {
                throw new InvalidOperationException(
                    $"coop interactable definition '{SourceName}' open seconds {_openSeconds} must not be negative and must be finite");
            }

            if (!IsFinite(_openOffset.x) || !IsFinite(_openOffset.y) || !IsFinite(_openOffset.z))
            {
                throw new InvalidOperationException($"coop interactable definition '{SourceName}' open offset {_openOffset} must be finite");
            }
        }

        public void CopyFrom(CoopHoldSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _requiredCount = spec.RequiredCount;
            _holdSeconds = spec.HoldSeconds;
            _resetOnInterrupt = spec.ResetOnInterrupt;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed coop interactable definition)" : name;
    }
}
