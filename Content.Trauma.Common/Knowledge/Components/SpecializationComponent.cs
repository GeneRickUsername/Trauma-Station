// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Trauma.Common.Knowledge.Components;

/// <summary>
/// Stores all specializations. Because I can't think up of a better method.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SpecializationComponent : Component
{

    [DataField]
    public SpecializationAllocation Specialization = new();
}
