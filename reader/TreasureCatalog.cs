namespace ChestCounter;

public sealed record TreasurePrize(
    string Id,
    string Kind,
    string Name,
    string? Image,
    params string[] PrizeIds
);

public static class TreasureCatalog
{
    public const string PirateShop = "pirate-shop";

    public static TreasurePrize? Find(string prizeId) => null;

    public static string KindName(string kind) => kind;
}