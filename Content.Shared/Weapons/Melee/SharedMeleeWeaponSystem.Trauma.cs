// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Common.Weapons;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;
using Content.Trauma.Common.Knowledge;
using Content.Trauma.Common.Knowledge.Components;
using Content.Trauma.Common.Knowledge.Systems;
using Content.Trauma.Common.Weapons;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Melee;

/// <summary>
/// Trauma - extra stuff for melee system
/// </summary>
public abstract partial class SharedMeleeWeaponSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private CommonKnowledgeSystem _knowledge = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private EntityQuery<InteractionRelayComponent> _relayQuery;

    public static readonly ProtoId<TagPrototype> WideSwingIgnore = "WideSwingIgnore"; // for mice
    public static readonly EntProtoId MeleeKnowledge = "MeleeKnowledge";

    private float _shoveRange;
    private float _shoveSpeed;

    private void InitializeTrauma()
    {
        _relayQuery = GetEntityQuery<InteractionRelayComponent>();

        Subs.CVar(_cfg, GoobCVars.ShoveRange, x => _shoveRange = x, true);
        Subs.CVar(_cfg, GoobCVars.ShoveSpeed, x => _shoveSpeed = x, true);
    }

    public bool AttemptHeavyAttack(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon, List<EntityUid> targets, EntityCoordinates coordinates)
        => AttemptAttack(user,
            weaponUid,
            weapon,
            new HeavyAttackEvent(GetNetEntity(weaponUid), GetNetEntityList(targets), GetNetCoordinates(coordinates)),
            null);

    private float CalculateShoveStaminaDamage(EntityUid disarmer, EntityUid disarmed)
        => TryComp<ShovingComponent>(disarmer, out var shoving) ? shoving.StaminaDamage : ShovingComponent.DefaultStaminaDamage;

    private void PhysicalShove(EntityUid user, EntityUid target)
    {
        var userPos = TransformSystem.ToMapCoordinates(user.ToCoordinates()).Position;
        var targetPos = TransformSystem.ToMapCoordinates(target.ToCoordinates()).Position;
        if (userPos == targetPos)
            return; // no NaN

        var pushVector = (targetPos - userPos).Normalized() * _shoveRange;

        var animated = HasComp<ItemComponent>(target);

        _throwing.TryThrow(target, pushVector, _shoveRange * _shoveSpeed, animated: animated);
    }

    private void AdjustStaminaDamage(EntityUid user, ref float staminaDamage)
    {
        // TODO: use event for this bruh
        if (_knowledge.GetSkill(user, MeleeKnowledge) is { } melee)
        {
            staminaDamage *= 1 - _knowledge.SharpCurve(melee);
        }
    }

    private void AddExtraDamageExamine(MeleeWeaponComponent component, DamageSpecifier damageSpec, FormattedMessage message)
    {
        var ap = component.ResistanceBypass ? 100 : (int) Math.Round(damageSpec.ArmorPenetration * 100);
        var clickMult = (int) Math.Round((component.ClickPartDamageMultiplier * component.ClickDamageModifier.Float() - 1f) * 100);
        var heavyMult = (int) Math.Round((component.HeavyPartDamageMultiplier - 1f) * 100);

        ModifyMessage(message, "armor-penetration", ap);
        ModifyMessage(message, "click-damage-modifier", clickMult);
        ModifyMessage(message, "heavy-damage-modifier", heavyMult);
    }

    private void ModifyMessage(FormattedMessage message, LocId loc, int value)
    {
        if (value == 0)
            return;
        var abs = Math.Abs(value);
        message.AddMarkupPermissive("\n" + Loc.GetString(loc, ("arg", value / abs), ("abs", abs)));
    }

    protected bool RaiseInRangeEvent(EntityUid ent,
        EntityUid target,
        float range,
        EntityCoordinates? targetCoordinates,
        Angle? targetAngle,
        out bool inRange,
        out EntityUid source)
    {
        var ev = new MeleeInRangeEvent(ent, target, range, targetCoordinates, targetAngle);
        RaiseLocalEvent(ent, ref ev);
        inRange = ev.InRange;
        source = ev.User;
        return ev.Handled;
    }

    /// <summary>
    /// Queues a player's attack if it is queueable.
    /// </summary>
    public void QueuePlayerAttack(
    EntityUid user,
    List<EntityUid> targets,
    EntityUid weaponUid,
    MeleeWeaponComponent weapon,
    HarmfulActionType type,
    AttackEvent? rawEvent)
    {
        if (!TryComp<KnowledgeHolderComponent>(user, out var queue))
        {
            ExecuteQueuedAttack(user, targets, weaponUid, weapon, type, rawEvent);
            return;
        }

        if (queue.NextAttackType == type && queue.Weapon == weaponUid && queue.QueuedTargets.SequenceEqual(targets))
            return; // Misclick, no reason to do anything.

        if (queue.NextAttackType != type || queue.Weapon != weaponUid)
        {
            queue.NextAttackType = type;
            queue.QueuedTargets = targets;
            queue.Weapon = weaponUid;
            queue.ScheduledExecutionTime += TimeSpan.FromSeconds(1); // You think about it, you lose.
            Dirty(user, queue);
            return;
        }

        var curTime = _timing.CurTime;

        // Penalty when changing targets/actions
        if (curTime < queue.ScheduledExecutionTime)
        {
            queue.NextAttackType = type;
            queue.QueuedTargets = targets;
            queue.Weapon = weaponUid;
            queue.ScheduledExecutionTime += TimeSpan.FromSeconds(1); // Add 1 second penalty

            Dirty(user, queue);
            return;
        }

        ExecuteQueuedAttack(user, targets, weaponUid, weapon, type, rawEvent);

        var evSpeedMod = new GetSpeedModifierEvent();
        RaiseLocalEvent(user, ref evSpeedMod);
        var minSpeed = 3;
        if (TryComp<ItemComponent>(weaponUid, out var itemComp))
            minSpeed = GetMinimumSpeedSize(itemComp.Size);

        var speed = 1 / weapon.AttackRate;
        speed = MathF.Max(speed - evSpeedMod.Mod, minSpeed);

        queue.NextAttackType = type;
        queue.QueuedTargets = targets;
        queue.Weapon = weaponUid;
        queue.ScheduledExecutionTime = _timing.CurTime + TimeSpan.FromSeconds(speed);

        Dirty(user, queue);
    }

    /// <summary>
    /// Executes queued attack.
    /// </summary>
    public void ExecuteQueuedAttack(
    EntityUid user,
    List<EntityUid> targets,
    EntityUid weaponUid,
    MeleeWeaponComponent weapon,
    HarmfulActionType attackType,
    AttackEvent? rawEvent = null)
    {
        if (targets.Count == 0)
            return;

        var primaryTarget = targets[0];
        if (!Exists(primaryTarget))
            return;

        switch (attackType)
        {
            case HarmfulActionType.Harm:
                AttemptLightAttack(user, weaponUid, weapon, primaryTarget);
                break;

            case HarmfulActionType.Heavy:
                // Convert list back to NetEntity list and reconstruct HeavyAttackEvent
                var netTargets = GetNetEntityList(targets);
                var netWeapon = GetNetEntity(weaponUid);
                var coordinates = GetNetCoordinates(Transform(user).Coordinates);

                if (rawEvent is HeavyAttackEvent heavyAttack)
                    AttemptAttack(user, weaponUid, weapon, heavyAttack, null);
                break;

            case HarmfulActionType.Disarm:
                AttemptDisarmAttack(user, weaponUid, weapon, primaryTarget);
                break;
        }
    }


    /// <summary>
    /// Gets minimum weapon speed based on weapon size
    /// </summary>
    private static int GetMinimumSpeedSize(ProtoId<ItemSizePrototype> size) => size.Id switch
    {
        "Large" => 4,
        "Normal" => 3,
        "Small" => 2,
        _ => 2
    };
}
