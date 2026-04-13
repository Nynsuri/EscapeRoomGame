/// <summary>
/// BarRoomState -- static state tracker for the bar room puzzles.
///
/// Syphons are NO LONGER granted automatically on puzzle completion.
/// Instead, the puzzle spawns a SymphonyNote. The player reads it,
/// which calls GrantNormalSyphons() or GrantSecretSyphons() here.
/// </summary>
public static class BarRoomState
{
    // -- Bottle puzzle ---------------------------------------------------------
    public static bool BottlePuzzleDone { get; private set; } = false;

    // -- Gun puzzle ------------------------------------------------------------
    public static bool GunPuzzleDone { get; private set; } = false;
    public static bool GunSecretConditionMet { get; private set; } = false;

    // -- Piano syphons ---------------------------------------------------------
    public static bool HasNormalSyphons { get; private set; } = false;
    public static bool HasSecretSyphons { get; private set; } = false;

    // -- Piano -----------------------------------------------------------------
    public static bool PianoSolved { get; private set; } = false;

    // -- Called by BarBottleRoomManager when all bottles placed ----------------
    // Does NOT grant syphons anymore � just marks puzzle done.
    // Syphons are granted when player reads the normal SymphonyNote.
    public static void OnBottlePuzzleCompleted()
    {
        if (BottlePuzzleDone) return;
        BottlePuzzleDone = true;
        UnityEngine.Debug.Log("[BarRoomState] Bottle puzzle done. Awaiting note read for syphons.");
    }

    // -- Called by ShootingPuzzle when gun puzzle completed --------------------
    // Does NOT grant syphons anymore � just marks puzzle done.
    // Syphons are granted when player reads the secret SymphonyNote.
    public static void OnGunPuzzleCompleted(bool secretCondition)
    {
        if (GunPuzzleDone) return;
        GunPuzzleDone = true;
        GunSecretConditionMet = secretCondition;
        UnityEngine.Debug.Log($"[BarRoomState] Gun puzzle done. Secret={secretCondition}. Awaiting note read.");
    }

    // -- Called by SymphonyNote (normal) when player reads the normal note -----
    public static void GrantNormalSyphons()
    {
        if (HasNormalSyphons) return;
        HasNormalSyphons = true;
        HasSecretSyphons = false; // normal path locks secret forever
        UnityEngine.Debug.Log("[BarRoomState] Normal syphons granted.");
    }

    // Kept for backward compatibility � same as GrantNormalSyphons
    // (BarBottleRoomManager still calls OnBottlePuzzleCompleted which no longer
    //  auto-grants; the note calls this instead)
    public static void GrantSecretSyphons()
    {
        if (HasSecretSyphons) return;
        if (BottlePuzzleDone) return; // bottle done = secret locked
        HasSecretSyphons = true;
        UnityEngine.Debug.Log("[BarRoomState] Secret syphons granted.");
    }

    // -- Called by PianoPuzzle when solved ------------------------------------
    public static void OnPianoSolved()
    {
        PianoSolved = true;
    }

    // -- Portal side -----------------------------------------------------------
    /// <summary>True while the player is on the corridor side of the portal ring.</summary>
    public static bool PlayerInCorridor { get; private set; } = false;

    public static void SetPlayerSide(bool inCorridor)
    {
        PlayerInCorridor = inCorridor;
    }

    // -- Helper ---------------------------------------------------------------
    public static bool CanDoSecretPiano => HasSecretSyphons && !BottlePuzzleDone;
}