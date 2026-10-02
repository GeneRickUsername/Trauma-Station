// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Content.Trauma.Client.Knowledge;
using Content.Trauma.Common.Knowledge;
using Content.Trauma.Common.Knowledge.Components;
using Content.Trauma.Common.Knowledge.Prototypes;
using Content.Trauma.Shared.Knowledge.Components;
using Content.Trauma.Shared.Weapons.Classes;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Trauma.Client.Knowledge.UI;

[GenerateTypedNameReferences]
public sealed partial class KnowledgeProfileEditor : BoxContainer
{
    private readonly IPrototypeManager _proto;
    private readonly KnowledgeSystem _knowledge;

    public event Action<KnowledgeProfile>? OnSave;

    private KnowledgeProfilePrototype _parent = default!;
    private KnowledgeProfile _profile = new();
    private bool _modified;

    public KnowledgeProfileEditor(IPrototypeManager proto, KnowledgeSystem knowledge)
    {
        RobustXamlLoader.Load(this);

        _proto = proto;
        _knowledge = knowledge;

        SaveButton.OnPressed += _ =>
        {
            OnSave?.Invoke(_profile);
            _modified = false;
            SaveButton.Disabled = true;
        };

        ResetButton.OnPressed += _ =>
        {
            _profile = new();
            _modified = true;
            ResetButton.Disabled = true;
            ReloadSkills();
            ReloadAttributes();
            ReloadProficiencies();
            ReloadTalents();
        };
    }

    public void SetProfile(ProtoId<SpeciesPrototype> species, KnowledgeProfile profile)
    {
        _profile = profile;
        _parent = _proto.Index(_proto.Index(species).Knowledge);
        ReloadSkills();
        ReloadAttributes();
        ReloadProficiencies();
        ReloadTalents();
        UpdateReset();
    }

    private void ReloadSkills()
    {
        Dictionary<ProtoId<SkillCategoryPrototype>, BoxContainer> categories = [];
        UpdatePoints();

        EnabledSkills.RemoveAllChildren();
        foreach (var (id, comp) in _knowledge.AllSkills)
        {
            var name = _proto.Index(id).Name;
            if (comp.Cost is not { } costs)
                continue;

            var control = new SkillControl(name, costs);
            var racialBase = _parent.Profile.SkillRolls.GetValueOrDefault(id);
            var mastery = _profile.SkillRolls.GetValueOrDefault(id) + racialBase;

            control.SetRolls(mastery, racialBase);

            control.OnChangeRolls += diff =>
            {
                var sum = control.Rolls + diff;
                if (sum < racialBase)
                    return;

                control.SetRolls(sum, racialBase);
                if (sum == 0)
                    _profile.SkillRolls.Remove(id);
                else
                    _profile.SkillRolls[id] = _profile.SkillRolls.GetValueOrDefault(id) + diff;

                _modified = true;
                UpdatePoints();
                UpdateReset();
            };

            if (categories.TryGetValue(comp.Category, out var category))
            {
                category.AddChild(control);
            }
            else
            {
                var newCategory = new SkillCategory(Loc.GetString(_proto.Index(comp.Category).Name));
                EnabledSkills.AddChild(newCategory);
                newCategory.AddChild(control);
                categories.TryAdd(comp.Category, newCategory);
            }
        }
    }

    private void ReloadAttributes()
    {
        UpdatePoints();

        EnabledAttributes.RemoveAllChildren();

        var sortedAttributes = _knowledge.AllAttributes
            .OrderBy(attr => attr.Value.Order);
        foreach (var (id, comp) in sortedAttributes)
        {
            var name = _proto.Index(id).Name;

            var control = new AttributeControl(name);
            var racialBase = _parent.Profile.Attributes.GetValueOrDefault(id);
            var mastery = _profile.Attributes.GetValueOrDefault(id) + racialBase;
            control.SetPurchased(mastery, racialBase);

            control.OnChangePurchased += diff =>
            {
                var sum = control.Purchased + diff;
                if (sum < racialBase)
                    return;

                control.SetPurchased(sum, racialBase);
                if (sum == 0)
                    _profile.Attributes.Remove(id);
                else
                    _profile.Attributes[id] = _profile.Attributes.GetValueOrDefault(id) + diff;

                _modified = true;
                UpdatePoints();
                UpdateReset();
            };

            EnabledAttributes.AddChild(control);
        }
    }

