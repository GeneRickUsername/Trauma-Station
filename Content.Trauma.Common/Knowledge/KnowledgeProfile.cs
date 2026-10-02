// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;

namespace Content.Trauma.Common.Knowledge;

/// <summary>
/// Stores changes to skill masteries for either a species or character.
/// For a species it is used inside <see cref="KnowledgeProfilePrototype"/> and is absolute.
/// For a character it is relative to the species.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct KnowledgeProfile
{
    /// <summary>
    /// Each skill and the amount of rolls this skill will get.
    /// </summary>
    public Dictionary<EntProtoId, int> SkillRolls;

    /// <summary>
    /// Each attribute and the amount purchased for the character.
    /// </summary>
    public Dictionary<EntProtoId, int> Attributes;

    /// <summary>
    /// Talents purchased by the character, tracking purchase rank/count for stackable talents.
    /// </summary>
    public Dictionary<EntProtoId, int> Talents;

    /// <summary>
    /// Base weapon or item proficiencies purchased.
    /// </summary>
    public Dictionary<EntProtoId, int> Proficiencies;

    /// <summary>
    /// Detailed HackMaster specializations allocated per weapon/item class.
    /// </summary>
    public Dictionary<EntProtoId, SpecializationAllocation> Specializations;
    public KnowledgeProfile(Dictionary<EntProtoId, int> attributes, Dictionary<EntProtoId, int> skillRolls, Dictionary<EntProtoId, int> talents, Dictionary<EntProtoId, int> proficiencies, Dictionary<EntProtoId, SpecializationAllocation> specializations)
    {
        Attributes = attributes;
        SkillRolls = skillRolls;
        Talents = talents;
        Proficiencies = proficiencies;
        Specializations = specializations;
    }

    /// <summary>
    /// Create an empty profile which uses the parent as-is.
    /// </summary>
    public KnowledgeProfile()
        : this(new Dictionary<EntProtoId, int>(), new Dictionary<EntProtoId, int>(), new Dictionary<EntProtoId, int>(), new Dictionary<EntProtoId, int>(), new Dictionary<EntProtoId, SpecializationAllocation>())
    {
    }

    /// <summary>
    /// Make a deep copy of another profile
    /// </summary>
    public KnowledgeProfile(KnowledgeProfile other)
        : this(new Dictionary<EntProtoId, int>(other.Attributes), new Dictionary<EntProtoId, int>(other.SkillRolls), new Dictionary<EntProtoId, int>(other.Talents), new Dictionary<EntProtoId, int>(other.Proficiencies), new Dictionary<EntProtoId, SpecializationAllocation>(other.Specializations))
    {
    }

    /// <summary>
    /// Verify potentially outdated/untrusted profile data.
    /// </summary>
    public static KnowledgeProfile Verify(Dictionary<string, int> skillRolls, Dictionary<string, int> attributePurchases, Dictionary<string, int> talents, Dictionary<string, int> proficiencies, Dictionary<string, int> specAttack, Dictionary<string, int> specDefense, Dictionary<string, int> specSpeed, Dictionary<string, int> specDamage, IPrototypeManager proto)
    {
        var profile = new KnowledgeProfile();
        foreach (var (id, change) in skillRolls)
        {
            // let's hope nobody ever changes a knowledge prototype to become non-knowledge...
            if (!proto.HasIndex(id))
                continue;

            // skill stuff
            profile.SkillRolls[id] = change;
        }
        foreach (var (id, change) in attributePurchases)
        {
            // let's hope nobody ever changes a knowledge prototype to become non-knowledge...
            if (!proto.HasIndex(id))
                continue;

            // skill stuff
            profile.Attributes[id] = change;
        }
        foreach (var (id, count) in talents)
        {
            if (!proto.HasIndex(id))
                continue;

            profile.Talents[id] = count;
        }

        foreach (var (id, level) in proficiencies)
        {
            if (!proto.HasIndex(id))
                continue;

            profile.Proficiencies[id] = level;
        }

        var specKeys = specSpeed.Keys.Union(specAttack.Keys).Union(specDefense.Keys).Union(specDamage.Keys);

        foreach (var id in specKeys)
        {
            if (!proto.HasIndex(id))
                continue;

            // Construct allocation struct from individual categories
            var alloc = new SpecializationAllocation
            {
                Speed = specSpeed.GetValueOrDefault(id, 0),
                Attack = specAttack.GetValueOrDefault(id, 0),
                Defense = specDefense.GetValueOrDefault(id, 0),
                Damage = specDamage.GetValueOrDefault(id, 0)
            };

            // Specializations require an active proficiency in the associated parent class
            if (profile.Proficiencies.TryGetValue(id, out var profLevel) && profLevel > 0 && !alloc.IsEmpty)
            {
                profile.Specializations[id] = alloc;
            }
        }
        return profile;
    }

    public bool MemberwiseEquals(KnowledgeProfile other)
    {
        if (SkillRolls.Count != other.SkillRolls.Count ||
            Attributes.Count != other.Attributes.Count ||
            Talents.Count != other.Talents.Count ||
            Proficiencies.Count != other.Proficiencies.Count ||
            Specializations.Count != other.Specializations.Count)
            return false;

        foreach (var (id, change) in SkillRolls)
        {
            if (!other.SkillRolls.TryGetValue(id, out var otherChange) || otherChange != change)
                return false;
        }

        foreach (var (id, change) in Attributes)
        {
            if (!other.Attributes.TryGetValue(id, out var otherChange) || otherChange != change)
                return false;
        }

        foreach (var (id, count) in Talents)
        {
            if (!other.Talents.TryGetValue(id, out var otherCount) || otherCount != count)
                return false;
        }

        foreach (var (id, level) in Proficiencies)
        {
            if (!other.Proficiencies.TryGetValue(id, out var otherLevel) || otherLevel != level)
                return false;
        }

        foreach (var (id, alloc) in Specializations)
        {
            if (!other.Specializations.TryGetValue(id, out var otherAlloc) || !alloc.MemberwiseEquals(otherAlloc))
                return false;
        }

        return true;
    }
}
