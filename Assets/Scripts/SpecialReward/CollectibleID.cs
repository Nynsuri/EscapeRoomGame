/// <summary>
/// CollectibleID — Unity 6000.3.9f1
///
/// Shared identifier between a CollectibleItem and its matching ItemPedestal.
/// Add a new entry here for every unique collectible in your game.
///
/// Example:
///   Room 1 collectible  →  CollectibleID.RedRelic
///   Room 1 pedestal     →  CollectibleID.RedRelic   ← same value = match
/// </summary>
public enum CollectibleID
{
    None = 0,
    RedRelic    = 1,
    BlueRelic   = 2,
    GreenRelic  = 3,
}
