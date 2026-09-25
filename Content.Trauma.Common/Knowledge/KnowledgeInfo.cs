// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Trauma.Common.Knowledge;

[Serializable, NetSerializable]
public record struct SkillInfo(string Name, string Description, Color Color, SpriteSpecifier? Sprite, int LearnedLevel, int NetLevel, int CurrentExp, int ExpCost);

[Serializable, NetSerializable]
public record struct AttributeInfo(string Name, string Description, NetEntity Entity);

[Serializable, NetSerializable]
public record struct TalentInfo(string Name, string Description, SpriteSpecifier? Sprite, int Rank, bool Repeat);

[Serializable, NetSerializable]
public record struct ProficiencyInfo(string Name, string Description, SpriteSpecifier? Sprite, SpecializationAllocation? Specialization);
