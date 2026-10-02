using Content.Shared.Damage;
using Content.Trauma.Common.Armor;

namespace Content.Shared.Armor;
public abstract partial class SharedArmorSystem
{

    /// <summary>
    /// Degrades flat armor reductions permanently if damage dealt exceeds the current flat protection value.
    /// </summary>
    private void ApplyArmorDegradation(EntityUid uid, ArmorComponent armor, DamageSpecifier damage)
    {
        EnsureComp<ArmorDegradationComponent>(uid, out var degradation);

        var modified = false;
        var flatReductions = armor.Modifiers.FlatReductions;

        foreach (var (damageType, amount) in damage.DamageDict)
        {
            if (amount <= 0)
                continue;

            // Check if damage type has flat armor reduction defined
            if (!flatReductions.TryGetValue(damageType, out var currentFlat) || currentFlat <= degradation.MinimumFlatReduction)
                continue;

            // Degrade if raw damage exceeds the armor's flat reduction
            if (amount > currentFlat)
            {
                var newFlat = MathF.Max(degradation.MinimumFlatReduction, (float) currentFlat - degradation.DegradationStep);
                flatReductions[damageType] = newFlat;
                modified = true;

                var ev = new ArmorDegradedEvent(uid, damageType, newFlat);
                RaiseLocalEvent(uid, ref ev);
            }
        }

        if (modified)
            Dirty(uid, armor);
    }

}
