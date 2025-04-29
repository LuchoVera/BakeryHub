namespace BakeryHub.Application.Recommendations;

public class DataMappings
{
    public Dictionary<Guid, float> UserGuidToFloatMap { get; set; } = new();
    public Dictionary<Guid, int> ProductGuidToIntMap { get; set; } = new();
    public Dictionary<Guid, int> CategoryGuidToIntMap { get; set; } = new();

    public Dictionary<int, Guid> IntToProductGuidMap { get; set; } = new();
    public Dictionary<int, Guid> IntToCategoryGuidMap { get; set; } = new();

    public Dictionary<int, int> ProductIntToCategoryIntMap { get; set; } = new();

    public Dictionary<Guid, HashSet<Guid>> UserPurchaseHistory { get; set; } = new();
}
