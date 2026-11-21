using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    public enum WeaponPaint
    {
        Body,
        Furniture,
        Accent,
        Glass,
    }

    public enum WeaponPartShape
    {
        Box,
        Cylinder,
        Sphere,
    }

    public readonly struct WeaponPart
    {
        public WeaponPart(string name, WeaponPartShape shape, Vector3 center, Vector3 size, WeaponPaint paint, Vector3 euler = default)
        {
            Name = name;
            Shape = shape;
            Center = center;
            Size = size;
            Paint = paint;
            Euler = euler;
        }

        public string Name { get; }

        public WeaponPartShape Shape { get; }

        public Vector3 Center { get; }

        public Vector3 Size { get; }

        public WeaponPaint Paint { get; }

        public Vector3 Euler { get; }
    }

    public sealed class WeaponLook
    {
        public const string RotaryBarrelPrefix = "RotaryBarrel_";
        public const string ScopeName = "Scope";
        public const string LongScopeName = "LongScope";
        public const string DotSightName = "DotSight";
        public const string HoloSightName = "HoloSight";
        public const string HoloReticleId = "holo";
        public const string MagazineName = "Magazine";
        public const string TubeMagazineName = "TubeMagazine";
        public const string PumpName = "Pump";
        public const string AmmoBoxName = "AmmoBox";
        public const string StockName = "Stock";
        public const string CoolingRingPrefix = "CoolingRing_";
        public const string CoilPrefix = "Coil_";
        public const string EmitterName = "Emitter";
        public const string FuelTankName = "FuelTank";
        public const string NozzleName = "Nozzle";
        public const string BowLimbsName = "BowLimbs";
        public const string BowStringName = "BowString";
        public const string WarheadName = "Warhead";
        public const string SeekerName = "Seeker";
        public const string DrumName = "Drum";
        public const string MuzzleBrakeName = "MuzzleBrake";
        public const string BipodPrefix = "BipodLeg_";
        public const string BoltHandleName = "BoltHandle";
        public const string ForegripName = "Foregrip";
        public const string CompensatorName = "Compensator";
        public const string TubeName = "Tube";
        public const string BarrelName = "Barrel";
        public const string ReceiverName = "Receiver";
        public const string GripName = "Grip";
        public const string BandName = "Band";
        public const string ShaftName = "Shaft";
        public const string HookName = "Hook";
        public const string HeadName = "Head";
        public const string BladeName = "Blade";
        public const string ShieldPanelName = "ShieldPanel";
        public const string ViewportName = "Viewport";

        // 扇がこれ以上広い近接武器は払って斬る刃に、振りがこれ以上遅いものは重い頭の棍棒に見せる。どちらでもなければ細い金属の棒。
        public const float BladeMinArcDegrees = 100f;
        public const float ClubMinSwingSeconds = 0.6f;

        // 盾の幅と高さそのままで手元に出すと一人称の画面がほぼ塞がるので、形の比だけ残して縮める。
        public const float ShieldLookScale = 0.5f;

        public const float ScopeMaxFieldOfView = 40f;

        public const float LongScopeMaxFieldOfView = 20f;

        public const float SightPostHeight = ViewmodelAssetStep.SightPostHeight;

        public static readonly Color MetalColor = ViewmodelAssetStep.MetalColor;
        public static readonly Color EnergyBodyColor = new Color(0.78f, 0.8f, 0.84f);
        public static readonly Color LauncherBodyColor = new Color(0.32f, 0.36f, 0.22f);
        public static readonly Color PolymerColor = new Color(0.1f, 0.1f, 0.11f);
        public static readonly Color WoodColor = new Color(0.42f, 0.26f, 0.14f);
        public static readonly Color TanColor = new Color(0.62f, 0.53f, 0.36f);
        public static readonly Color OliveColor = new Color(0.28f, 0.32f, 0.2f);
        public static readonly Color GlassColor = new Color(0.25f, 0.55f, 0.85f);

        public static readonly Color BurnAccent = new Color(1f, 0.45f, 0.1f);
        public static readonly Color BeamAccent = new Color(0.95f, 0.15f, 0.15f);
        public static readonly Color HeatAccent = new Color(0.2f, 0.85f, 1f);
        public static readonly Color ChargeAccent = new Color(0.6f, 0.3f, 1f);
        public static readonly Color HomingAccent = new Color(0.3f, 0.9f, 0.35f);
        public static readonly Color ExplosiveAccent = new Color(0.9f, 0.75f, 0.15f);
        public static readonly Color RicochetAccent = new Color(1f, 0.85f, 0.45f);

        private readonly List<WeaponPart> _parts;

        private WeaponLook(
            List<WeaponPart> parts,
            Color body,
            Color furniture,
            Color? accent,
            Vector3 muzzle,
            Vector3 shellEject,
            Vector3 sight,
            Vector3 rearGrip,
            Vector3? foreGrip)
        {
            _parts = parts;
            BodyColor = body;
            FurnitureColor = furniture;
            AccentColor = accent;
            Muzzle = muzzle;
            ShellEject = shellEject;
            Sight = sight;
            RearGrip = rearGrip;
            ForeGrip = foreGrip;

            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            foreach (var part in parts)
            {
                var half = part.Size * 0.5f;
                min = Vector3.Min(min, part.Center - half);
                max = Vector3.Max(max, part.Center + half);
            }

            Bounds = new Bounds((min + max) * 0.5f, max - min);
        }

        public IReadOnlyList<WeaponPart> Parts => _parts;

        public Color BodyColor { get; }

        public Color FurnitureColor { get; }

        public Color? AccentColor { get; }

        public Vector3 Muzzle { get; }

        public Vector3 ShellEject { get; }

        public Vector3 Sight { get; }

        public Vector3 RearGrip { get; }

        public Vector3? ForeGrip { get; }

        public Bounds Bounds { get; }

        public bool Has(string partName)
        {
            return _parts.Any(part => string.Equals(part.Name, partName, StringComparison.Ordinal));
        }

        public bool HasPrefix(string prefix)
        {
            return _parts.Any(part => part.Name.StartsWith(prefix, StringComparison.Ordinal));
        }

        public string Signature()
        {
            var parts = _parts.Select(part => string.Format(
                CultureInfo.InvariantCulture, "{0}:{1}:{2:F3},{3:F3},{4:F3}", part.Name, ColorOf(part.Paint), part.Size.x, part.Size.y, part.Size.z));
            return string.Join("|", parts);
        }

        public Color ColorOf(WeaponPaint paint)
        {
            switch (paint)
            {
                case WeaponPaint.Body:
                    return BodyColor;
                case WeaponPaint.Furniture:
                    return FurnitureColor;
                case WeaponPaint.Accent:
                    return AccentColor ?? BodyColor;
                case WeaponPaint.Glass:
                    return GlassColor;
                default:
                    throw new ArgumentOutOfRangeException(nameof(paint), paint, "unknown paint");
            }
        }

        public static WeaponLook For(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (spec.IsShield)
            {
                return ShieldFor(spec.Shield);
            }

            if (spec.IsMelee)
            {
                return MeleeFor(spec.Melee);
            }

            var category = spec.Category;
            var shape = ViewmodelAssetStep.ShapeFor(category);
            var trigger = spec.Trigger;
            var round = spec.Round;
            var ammo = spec.Ammo;
            var impact = spec.Impact;

            var energy = ammo.Kind == AmmoKind.Heat || round.Delivery == RoundDelivery.Beam;
            var rotary = trigger.SpinUp != null;
            var bow = round.Delivery == RoundDelivery.Projectile && round.Projectile.OnWorldHit == WorldHitResponse.Stick;
            var drum = round.Delivery == RoundDelivery.Projectile && round.Projectile.OnWorldHit == WorldHitResponse.Bounce && ammo.MagazineSize > 1;
            var rocket = round.Delivery == RoundDelivery.Projectile && impact.Explosion != null && round.Projectile.OnWorldHit == WorldHitResponse.Stop;
            var tube = category == WeaponCategory.Launcher && !drum;
            var narrowest = spec.Aim.ZoomLevels.Min();
            var sightSpec = spec.Aim.Sight;
            var scoped = (narrowest <= ScopeMaxFieldOfView || sightSpec.ScopeOverlay) && !tube;
            var reflex = !scoped && !tube && sightSpec.HasReticle;
            var longScope = scoped && (narrowest <= LongScopeMaxFieldOfView || spec.Aim.ZoomLevels.Count > 1);

            var r = shape.ReceiverLength;
            var b = shape.BarrelLength * (round.Penetration != null ? 1.25f : 1f);
            var w = shape.Width;
            var h = shape.Height;
            var barrelY = tube ? 0f : h * 0.15f;
            var bore = round.PelletCount > 1 || category == WeaponCategory.Shotgun
                ? Mathf.Min(w, h) * 0.6f
                : Mathf.Max(0.018f, Mathf.Min(w, h) * 0.45f);
            var gripZ = r * 0.3f;
            var gripY = (-h * 0.5f) - (h * 0.55f);
            var top = h * 0.5f;

            var parts = new List<WeaponPart>();
            var muzzleZ = r + b;

            if (tube)
            {
                parts.Add(new WeaponPart(TubeName, WeaponPartShape.Cylinder, new Vector3(0f, 0f, (r + b) * 0.5f), new Vector3(h, h, r + b), WeaponPaint.Body));
            }
            else
            {
                parts.Add(new WeaponPart(ReceiverName, WeaponPartShape.Box, new Vector3(0f, 0f, r * 0.5f), new Vector3(w, h, r), WeaponPaint.Body));
            }

            parts.Add(new WeaponPart(GripName, WeaponPartShape.Box, new Vector3(0f, gripY, gripZ), new Vector3(w * 0.8f, h * 1.1f, w * 0.9f), WeaponPaint.Furniture));

            if (rotary)
            {
                var ring = bore * 0.9f;
                for (var i = 0; i < 6; i++)
                {
                    var angle = i * Mathf.PI / 3f;
                    var center = new Vector3(Mathf.Cos(angle) * ring, barrelY + (Mathf.Sin(angle) * ring), r + (b * 0.5f));
                    parts.Add(new WeaponPart(
                        RotaryBarrelPrefix + i.ToString(CultureInfo.InvariantCulture), WeaponPartShape.Cylinder, center,
                        new Vector3(bore * 0.55f, bore * 0.55f, b), WeaponPaint.Body));
                }

                var plate = (ring * 2f) + (bore * 0.8f);
                parts.Add(new WeaponPart("BarrelClamp", WeaponPartShape.Cylinder, new Vector3(0f, barrelY, r + (b * 0.8f)), new Vector3(plate, plate, 0.015f), WeaponPaint.Body));
                parts.Add(new WeaponPart("BarrelHousing", WeaponPartShape.Cylinder, new Vector3(0f, barrelY, r + 0.03f), new Vector3(plate, plate, 0.06f), WeaponPaint.Furniture));
                parts.Add(new WeaponPart(AmmoBoxName, WeaponPartShape.Box, new Vector3((-w * 0.5f) - 0.06f, -h * 0.2f, r * 0.5f), new Vector3(0.12f, 0.12f, 0.16f), WeaponPaint.Furniture));
            }
            else if (!tube)
            {
                parts.Add(new WeaponPart(BarrelName, WeaponPartShape.Cylinder, new Vector3(0f, barrelY, r + (b * 0.5f)), new Vector3(bore, bore, b), WeaponPaint.Body));
            }

            if (shape.TwoHanded && category != WeaponCategory.Launcher)
            {
                var stockLength = r * (category == WeaponCategory.SniperRifle ? 0.7f : 0.55f);
                parts.Add(new WeaponPart(StockName, WeaponPartShape.Box, new Vector3(0f, -h * 0.1f, -stockLength * 0.5f), new Vector3(w * 0.8f, h * 0.9f, stockLength), WeaponPaint.Furniture));
            }

            if (ammo.Kind == AmmoKind.Magazine && ammo.ReloadStyle == ReloadStyle.Whole && ammo.MagazineSize > 1 && !rotary && !drum && !bow
                && category != WeaponCategory.Handgun && category != WeaponCategory.Launcher)
            {
                var depth = Mathf.Clamp(0.04f + (ammo.MagazineSize * 0.004f), 0.05f, 0.18f);
                parts.Add(new WeaponPart(MagazineName, WeaponPartShape.Box, new Vector3(0f, (-h * 0.5f) - (depth * 0.5f), r * 0.62f), new Vector3(w * 0.7f, depth, 0.05f), WeaponPaint.Furniture));
            }

            if (ammo.ReloadStyle == ReloadStyle.PerRound)
            {
                var tubeY = barrelY - (bore * 1.05f);
                parts.Add(new WeaponPart(TubeMagazineName, WeaponPartShape.Cylinder, new Vector3(0f, tubeY, r + (b * 0.4f)), new Vector3(bore * 0.85f, bore * 0.85f, b * 0.8f), WeaponPaint.Body));
                parts.Add(new WeaponPart(PumpName, WeaponPartShape.Box, new Vector3(0f, tubeY, r + (b * 0.25f)), new Vector3(bore * 1.4f, bore * 1.4f, b * 0.3f), WeaponPaint.Furniture));
            }

            if (ammo.Kind == AmmoKind.Heat)
            {
                var size = bore * 2.6f;
                for (var i = 0; i < 4; i++)
                {
                    var z = r + (b * (0.15f + (i * 0.15f)));
                    parts.Add(new WeaponPart(
                        CoolingRingPrefix + i.ToString(CultureInfo.InvariantCulture), WeaponPartShape.Box, new Vector3(0f, barrelY, z),
                        new Vector3(size, size, 0.008f), WeaponPaint.Accent));
                }
            }

            if (trigger.Charge != null)
            {
                var size = bore * 3f;
                for (var i = 0; i < 3; i++)
                {
                    var z = r + (b * (0.2f + (i * 0.22f)));
                    parts.Add(new WeaponPart(
                        CoilPrefix + i.ToString(CultureInfo.InvariantCulture), WeaponPartShape.Cylinder, new Vector3(0f, barrelY, z),
                        new Vector3(size, size, 0.014f), WeaponPaint.Accent));
                }
            }

            if (round.Delivery == RoundDelivery.Beam)
            {
                var size = bore * 3f;
                parts.Add(new WeaponPart(EmitterName, WeaponPartShape.Cylinder, new Vector3(0f, barrelY, muzzleZ + 0.015f), new Vector3(size, size, 0.03f), WeaponPaint.Accent));
                parts.Add(new WeaponPart("Lens", WeaponPartShape.Cylinder, new Vector3(0f, barrelY, muzzleZ + 0.032f), new Vector3(size * 0.6f, size * 0.6f, 0.004f), WeaponPaint.Glass));
                muzzleZ += 0.034f;
            }

            if (round.Delivery == RoundDelivery.Spray)
            {
                parts.Add(new WeaponPart(FuelTankName, WeaponPartShape.Cylinder, new Vector3(0f, (-h * 0.5f) - 0.05f, r * 0.6f), new Vector3(0.09f, 0.09f, r * 0.8f), WeaponPaint.Accent));
                var nozzle = bore * 2.2f;
                parts.Add(new WeaponPart(NozzleName, WeaponPartShape.Cylinder, new Vector3(0f, barrelY, muzzleZ + 0.025f), new Vector3(nozzle, nozzle, 0.05f), WeaponPaint.Body));
                parts.Add(new WeaponPart("PilotLight", WeaponPartShape.Sphere, new Vector3(0f, barrelY - (nozzle * 0.7f), muzzleZ), Vector3.one * 0.02f, WeaponPaint.Accent));
                muzzleZ += 0.05f;
            }

            if (bow)
            {
                var front = muzzleZ - 0.03f;
                parts.Add(new WeaponPart(BowLimbsName, WeaponPartShape.Box, new Vector3(0f, barrelY, front), new Vector3(0.55f, 0.02f, 0.03f), WeaponPaint.Furniture));
                parts.Add(new WeaponPart(BowStringName, WeaponPartShape.Box, new Vector3(0f, barrelY + 0.012f, front - 0.12f), new Vector3(0.5f, 0.004f, 0.004f), WeaponPaint.Glass));
            }

            if (rocket)
            {
                var size = h * 0.9f;
                parts.Add(new WeaponPart(WarheadName, WeaponPartShape.Sphere, new Vector3(0f, barrelY, muzzleZ + (size * 0.6f)), new Vector3(size, size, size * 1.6f), WeaponPaint.Accent));
                muzzleZ += size * 1.4f;
            }

            if (round.Homing != null)
            {
                var seekerY = top + 0.03f;
                parts.Add(new WeaponPart(SeekerName, WeaponPartShape.Box, new Vector3(0f, seekerY, r * 0.4f), new Vector3(w * 0.8f, 0.06f, 0.14f), WeaponPaint.Body));
                parts.Add(new WeaponPart("SeekerLens", WeaponPartShape.Cylinder, new Vector3(0f, seekerY, (r * 0.4f) + 0.072f), new Vector3(0.045f, 0.045f, 0.004f), WeaponPaint.Glass));
            }

            if (drum)
            {
                parts.Add(new WeaponPart(DrumName, WeaponPartShape.Cylinder, new Vector3(0f, 0f, r * 0.55f), new Vector3(h * 1.9f, h * 1.9f, r * 0.5f), WeaponPaint.Body));
            }

            if (round.Penetration != null)
            {
                parts.Add(new WeaponPart(MuzzleBrakeName, WeaponPartShape.Box, new Vector3(0f, barrelY, muzzleZ + 0.03f), new Vector3(bore * 2.6f, bore * 1.6f, 0.06f), WeaponPaint.Body));
                muzzleZ += 0.06f;
                var legY = barrelY - 0.08f;
                parts.Add(new WeaponPart(BipodPrefix + "L", WeaponPartShape.Box, new Vector3(-0.03f, legY, r + (b * 0.6f)), new Vector3(0.012f, 0.16f, 0.012f), WeaponPaint.Furniture, new Vector3(0f, 0f, -20f)));
                parts.Add(new WeaponPart(BipodPrefix + "R", WeaponPartShape.Box, new Vector3(0.03f, legY, r + (b * 0.6f)), new Vector3(0.012f, 0.16f, 0.012f), WeaponPaint.Furniture, new Vector3(0f, 0f, 20f)));
            }

            if (trigger.Cycle != null)
            {
                var y = h * 0.2f;
                parts.Add(new WeaponPart(BoltHandleName, WeaponPartShape.Cylinder, new Vector3((w * 0.5f) + 0.025f, y, r * 0.3f), new Vector3(0.012f, 0.012f, 0.05f), WeaponPaint.Body, new Vector3(0f, 90f, 0f)));
                parts.Add(new WeaponPart("BoltKnob", WeaponPartShape.Sphere, new Vector3((w * 0.5f) + 0.05f, y, r * 0.3f), Vector3.one * 0.025f, WeaponPaint.Body));
            }

            Vector3? foreGrip = shape.TwoHanded ? new Vector3(0f, (-h * 0.5f) - 0.03f, r * 0.85f) : (Vector3?)null;
            if (trigger.Burst != null)
            {
                var gripCenter = new Vector3(0f, barrelY - (bore * 0.5f) - (h * 0.5f), r + (b * 0.3f));
                parts.Add(new WeaponPart(ForegripName, WeaponPartShape.Box, gripCenter, new Vector3(w * 0.6f, h, w * 0.7f), WeaponPaint.Furniture));
                foreGrip = gripCenter;
            }

            if (round.Ricochet != null)
            {
                parts.Add(new WeaponPart(CompensatorName, WeaponPartShape.Box, new Vector3(0f, barrelY, muzzleZ + 0.02f), new Vector3(w * 0.9f, h * 0.6f, 0.04f), WeaponPaint.Accent));
                muzzleZ += 0.04f;
            }

            var accent = AccentFor(spec);
            if (accent.HasValue && !tube)
            {
                parts.Add(new WeaponPart(BandName, WeaponPartShape.Box, new Vector3(0f, 0f, r * 0.85f), new Vector3(w * 1.06f, h * 1.06f, 0.02f), WeaponPaint.Accent));
            }
            else if (accent.HasValue)
            {
                parts.Add(new WeaponPart(BandName, WeaponPartShape.Cylinder, new Vector3(0f, 0f, (r + b) * 0.8f), new Vector3(h * 1.06f, h * 1.06f, 0.03f), WeaponPaint.Accent));
            }

            Vector3 sight;
            if (scoped)
            {
                var length = longScope ? 0.22f : 0.14f;
                var diameter = longScope ? 0.045f : 0.035f;
                var scopeY = top + 0.014f + (diameter * 0.5f);
                var scopeZ = r * 0.45f;
                parts.Add(new WeaponPart(longScope ? LongScopeName : ScopeName, WeaponPartShape.Cylinder, new Vector3(0f, scopeY, scopeZ), new Vector3(diameter, diameter, length), WeaponPaint.Body));
                parts.Add(new WeaponPart("ScopeLens", WeaponPartShape.Cylinder, new Vector3(0f, scopeY, scopeZ + (length * 0.5f) + 0.002f), new Vector3(diameter * 0.8f, diameter * 0.8f, 0.004f), WeaponPaint.Glass));
                parts.Add(new WeaponPart("ScopeMount_F", WeaponPartShape.Box, new Vector3(0f, top + 0.007f, scopeZ + (length * 0.3f)), new Vector3(0.012f, 0.014f, 0.012f), WeaponPaint.Body));
                parts.Add(new WeaponPart("ScopeMount_R", WeaponPartShape.Box, new Vector3(0f, top + 0.007f, scopeZ - (length * 0.3f)), new Vector3(0.012f, 0.014f, 0.012f), WeaponPaint.Body));
                sight = new Vector3(0f, scopeY, scopeZ - (length * 0.5f));
            }
            else if (reflex && string.Equals(sightSpec.ReticleId, HoloReticleId, StringComparison.Ordinal))
            {
                sight = AddHoloSight(parts, top, r * 0.4f);
            }
            else if (reflex)
            {
                sight = AddDotSight(parts, top, r * 0.4f);
            }
            else
            {
                // 照星は照門と同じ高さまで伸ばす。高さが違うと、構えたときに照準の線が銃身と平行にならず上下に傾いて見える。
                var rearZ = r * 0.08f;
                var frontZ = r + b - 0.01f;
                var notch = Mathf.Max(0.006f, w * 0.12f);
                parts.Add(new WeaponPart("RearSight_L", WeaponPartShape.Box, new Vector3(-notch, top + (SightPostHeight * 0.5f), rearZ), new Vector3(0.004f, SightPostHeight, 0.008f), WeaponPaint.Body));
                parts.Add(new WeaponPart("RearSight_R", WeaponPartShape.Box, new Vector3(notch, top + (SightPostHeight * 0.5f), rearZ), new Vector3(0.004f, SightPostHeight, 0.008f), WeaponPaint.Body));
                var frontBase = barrelY + (bore * 0.5f);
                var frontHeight = top + SightPostHeight - frontBase;
                parts.Add(new WeaponPart("FrontSight", WeaponPartShape.Box, new Vector3(0f, frontBase + (frontHeight * 0.5f), frontZ), new Vector3(0.004f, frontHeight, 0.006f), WeaponPaint.Body));
                sight = new Vector3(0f, top + SightPostHeight, rearZ);
            }

            return new WeaponLook(
                parts,
                energy ? EnergyBodyColor : category == WeaponCategory.Launcher ? LauncherBodyColor : MetalColor,
                FurnitureFor(category),
                accent,
                new Vector3(0f, barrelY, muzzleZ),
                new Vector3(w * 0.5f, h * 0.25f, r * 0.5f),
                sight,
                new Vector3(0f, gripY, gripZ),
                foreGrip);
        }

        // 近接武器の形を選ぶ欄は WeaponSpec に無いので、振りの値から形を決め、新しい近接武器の JSON にも見た目が付くようにする。
        public static WeaponLook MeleeFor(MeleeWeaponSpec melee)
        {
            if (melee == null)
            {
                throw new ArgumentNullException(nameof(melee));
            }

            var swing = melee.Swing;
            var length = Mathf.Clamp(0.3f + (swing.Reach * 0.15f), 0.45f, 0.8f);
            const float handle = 0.14f;
            const float handleWidth = 0.034f;
            var parts = new List<WeaponPart>
            {
                new WeaponPart(GripName, WeaponPartShape.Cylinder, new Vector3(0f, 0f, handle * 0.5f), new Vector3(handleWidth, handleWidth, handle), WeaponPaint.Furniture),
            };

            Color body;
            Color furniture;
            if (swing.ArcDegrees >= BladeMinArcDegrees)
            {
                var blade = length - handle;
                parts.Add(new WeaponPart(BladeName, WeaponPartShape.Box, new Vector3(0f, 0.012f, handle + (blade * 0.5f)), new Vector3(0.005f, 0.05f, blade), WeaponPaint.Body));
                parts.Add(new WeaponPart("Guard", WeaponPartShape.Box, new Vector3(0f, 0.005f, handle), new Vector3(0.02f, 0.07f, 0.012f), WeaponPaint.Furniture));
                body = MetalColor;
                furniture = PolymerColor;
            }
            else if (swing.SwingSeconds >= ClubMinSwingSeconds)
            {
                var head = length * 0.4f;
                parts.Add(new WeaponPart(ShaftName, WeaponPartShape.Cylinder, new Vector3(0f, 0f, (handle + length - head) * 0.5f), new Vector3(0.045f, 0.045f, length - head - handle), WeaponPaint.Body));
                parts.Add(new WeaponPart(HeadName, WeaponPartShape.Cylinder, new Vector3(0f, 0f, length - (head * 0.5f)), new Vector3(0.085f, 0.085f, head), WeaponPaint.Body));
                body = WoodColor;
                furniture = new Color(0.3f, 0.18f, 0.1f);
            }
            else
            {
                parts.Add(new WeaponPart(ShaftName, WeaponPartShape.Cylinder, new Vector3(0f, 0f, (handle + length) * 0.5f), new Vector3(0.022f, 0.022f, length - handle), WeaponPaint.Body));
                parts.Add(new WeaponPart(HookName, WeaponPartShape.Box, new Vector3(0f, 0.025f, length - 0.01f), new Vector3(0.02f, 0.06f, 0.02f), WeaponPaint.Body, new Vector3(-30f, 0f, 0f)));
                body = BeamAccent;
                furniture = PolymerColor;
            }

            return new WeaponLook(
                parts,
                body,
                furniture,
                null,
                new Vector3(0f, 0f, length),
                new Vector3(0f, 0f, length * 0.5f),
                new Vector3(0f, handleWidth, handle),
                new Vector3(0f, 0f, handle * 0.5f),
                null);
        }

        public static WeaponLook ShieldFor(RiotShieldSpec shield)
        {
            if (shield == null)
            {
                throw new ArgumentNullException(nameof(shield));
            }

            const float handle = 0.12f;
            const float handleWidth = 0.03f;
            const float thickness = 0.03f;
            var width = shield.Width * ShieldLookScale;
            var height = shield.Height * ShieldLookScale;
            var panelZ = handle + (thickness * 0.5f);
            var viewportY = height * 0.3f;
            var parts = new List<WeaponPart>
            {
                new WeaponPart(GripName, WeaponPartShape.Cylinder, new Vector3(0f, 0f, handle * 0.5f), new Vector3(handleWidth, handleWidth, handle), WeaponPaint.Furniture),
                new WeaponPart(ShieldPanelName, WeaponPartShape.Box, new Vector3(0f, 0f, panelZ), new Vector3(width, height, thickness), WeaponPaint.Body),
                new WeaponPart(ViewportName, WeaponPartShape.Box, new Vector3(0f, viewportY, panelZ), new Vector3(width * 0.6f, height * 0.12f, thickness * 1.1f), WeaponPaint.Glass),
            };

            return new WeaponLook(
                parts,
                OliveColor,
                PolymerColor,
                null,
                new Vector3(0f, 0f, panelZ + (thickness * 0.55f)),
                new Vector3(0f, 0f, panelZ),
                new Vector3(0f, viewportY, 0f),
                new Vector3(0f, 0f, handle * 0.5f),
                null);
        }

        // 構えている間も一人称の銃は隠さないので、不透明な材質しか無い今はレンズや蓋のある円柱で作らず、中を素通しにした枠にする。
        public static Vector3 AddDotSight(List<WeaponPart> parts, float top, float z)
        {
            return AddReflexSight(parts, DotSightName, top, z, 0.008f, 0.04f, 0.022f, 0.022f, 0.022f);
        }

        public static Vector3 AddHoloSight(List<WeaponPart> parts, float top, float z)
        {
            return AddReflexSight(parts, HoloSightName, top, z, 0.014f, 0.06f, 0.036f, 0.028f, 0.026f);
        }

        private static Vector3 AddReflexSight(
            List<WeaponPart> parts, string name, float top, float z, float baseHeight, float baseLength, float windowWidth, float windowHeight, float frameLength)
        {
            const float wall = 0.004f;
            var windowY = top + baseHeight + (windowHeight * 0.5f);
            var frameZ = z + ((baseLength - frameLength) * 0.5f);
            parts.Add(new WeaponPart(name, WeaponPartShape.Box, new Vector3(0f, top + (baseHeight * 0.5f), z), new Vector3(windowWidth + (wall * 2f), baseHeight, baseLength), WeaponPaint.Body));
            parts.Add(new WeaponPart(name + "Wall_L", WeaponPartShape.Box, new Vector3(-(windowWidth + wall) * 0.5f, windowY, frameZ), new Vector3(wall, windowHeight, frameLength), WeaponPaint.Body));
            parts.Add(new WeaponPart(name + "Wall_R", WeaponPartShape.Box, new Vector3((windowWidth + wall) * 0.5f, windowY, frameZ), new Vector3(wall, windowHeight, frameLength), WeaponPaint.Body));
            parts.Add(new WeaponPart(name + "Hood", WeaponPartShape.Box, new Vector3(0f, windowY + ((windowHeight + wall) * 0.5f), frameZ), new Vector3(windowWidth + (wall * 2f), wall, frameLength), WeaponPaint.Body));
            return new Vector3(0f, windowY, z - (baseLength * 0.5f));
        }

        // いくつも当てはまるときは、撃ったときに目立つ効果の色を先にする。
        public static Color? AccentFor(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (spec.Impact.StatusEffect != null)
            {
                return BurnAccent;
            }

            if (spec.Round.Delivery == RoundDelivery.Beam)
            {
                return BeamAccent;
            }

            if (spec.Ammo.Kind == AmmoKind.Heat)
            {
                return HeatAccent;
            }

            if (spec.Trigger.Charge != null)
            {
                return ChargeAccent;
            }

            if (spec.Round.Homing != null)
            {
                return HomingAccent;
            }

            if (spec.Impact.Explosion != null)
            {
                return ExplosiveAccent;
            }

            if (spec.Round.Ricochet != null)
            {
                return RicochetAccent;
            }

            return null;
        }

        public static Color FurnitureFor(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Shotgun:
                    return WoodColor;
                case WeaponCategory.SniperRifle:
                    return TanColor;
                case WeaponCategory.MarksmanRifle:
                case WeaponCategory.LightMachineGun:
                    return OliveColor;
                default:
                    return PolymerColor;
            }
        }

        // 当たり判定は付けない。手元のモデルは壁や弾の Ray に当たらず、置いた銃は拾う範囲の球だけで判定するため。
        public void Build(Transform parent, Func<WeaponPaint, Color, Material> paint, bool castShadows)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (paint == null)
            {
                throw new ArgumentNullException(nameof(paint));
            }

            BuildParts(parent, _parts, part => paint(part.Paint, ColorOf(part.Paint)), castShadows);
        }

        public static void BuildParts(Transform parent, IEnumerable<WeaponPart> parts, Func<WeaponPart, Material> materialOf, bool castShadows)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            if (materialOf == null)
            {
                throw new ArgumentNullException(nameof(materialOf));
            }

            foreach (var part in parts)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveOf(part.Shape));
                primitive.name = part.Name;
                Object.DestroyImmediate(primitive.GetComponent<Collider>());
                var transform = primitive.transform;
                transform.SetParent(parent, false);
                transform.localPosition = part.Center;
                transform.localRotation = Quaternion.Euler(part.Euler);

                // Unity の円柱は Y 軸に沿った直径 1・高さ 2 なので、X 軸で 90 度倒して前へ向け、長さは半分で割る。
                if (part.Shape == WeaponPartShape.Cylinder)
                {
                    transform.localRotation *= Quaternion.Euler(90f, 0f, 0f);
                    transform.localScale = new Vector3(part.Size.x, part.Size.z * 0.5f, part.Size.y);
                }
                else
                {
                    transform.localScale = part.Size;
                }

                var renderer = primitive.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = materialOf(part);
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        private static PrimitiveType PrimitiveOf(WeaponPartShape shape)
        {
            switch (shape)
            {
                case WeaponPartShape.Box:
                    return PrimitiveType.Cube;
                case WeaponPartShape.Cylinder:
                    return PrimitiveType.Cylinder;
                case WeaponPartShape.Sphere:
                    return PrimitiveType.Sphere;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown part shape");
            }
        }
    }
}
