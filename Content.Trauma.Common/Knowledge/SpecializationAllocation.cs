namespace Content.Trauma.Common.Knowledge;

/// <summary>
/// Tracks point allocations for weapon specializations across the 4 core combat metrics.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct SpecializationAllocation
{
    [DataField] public int Speed;
    [DataField] public int Attack;
    [DataField] public int Defense;
    [DataField] public int Damage;

    public readonly bool IsEmpty => Speed == 0 && Attack == 0 && Defense == 0 && Damage == 0;

    public readonly bool MemberwiseEquals(SpecializationAllocation other)
    {
        return Speed == other.Speed &&
               Attack == other.Attack &&
               Defense == other.Defense &&
               Damage == other.Damage;
    }

    public readonly int TotalCost(int baseCost)
    {
        return (TotalIter(Speed) + TotalIter(Attack) + TotalIter(Defense) + TotalIter(Damage)) * baseCost;
    }

    private static int TotalIter(int cost)
    {
        return cost * (cost + 1) / 2;
    }
}
