using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 箱から出る物の表と持ち物庫の広さは、脱出のほかでも使えるよう別の JSON (loot.json・stash.json) に置き、ここには持たない。
    [CreateAssetMenu(fileName = "Extraction", menuName = "Shooter/Modes/Extraction")]
    public sealed class ExtractionDefinition : ScriptableObject
    {
        [SerializeField, Min(1f), Tooltip("出撃してから時間切れになるまでの秒数")]
        private float _raidSeconds = ExtractionSpec.DefaultRaidSeconds;

        [SerializeField, Min(0f), Tooltip("開いた脱出地点に立ち続けると脱出できる秒数")]
        private float _extractHoldSeconds = ExtractionSpec.DefaultExtractHoldSeconds;

        [SerializeField, Min(1), Tooltip("シーンの脱出地点のうち、毎回選んで開ける数。置いた数より多ければ全部を開ける")]
        private int _openPoints = ExtractionSpec.DefaultOpenPoints;

        [SerializeField, Min(0f), Tooltip("出撃してから脱出地点が開くまでの秒数")]
        private float _openAfterSeconds = ExtractionSpec.DefaultOpenAfterSeconds;

        [SerializeField, Tooltip("倒されても持っていた物を持ち物庫に入れるか")]
        private bool _keepCarriedOnDeath = ExtractionSpec.DefaultKeepCarriedOnDeath;

        public float RaidSeconds
        {
            get => _raidSeconds;
            set => _raidSeconds = value;
        }

        public float ExtractHoldSeconds
        {
            get => _extractHoldSeconds;
            set => _extractHoldSeconds = value;
        }

        public int OpenPoints
        {
            get => _openPoints;
            set => _openPoints = value;
        }

        public float OpenAfterSeconds
        {
            get => _openAfterSeconds;
            set => _openAfterSeconds = value;
        }

        public bool KeepCarriedOnDeath
        {
            get => _keepCarriedOnDeath;
            set => _keepCarriedOnDeath = value;
        }

        public ExtractionSpec ToSpec()
        {
            try
            {
                return new ExtractionSpec(_raidSeconds, _extractHoldSeconds, _openPoints, _openAfterSeconds, _keepCarriedOnDeath);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"extraction definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(ExtractionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _raidSeconds = spec.RaidSeconds;
            _extractHoldSeconds = spec.ExtractHoldSeconds;
            _openPoints = spec.OpenPoints;
            _openAfterSeconds = spec.OpenAfterSeconds;
            _keepCarriedOnDeath = spec.KeepCarriedOnDeath;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed extraction definition)" : name;
    }
}