    private void ReloadProficiencies()
    {
        EnabledProficiencies.RemoveAllChildren();

        // Get all weapon classes that explicitly support specializations
        var specializationEnabledClasses = _proto.EnumeratePrototypes<WeaponClassPrototype>()
            .Select(wc => wc.Knowledge)
            .ToHashSet();

        foreach (var (protoId, profComp) in _knowledge.AllProficiencies)
        {
            var costPerLevel = profComp.Cost;
            var name = _proto.Index(protoId).Name;

            var control = new WeaponClassProficiencyControl(name, costPerLevel: costPerLevel, maxLevel: 1);
            var profLevel = _profile.Proficiencies.GetValueOrDefault(protoId, 0);

            var hasSpecializations = specializationEnabledClasses.Contains(protoId);
            control.SetSpecializationVisible(hasSpecializations);

            // Proficiency level is capped at 0 or 1
            control.OnChangeProficiencyLevel += diff =>
            {
                var nextLevel = Math.Clamp(control.Level + diff, 0, 1);
                if (nextLevel == 0)
                {
                    _profile.Proficiencies.Remove(protoId);
                    _profile.Specializations.Remove(protoId); // Clear specializations if proficiency is lost
                }
                else
                {
                    _profile.Proficiencies[protoId] = nextLevel;
                }

                control.SetLevel(nextLevel);

                _modified = true;
                UpdatePoints();
                UpdateReset();
            };

            // Specialization allocation can be bought infinitely as long as proficiency >= 1
            if (hasSpecializations)
            {
                var currentAlloc = _profile.Specializations.GetValueOrDefault(protoId);
                control.SetSpecializationAllocation(currentAlloc);

                control.OnChangeSpecializationAllocation += (category, diff) =>
                {
                    if (!_profile.Proficiencies.TryGetValue(protoId, out var level) || level <= 0)
                        return;

                    var alloc = _profile.Specializations.GetValueOrDefault(protoId);

                    if (diff > 0)
                    {
                        // Find the current minimum points among all 4 specialization categories
                        var minVal = Math.Min(Math.Min(alloc.Speed, alloc.Attack), Math.Min(alloc.Defense, alloc.Damage));

                        // Prevent increasing any category if it would exceed minVal + 1
                        var currentVal = category switch
                        {
                            SpecializationCategory.Speed => alloc.Speed,
                            SpecializationCategory.Attack => alloc.Attack,
                            SpecializationCategory.Defense => alloc.Defense,
                            SpecializationCategory.Damage => alloc.Damage,
                            _ => 0
                        };

                        if (currentVal >= minVal + 1)
                            return;
                    }

                    switch (category)
                    {
                        case SpecializationCategory.Speed:
                            alloc.Speed = Math.Max(0, alloc.Speed + diff);
                            break;
                        case SpecializationCategory.Attack:
                            alloc.Attack = Math.Max(0, alloc.Attack + diff);
                            break;
                        case SpecializationCategory.Defense:
                            alloc.Defense = Math.Max(0, alloc.Defense + diff);
                            break;
                        case SpecializationCategory.Damage:
                            alloc.Damage = Math.Max(0, alloc.Damage + diff);
                            break;
                    }

                    if (alloc.IsEmpty)
                        _profile.Specializations.Remove(protoId);
                    else
                        _profile.Specializations[protoId] = alloc;

                    control.SetSpecializationAllocation(alloc);

                    _modified = true;
                    UpdatePoints();
                    UpdateReset();
                };
            }

            control.SetLevel(profLevel);
            EnabledProficiencies.AddChild(control);
        }
    }

    private void ReloadTalents()
    {
        EnabledTalents.RemoveAllChildren();

        foreach (var (id, talentComp) in _knowledge.AllTalents)
        {
            var rank = _profile.Talents.GetValueOrDefault(id, 0);
            var name = _proto.Index(id).Name;

            // Allow infinite purchases if Repeat is true, otherwise hard-cap at 1 rank
            var maxRank = talentComp.Repeat ? int.MaxValue : 1;
            var control = new TalentControl(name, talentComp.Cost, maxRank);
            control.SetRank(rank);

            control.OnChangeRank += diff =>
            {
                var nextRank = Math.Clamp(control.Rank + diff, 0, maxRank);
                if (nextRank == 0)
                    _profile.Talents.Remove(id);
                else
                    _profile.Talents[id] = nextRank;

                control.SetRank(nextRank);

                _modified = true;
                UpdatePoints();
                UpdateReset();
            };

            EnabledTalents.AddChild(control);
        }
    }

    private void UpdatePoints()
    {
        var points = _parent.PointsLimit;
        var cost = _knowledge.ProfileCost(_profile);
        points -= cost;
        PointsLabel.Text = Loc.GetString("knowledge-editor-points", ("points", points));
        if (points >= 0)
        {
            PointsLabel.FontColorOverride = Color.White;
            SaveButton.Disabled = !_modified;
            return;
        }

        PointsLabel.FontColorOverride = Color.Red;
        SaveButton.Disabled = true;
    }

    private void UpdateReset()
    {
        ResetButton.Disabled = true;

        if (_profile.SkillRolls.Values.Any(level => level != 0) ||
            _profile.Attributes.Values.Any(level => level != 0) ||
            _profile.Proficiencies.Values.Any(level => level != 0) ||
            _profile.Talents.Values.Any(rank => rank > 0) ||
            _profile.Specializations.Values.Any(alloc => !alloc.IsEmpty))
        {
            ResetButton.Disabled = false;
        }
    }
}
