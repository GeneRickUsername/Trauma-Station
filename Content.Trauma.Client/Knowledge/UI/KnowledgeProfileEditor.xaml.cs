// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Content.Trauma.Client.Knowledge;
using Content.Trauma.Common.Knowledge;
using Content.Trauma.Common.Knowledge.Prototypes;
using Content.Trauma.Shared.Weapons.Classes;
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
            ReloadWeaponProficiencies();
            ReloadTalents();
        };
    }

    public void SetProfile(ProtoId<SpeciesPrototype> species, KnowledgeProfile profile)
    {
        _profile = profile;
        _parent = _proto.Index(_proto.Index(species).Knowledge);
        ReloadSkills();
        ReloadAttributes();
        ReloadWeaponProficiencies();
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

            // Put the skill in its respective category (or create it if there isn't one yet)
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

    private void ReloadWeaponProficiencies()
    {
        EnabledProficiencies.RemoveAllChildren();

        // Iterate over defined weapon classes
        foreach (var weaponClass in _proto.EnumeratePrototypes<WeaponClassPrototype>())
        {
            if (!_proto.TryIndex(weaponClass.Knowledge, out var knowledgeProto))
                continue;

            var protoId = (EntProtoId) weaponClass.ID;
            var control = new WeaponClassProficiencyControl(weaponClass.Name, costPerLevel: 10);
            var profLevel = _profile.Proficiencies.GetValueOrDefault(protoId, 0);

            // Bind parent level changes
            control.OnChangeProficiencyLevel += diff =>
            {
                var nextLevel = Math.Max(0, control.Level + diff);
                if (nextLevel == 0)
                {
                    _profile.Proficiencies.Remove(protoId);
                    _profile.Specializations.Remove(protoId); // Drop specs if proficiency is removed
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

            // HackMaster Specializations: 4-category allocation (Speed, Attack, Defense, Damage)
            var currentAlloc = _profile.Specializations.GetValueOrDefault(protoId);
            control.SetSpecializationAllocation(currentAlloc);

            control.OnChangeSpecializationAllocation += (category, diff) =>
            {
                // Verify active proficiency before permitting specialization adjustments
                if (!_profile.Proficiencies.TryGetValue(protoId, out var level) || level <= 0)
                    return;

                var alloc = _profile.Specializations.GetValueOrDefault(protoId);
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

            var control = new TalentControl(name, talentComp.Cost);
            control.SetRank(rank);

            control.OnChangeRank += diff =>
            {
                var nextRank = Math.Clamp(control.Rank + diff, 0, talentComp.Repeat ? int.MaxValue : 1);
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

        // Can't save with a deficit
        PointsLabel.FontColorOverride = Color.Red;
        SaveButton.Disabled = true;
    }

    private void UpdateReset()
    {
        ResetButton.Disabled = true;

        // Enable reset button if ANY profile dictionary contains modified data
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
