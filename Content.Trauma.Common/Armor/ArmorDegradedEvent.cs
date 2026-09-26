using Content.Shared.Damage.Prototypes;

namespace Content.Trauma.Common.Armor;

/// <summary>
/// Raised on the armor item when its flat protection degrades.
/// </summary>
[ByRefEvent]
public record struct ArmorDegradedEvent(EntityUid Armor, ProtoId<DamageTypePrototype> DamageType, float NewFlatReduction);
