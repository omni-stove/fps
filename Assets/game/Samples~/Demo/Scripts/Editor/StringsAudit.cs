using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Runtime;

namespace Shooter.Editor
{
    public sealed class StringsAudit : IReplacementAuditCheck
    {
        public void Check(ICollection<ReplacementAuditFinding> findings)
        {
            var strings = AuditedStrings.Read(GameLanguages.All, Localizer.ReadResources, findings);
            CheckStringKeys(strings, findings);
            CheckWeaponNames(ReplacementAudit.LoadAssets<WeaponDefinition>(), strings, findings);
        }

        public static void CheckStringKeys(AuditedStrings strings, ICollection<ReplacementAuditFinding> findings)
        {
            if (strings == null)
            {
                throw new ArgumentNullException(nameof(strings));
            }

            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            var languages = strings.ReadableLanguages;
            var allKeys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var language in languages)
            {
                allKeys.UnionWith(strings.KeysOf(language));
            }

            foreach (var key in allKeys)
            {
                foreach (var language in languages)
                {
                    if (strings.TryGetTable(language, key, out _))
                    {
                        continue;
                    }

                    var holder = languages.First(other => strings.TryGetTable(other, key, out _));
                    strings.TryGetTable(holder, key, out var table);
                    findings.Add(new ReplacementAuditFinding(
                        ReplacementAuditArea.Strings,
                        GameLanguages.Code(language),
                        $"key '{key}' is missing ({GameLanguages.Code(holder)} has it in '{table}')"));
                }
            }
        }

        public static void CheckWeaponNames(
            IEnumerable<WeaponDefinition> weapons,
            AuditedStrings strings,
            ICollection<ReplacementAuditFinding> findings)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (strings == null)
            {
                throw new ArgumentNullException(nameof(strings));
            }

            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            foreach (var weapon in weapons)
            {
                if (weapon == null)
                {
                    continue;
                }

                var nameKey = weapon.NameKey.Trim();
                // キーの無い武器は ReplacementAudit.CheckWeapons が出す
                if (nameKey.Length == 0)
                {
                    continue;
                }

                foreach (var language in strings.ReadableLanguages)
                {
                    if (!strings.TryGetTable(language, nameKey, out _))
                    {
                        findings.Add(new ReplacementAuditFinding(
                            ReplacementAuditArea.Strings,
                            ReplacementAudit.WhereIs(weapon),
                            $"name key '{nameKey}' is missing from the {GameLanguages.Code(language)} strings"));
                    }
                }
            }
        }
    }
}
