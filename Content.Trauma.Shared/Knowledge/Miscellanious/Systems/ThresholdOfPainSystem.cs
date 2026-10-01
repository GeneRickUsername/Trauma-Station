using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew;
using Content.Trauma.Common.Knowledge;
using Content.Trauma.Common.Knowledge.Components;
using Content.Trauma.Shared.Knowledge.Systems;

namespace Content.Trauma.Shared.Knowledge.Miscellanious.Systems;

public sealed partial class ThresholdOfPainSystem : EntitySystem
{
    [Dependency] private SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    private static readonly EntProtoId ConstitutionAttribute = "ConstitutionAttribute";
    private static readonly EntProtoId StatusEffectPain = "StatusEffectPain";
    private static readonly EntProtoId StatusEffectKnockout = "StatusEffectKnockout";

    [SubscribeLocalEvent]
    private void OnDamage(Entity<KnowledgeHolderComponent> ent, ref DamageDealtEvent args)
    {
        _knowledge.OnDamageDealt(ent, ref args);

        var totalDamage = args.Damage.GetTotal();
        if (totalDamage <= 0)
            return;

        if (_knowledge.GetContainer(ent) is not { } brain || _knowledge.TryGetKnowledge<AttributeComponent>(brain, ConstitutionAttribute) is not { } consitution)
            return;

        if (totalDamage <= consitution.Comp.Attribute)
            return;

        var halfCon = consitution.Comp.Attribute.Int() / 2;
        var ev = new SingleContestEvent(20, 0, halfCon);
        RaiseLocalEvent(ent, ref ev);

        if (ev.CriticallySucceeded)
            _status.TryAddStatusEffectDuration(ent.Owner, StatusEffectKnockout, out _, TimeSpan.FromSeconds((ev.DiceUser - halfCon) * 5 * 60), null);
        else if (!ev.Failed)
            _status.TryAddStatusEffectDuration(ent.Owner, StatusEffectPain, out _, TimeSpan.FromSeconds((ev.DiceUser - halfCon) * 5), null);

    }
}
