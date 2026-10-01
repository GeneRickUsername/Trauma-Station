using Content.Shared.Hands.EntitySystems;
using Content.Trauma.Common.Knowledge;
using Content.Trauma.Common.Knowledge.Components;

namespace Content.Trauma.Shared.Knowledge.Miscellanious.Systems;
public sealed partial class SpecialResultsSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    [SubscribeLocalEvent]
    private void OnSpecialResultsSum(Entity<KnowledgeHolderComponent> ent, ref SpecialResultsSumEvent args)
    {
        //SpecialResultsSum(args.Attacker, args.Defender, args.Rolls, args.DamageDealt);
    }
}
