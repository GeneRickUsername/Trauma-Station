// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Trauma.Common.Armor;

/// <summary>
///     Tracks permanently degraded flat armor reductions on an item when hit by incoming attacks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArmorDegradationComponent : Component
{
    /// <summary>
    /// Amount of flat damage reduction stripped per degrading hit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DegradationStep = 1.0f;

    /// <summary>
    /// Tracks minimum flat reduction floor so armor cannot degrade below this value.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinimumFlatReduction = 0.0f;
}
