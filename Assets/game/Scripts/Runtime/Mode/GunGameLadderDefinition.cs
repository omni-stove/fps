using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "GunGameLadder", menuName = "Shooter/Gun Game Ladder")]
    public sealed class GunGameLadderDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("0 段目からの武器の並び。今の段の武器で倒すと次の段へ進み、最後の段の武器で倒したら勝ち")]
        private WeaponDefinition[] _tiers = Array.Empty<WeaponDefinition>();

        [SerializeField, Tooltip("近接の武器 (ナイフなど)。これで倒された人は、下の設定がオンなら 1 段戻る")]
        private WeaponDefinition[] _meleeWeapons = Array.Empty<WeaponDefinition>();

        [SerializeField, Tooltip("近接の武器で倒された人を 1 段戻すか")]
        private bool _demotesOnMelee = true;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)。切れたら段のいちばん進んだ人の勝ち")]
        private float _timeLimitSeconds = GunGameRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = GunGameRunner.DefaultRespawnSeconds;

        public IReadOnlyList<WeaponDefinition> Tiers => _tiers ?? Array.Empty<WeaponDefinition>();

        public IReadOnlyList<WeaponDefinition> MeleeWeapons => _meleeWeapons ?? Array.Empty<WeaponDefinition>();

        public bool DemotesOnMelee
        {
            get => _demotesOnMelee;
            set => _demotesOnMelee = value;
        }

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public void SetTiers(IEnumerable<WeaponDefinition> tiers)
        {
            _tiers = tiers == null ? Array.Empty<WeaponDefinition>() : new List<WeaponDefinition>(tiers).ToArray();
        }

        public void SetMeleeWeapons(IEnumerable<WeaponDefinition> weapons)
        {
            _meleeWeapons = weapons == null ? Array.Empty<WeaponDefinition>() : new List<WeaponDefinition>(weapons).ToArray();
        }

        public GunGameLadder ToLadder()
        {
            var source = SourceName;
            var tiers = Tiers;
            if (tiers.Count == 0)
            {
                throw new WeaponDefinitionException($"gun game ladder '{source}' has no tier");
            }

            var converted = new GunGameTier[tiers.Count];
            for (var i = 0; i < converted.Length; i++)
            {
                var spec = ToSpec(source, "tier", i, tiers[i]);
                converted[i] = new GunGameTier(spec.Id, spec.NameKey);
            }

            var melee = MeleeWeapons;
            var meleeIds = new string[melee.Count];
            for (var i = 0; i < meleeIds.Length; i++)
            {
                meleeIds[i] = ToSpec(source, "melee weapon", i, melee[i]).Id;
            }

            return new GunGameLadder(converted, meleeIds, _demotesOnMelee);
        }

        public ModeSession CreateSession(GunGame gunGame)
        {
            if (gunGame == null)
            {
                throw new ArgumentNullException(nameof(gunGame));
            }

            try
            {
                return GunGameRunner.CreateSession(gunGame, _timeLimitSeconds, _respawnSeconds);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"gun game ladder '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed gun game ladder)" : name;

        private static WeaponSpec ToSpec(string source, string what, int index, WeaponDefinition definition)
        {
            // 破棄されたアセットや参照切れも Unity の == null で拾う。
            if (definition == null)
            {
                throw new WeaponDefinitionException($"gun game ladder '{source}' {what} #{index} is empty");
            }

            try
            {
                return definition.ToSpec();
            }
            catch (WeaponDefinitionException e)
            {
                throw new WeaponDefinitionException($"gun game ladder '{source}' {what} #{index}: {e.Message}", e);
            }
        }
    }
}
