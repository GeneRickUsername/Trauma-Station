// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Trauma.Common.Weapons;

namespace Content.Trauma.Common.Knowledge.Components;

/// <summary>
/// Stores information about the entity that holds knowledge units,
/// see <see cref="KnowledgeContainerComponent"/>, usually a brain.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KnowledgeHolderComponent : Component
{
    /// <summary>
    /// Pointer to the actual entity with KnowledgeContainerComponent.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? KnowledgeEntity;

    /// <summary>
    /// The exact server time when the current queued action is allowed to execute.
    /// </summary>
    [DataField]
    public TimeSpan ScheduledExecutionTime = TimeSpan.Zero;

    /// <summary>
    /// Target entities the user is currently winding up to strike.
    /// </summary>
    [DataField]
    public List<EntityUid> QueuedTargets = new();

    /// <summary>
    /// Weapon ID, if any.
    /// </summary>
    [DataField]
    public EntityUid? Weapon;

    /// <summary>
    /// What kind are we fighting?
    /// </summary>
    [DataField]
    public HarmfulActionType NextAttackType = HarmfulActionType.Harm;
}
