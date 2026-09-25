// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Weapons.Melee.Events;
using Content.Trauma.Common.Knowledge;
using Content.Trauma.Common.Knowledge.Components;
using Content.Trauma.Shared.Knowledge.Systems;

namespace Content.Trauma.Shared.Knowledge.Attribute.Attribute.Systems;

/// <summary>
/// Handles damage related stuff.
/// </summary>
public sealed partial class MeleeAttributeSystem : EntitySystem
{
    [Dependency] private SharedKnowledgeSystem _knowledge = default!;

    private static readonly HashSet<ProtoId<DamageTypePrototype>> DamageTypes = new()
    {
        "Blunt",
        "Slash",
        "Piercing",
        "Structural"
    };

    [SubscribeLocalEvent]
    private void OnDamageGet(Entity<KnowledgeHolderComponent> ent, ref GetUserMeleeDamageEvent args)
    {
        var selfEv = new GetDamageModifierEvent();

        RaiseLocalEvent(ent.Owner, ref selfEv);

        var damage = new DamageModifierSet();

        foreach (var (key, _) in args.Damage.DamageDict)
        {
            if (!DamageTypes.Contains(key))
                continue;

            damage.FlatReductions.Add(key, -selfEv.Mod); // Negative for more damage.
        }
        args.Modifiers.Add(damage);

        _knowledge.RelayActiveEvent(ent, ref selfEv);
    }
}
