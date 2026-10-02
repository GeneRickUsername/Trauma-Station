// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Trauma.Common.Knowledge;
using Content.Trauma.Common.Knowledge.Components;
using Content.Trauma.Shared.Knowledge.Talents.Components;

namespace Content.Trauma.Shared.Knowledge.Attribute.Attribute.Systems;

/// <summary>
/// This class handles all the talent relay events
/// </summary>
public sealed partial class TalentRelaySystem : EntitySystem
{
    private static readonly HashSet<ProtoId<DamageTypePrototype>> DamageTypesToughHide = new()
    {
        "Blunt",
        "Slash",
        "Piercing",
    };

    private static readonly HashSet<ProtoId<DamageTypePrototype>> DamageTypesPoison = new()
    {
        "Poison",
        "Caustic",
    };

    [SubscribeLocalEvent]
    private void OnCalculateDodge(Entity<DodgeComponent> ent, ref GetDodgeSavingThrowEvent args)
    {
        if (!TryComp<TalentComponent>(ent, out var talent))
            return;

        args.Mod += talent.Level;
    }

    [SubscribeLocalEvent]
    private void OnCalculateDefense(Entity<DefenseTalentComponent> ent, ref GetDefenseModifierEvent args)
    {
        if (!TryComp<TalentComponent>(ent, out var talent))
            return;

        args.Mod += talent.Level;
    }

    [SubscribeLocalEvent]
    private void OnCalculateAttack(Entity<AttackTalentComponent> ent, ref GetAttackModifierEvent args)
    {
        if (!TryComp<TalentComponent>(ent, out var talent))
            return;
        args.Mod += talent.Level;
    }

    [SubscribeLocalEvent]
    private void OnCalculateDamage(Entity<DamageTalentComponent> ent, ref GetDamageModifierEvent args)
    {
        if (!TryComp<TalentComponent>(ent, out var talent))
            return;

        args.Mod += talent.Level;
    }

    [SubscribeLocalEvent]
    private void OnCalculateSpeed(Entity<SpeedTalentComponent> ent, ref GetSpeedModifierEvent args)
    {
        if (!TryComp<TalentComponent>(ent, out var talent))
            return;

        args.Mod += talent.Level;
    }

    [SubscribeLocalEvent]
    private void OnCalculateHeal(Entity<FastHealerComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!TryComp<TalentComponent>(ent, out var talent))
            return;

        var heal = DamageSpecifier.GetNegative(args.Damage) * (talent.Level + 1);
        args.Damage.ExclusiveAdd(heal);
    }

    [SubscribeLocalEvent]
    private void OnCalculateResistToughHide(Entity<ToughHideComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!TryComp<TalentComponent>(ent, out var talent))
            return;

        var damageReduction = DamageSpecifier.GetPositive(args.Damage);
        foreach (var (key, amount) in damageReduction.DamageDict)
        {
            if (!DamageTypesToughHide.Contains(key))
                continue;

            damageReduction.DamageDict[key] = -FixedPoint2.Min(amount, FixedPoint2.New(talent.Level));
        }
        args.Damage.ExclusiveAdd(damageReduction);
    }

    [SubscribeLocalEvent]
    private void OnCalculateResistPoison(Entity<PoisonResistantComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!TryComp<TalentComponent>(ent, out var talent))
            return;

        var damageReduction = DamageSpecifier.GetPositive(args.Damage);
        foreach (var (key, amount) in damageReduction.DamageDict)
        {
            if (!DamageTypesPoison.Contains(key))
                continue;

            damageReduction.DamageDict[key] = -FixedPoint2.Min(amount, FixedPoint2.New(talent.Level));
        }
        args.Damage.ExclusiveAdd(damageReduction);
    }
}
