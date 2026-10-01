using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Trauma.Shared.Knowledge.Miscellanious.Components;

[RegisterComponent]
public sealed partial class ForcedSpeechStatusEffectComponent : Component
{
    [DataField]
    public float SpeakInterval = 6.5f;

    [DataField]
    public TimeSpan NextScreamTime = TimeSpan.Zero;

    [DataField]
    public List<string> Sayings = new();
}
