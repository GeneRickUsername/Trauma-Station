using Content.Shared.Chat;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusEffectNew.Components;
using Content.Trauma.Shared.Knowledge.Miscellanious.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Trauma.Shared.Knowledge.Miscellanious.Systems;

public sealed partial class ForcedSpeechSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ForcedSpeechStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out var ent, out var speech, out var effect))
        {
            if (_timing.CurTime <= speech.NextScreamTime || effect.AppliedTo is not { } applied)
                continue;
            speech.NextScreamTime = _timing.CurTime + TimeSpan.FromSeconds(speech.SpeakInterval);

            var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent));
            _chat.TrySendInGameICMessage(applied, random.Pick(speech.Sayings), InGameICChatType.Speak, false);
        }
    }
}
