// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Weapons;
using Content.Medical.Common.Targeting;
using Content.Shared.CombatMode;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Trauma.Common.Knowledge;
using Content.Trauma.Common.Knowledge.Components;
using Content.Trauma.Common.Weapons;
using Content.Trauma.Shared.Knowledge.Attribute.Attribute.Components;
using Content.Trauma.Shared.Knowledge.FightingStance;
using Content.Trauma.Shared.Knowledge.Miscellanious.Components;
using Content.Trauma.Shared.Knowledge.Systems;
using Content.Trauma.Shared.Parry;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Trauma.Shared.Knowledge.Miscellanious.Systems;

public sealed partial class CombatSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityQuery<CombatModeComponent> _combatQuery = default!;
    [Dependency] private EntityQuery<MeleeWeaponComponent> _weaponQuery = default!;

    private TimeSpan _lastTick = TimeSpan.Zero;

    private static readonly SoundSpecifier ParrySound = new SoundPathSpecifier("/Audio/_Goobstation/Heretic/parry.ogg", AudioParams.Default.WithVariation(0.05f));
    private static readonly EntProtoId DodgeTalent = "DodgeTalent";

    [SubscribeLocalEvent]
    private void ResolveAttack(Entity<MobStateComponent> ent, ref BeforeHarmfulActionEvent args)
    {
        if (args.Type == HarmfulActionType.Harm)
        {
            ResolveDodgeAttack(args.User, args.Target, args.Used ?? args.User, args.Type, args.Damage, out var cancelled);

            args.Cancelled = cancelled;
            return;
        }

        // Standard parry/block pipeline for Disarm / Grab
        ResolveAttack(args.User, args.Target, args.Used ?? args.User, args.Type, args.Damage, out var standardCancelled);
        args.Cancelled = standardCancelled;
    }

    [SubscribeLocalEvent]
    private void TryDodgeProjectile(Entity<KnowledgeHolderComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (TryDodge(ent, args.ProjUid))
            args.Cancelled = true;

        _knowledge.RelayMartialArt(ent, ref args);
    }

    [SubscribeLocalEvent]
    private void TryDodgeHitscan(Entity<KnowledgeHolderComponent> ent, ref HitScanReflectAttemptEvent args)
    {
        if (TryDodge(ent, args.SourceItem))
            args.Reflected = true;
    }

    [SubscribeLocalEvent]
    private void CalculateDefenseDice(Entity<KnowledgeHolderComponent> ent, ref GetDefenseDice args)
    {
        if (!_mobState.IsAlive(ent))
            return;

        if (!TryComp<FightingStanceComponent>(ent, out var fighting))
        {
            args.Dice = 12;
            return;
        }

        args.Dice = fighting.DefenseDice;
    }

    [SubscribeLocalEvent]
    private void Resolve(Entity<KnowledgeHolderComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        _knowledge.RelayActiveEvent(ent, ref args);

        var evSpeedMod = new GetSpeedModifierEvent();
        RaiseLocalEvent(ent.Owner, ref evSpeedMod);
        var minSpeed = 3;
        if (TryComp<ItemComponent>(args.Weapon, out var itemComp))
            minSpeed = GetMinimumSpeedSize(itemComp.Size);

        var speed = 0.1f / args.Rate;
        speed = 1 / MathF.Max(speed - evSpeedMod.Mod, minSpeed); // You can't be faster than the weapon sizes, it's a minimum weapon speed.
        args.Rate = speed;
    }

    /// <summary>
    /// Gets minimum weapon speed based on weapon size.
    /// </summary>
    private static int GetMinimumSpeedSize(ProtoId<ItemSizePrototype> size) => size.Id switch
    {
        "Large" => 4,
        "Normal" => 3,
        "Small" => 2,
        _ => 2
    };


    private void ResolveDodgeAttack(EntityUid attacker, EntityUid defender, EntityUid weapon, HarmfulActionType attackType, DamageSpecifier? damage, out bool cancelled)
    {
        cancelled = false;

        if (_mobState.IsIncapacitated(defender) || !HasComp<MobStateComponent>(defender) || attacker == defender || !TryComp<MeleeWeaponComponent>(weapon, out var weaponComp))
            return;

        var rangeEv = new GetLightAttackRangeEvent(defender, attacker, weaponComp.Range);
        RaiseLocalEvent(weapon, ref rangeEv);
        var range = rangeEv.Cancel ? weaponComp.Range : rangeEv.Range;

        if (!_melee.InRange(attacker, defender, range, null, out _))
            return;

        // 3. Roll Attack vs. Dodge Modifiers
        var evAttackMod = new GetAttackModifierEvent();
        RaiseLocalEvent(attacker, ref evAttackMod);

        var evDodgeMod = new GetDodgeSavingThrowEvent();
        RaiseLocalEvent(defender, ref evDodgeMod);

        var evDefenseDice = new GetDefenseDice(8);
        RaiseLocalEvent(defender, ref evDefenseDice);

        var evOpposedContest = new OpposedContestEvent(defender, 20, evAttackMod.Mod, evDefenseDice.Dice, evDodgeMod.Mod);
        RaiseLocalEvent(attacker, ref evOpposedContest);

        if (evOpposedContest.Failed)
        {
            cancelled = true;

            _popup.PopupEntity("The enemy dodged your attack!", attacker, attacker, PopupType.Small);
            _popup.PopupEntity("You narrowly dodge the incoming attack!", defender, defender, PopupType.Small);

            if (evOpposedContest.CriticallyFailedUser)
            {
                var difference = evOpposedContest.DiceOpposed + evOpposedContest.ModOpposed - evOpposedContest.DiceUser - evOpposedContest.ModUser;
                var fumbleEv = new OnFumbleEvent(difference);
                RaiseLocalEvent(attacker, ref fumbleEv);
            }

            return;
        }

        if (evOpposedContest.CriticallyFailedOpposed)
            _popup.PopupEntity("The enemy clumsily failed to dodge your strike!", attacker, attacker, PopupType.Small);

        if (evOpposedContest.CriticallySucceededUser)
        {
            var ev = new CriticalHitEvent(attacker, damage ?? new());
            RaiseLocalEvent(defender, ref ev);
            _popup.PopupEntity("Critical Strike!", attacker, attacker, PopupType.Small);
        }
    }

    private void ResolveAttack(EntityUid attacker, EntityUid defender, EntityUid weapon, HarmfulActionType attackType, DamageSpecifier? damage, out bool cancelled)
    {
        cancelled = false;
        if (_mobState.IsIncapacitated(defender) || !HasComp<MobStateComponent>(defender) || attacker == defender || !TryComp<MeleeWeaponComponent>(weapon, out var weaponComp)) // ever seen a corpse parry? Can't say I have.
            return;

        var evAttackMod = new GetAttackModifierEvent();
        RaiseLocalEvent(attacker, ref evAttackMod);

        var evDefenseMod = new GetDefenseModifierEvent();
        RaiseLocalEvent(defender, ref evDefenseMod);

        var evDefenseDice = new GetDefenseDice(8);
        RaiseLocalEvent(defender, ref evDefenseDice);

        var evOpposedContest = new OpposedContestEvent(defender, 20, evAttackMod.Mod, evDefenseDice.Dice, evDefenseMod.Mod);
        RaiseLocalEvent(attacker, ref evOpposedContest);

        // Makes it harder to defend after being hit, the beatdown is gonna be brutal if you're surrounded.
        if (!TryComp<DefenseTierdownComponent>(defender, out var defenseDown))
            defenseDown = AddComp<DefenseTierdownComponent>(defender);
        defenseDown.Mod += 1.0f;
        Dirty(defender, defenseDown);

        if (evOpposedContest.CriticallyFailedUser && evOpposedContest.CriticallyFailedOpposed)
        {
            cancelled = true;
            _popup.PopupEntity("You try to strike the enemy, but end up not doing much of anything.", attacker, attacker, PopupType.Small);
            _popup.PopupEntity("You stumble around like a bummbling fool, not doing anything effect.", defender, defender, PopupType.Small);
        }

        if (evOpposedContest.Failed)
        {
            if (evOpposedContest.CriticallySucceededUser)
                return; // If you crit but can't strike the opponent, then what are you fighting?

            var difference = evOpposedContest.DiceOpposed + evOpposedContest.ModOpposed - evOpposedContest.DiceUser - evOpposedContest.ModUser;

            if (evOpposedContest.CriticallyFailedUser)
            {
                var fumbleEv = new OnFumbleEvent(difference);
                RaiseLocalEvent(attacker, ref fumbleEv);
            }

            var parrySound = ParrySound;
            if (TryComp<ParryComponent>(weapon, out var parryComp))
                parrySound = parryComp.SoundOnParry;
            _audio.PlayLocal(parrySound, defender, _player.LocalEntity);
            if (evOpposedContest.ModOpposed >= 19)
            {
                var queued = AddComp<QueuedStrikeComponent>(defender); // Defender gets a free strike.
                queued.TimeToHit = _timing.CurTime + TimeSpan.FromSeconds(1); // Hit next second.
                queued.Target = attacker;
                queued.Offhand = !evOpposedContest.CriticallySucceededOpposed;
                Dirty(defender, queued);
                // TODO: Replace with sound effects to not flood up chat.
                _popup.PopupEntity("You've shown an opening!", attacker, attacker, PopupType.Small);
                _popup.PopupEntity("The opponent has shown an opening, prepare for an attack!", defender, defender, PopupType.Small);
            }
            else
            {
                _popup.PopupEntity("You've been parried.", attacker, attacker, PopupType.Small);
                _popup.PopupEntity("You've successfully defended against an opponent.", defender, defender, PopupType.Small);
            }

            if (difference < 10)
            {
                var hands = _hands.EnumerateHands(defender);
                var handCount = 0;
                foreach (var hand in hands)
                {
                    if (difference > 5 && handCount == 0)
                    {
                        handCount++;
                        continue;
                    }

                    if (_hands.TryGetHeldItem(defender, hand, out var item))
                        defender = item.Value;
                    else
                        return; // TODO: Replace with hand targeting.
                    return;
                }
            }
            else
                cancelled = true;
            return;
        }

        if (evOpposedContest.CriticallyFailedOpposed)
        {
            _popup.PopupEntity("You missed, but it could have been worse.", attacker, attacker, PopupType.Small);
            cancelled = true;
            return;
        }

        if (evOpposedContest.CriticallySucceededUser)
        {
            var ev = new CriticalHitEvent(attacker, damage ?? new());
            RaiseLocalEvent(defender, ref ev);
            _popup.PopupEntity("Good strike!", attacker, attacker, PopupType.Small);
        }
    }

    private bool TryDodge(Entity<KnowledgeHolderComponent> ent, EntityUid projectile)
    {
        if (_mobState.IsIncapacitated(ent.Owner) || !HasComp<MobStateComponent>(ent.Owner)) // ever seen a corpse parry? Can't say I have.
            return false;

        int defense = 0;
        if (_knowledge.GetContainer(ent.Owner) is { } brain && _knowledge.GetTalent(brain, DodgeTalent) is { } talent)
        {
            var defenseEv = new GetDefenseModifierEvent();
            RaiseLocalEvent(ent, ref defenseEv);
            defense += defenseEv.Mod;
        }

        // TODO: Replace with gun attack thing.
        var ev = new SingleContestEvent(20, defense, 20);
        RaiseLocalEvent(ent, ref ev);
        return !ev.Failed;
    }
}
