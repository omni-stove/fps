using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ガンゲームの武器の並びをアセットとして持つ。<see cref="ToLadder"/> で Core の <see cref="GunGameLadder"/> に変換する。
    /// 段の武器と近接の武器は <see cref="WeaponDefinition"/> で持ち、Id と名前はその <see cref="WeaponDefinition.ToSpec"/> から取る。
    /// 中身は Fps.Editor.GunGameSceneBuilder が作る。
    /// </summary>
    [CreateAssetMenu(fileName = "GunGameLadder", menuName = "Fps/Gun Game Ladder")]
    public sealed class GunGameLadderDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("0 段目からの武器の並び。今の段の武器で倒すと次の段へ進み、最後の段の武器で倒したら勝ち")]
        private WeaponDefinition[] _tiers = Array.Empty<WeaponDefinition>();

        [SerializeField, Tooltip("近接の武器 (ナイフなど)。これで倒された人は、下の設定がオンなら 1 段戻る")]
        private WeaponDefinition[] _meleeWeapons = Array.Empty<WeaponDefinition>();

        [SerializeField, Tooltip("近接の武器で倒された人を 1 段戻すか")]
        private bool _demotesOnMelee = true;

        /// <summary>0 段目からの武器の並び。</summary>
        public IReadOnlyList<WeaponDefinition> Tiers => _tiers ?? Array.Empty<WeaponDefinition>();

        /// <summary>近接の武器。</summary>
        public IReadOnlyList<WeaponDefinition> MeleeWeapons => _meleeWeapons ?? Array.Empty<WeaponDefinition>();

        public bool DemotesOnMelee
        {
            get => _demotesOnMelee;
            set => _demotesOnMelee = value;
        }

        /// <summary>段の武器をまとめて差し替える。null は空にする。</summary>
        public void SetTiers(IEnumerable<WeaponDefinition> tiers)
        {
            _tiers = tiers == null ? Array.Empty<WeaponDefinition>() : new List<WeaponDefinition>(tiers).ToArray();
        }

        /// <summary>近接の武器をまとめて差し替える。null は空にする。</summary>
        public void SetMeleeWeapons(IEnumerable<WeaponDefinition> weapons)
        {
            _meleeWeapons = weapons == null ? Array.Empty<WeaponDefinition>() : new List<WeaponDefinition>(weapons).ToArray();
        }

        /// <summary>定義を変換して <see cref="GunGameLadder"/> を作る。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="WeaponDefinitionException">段が無い、空の段がある、どれかの武器の定義が不正、近接の武器に空の要素があるとき。</exception>
        public GunGameLadder ToLadder()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed gun game ladder)" : name;
            var tiers = Tiers;
            if (tiers.Count == 0)
            {
                throw new WeaponDefinitionException($"gun game ladder '{source}' has no tier");
            }

            var converted = new GunGameTier[tiers.Count];
            for (var i = 0; i < converted.Length; i++)
            {
                var spec = ToSpec(source, "tier", i, tiers[i]);
                converted[i] = new GunGameTier(spec.Id, spec.DisplayName);
            }

            var melee = MeleeWeapons;
            var meleeIds = new string[melee.Count];
            for (var i = 0; i < meleeIds.Length; i++)
            {
                meleeIds[i] = ToSpec(source, "melee weapon", i, melee[i]).Id;
            }

            return new GunGameLadder(converted, meleeIds, _demotesOnMelee);
        }

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
