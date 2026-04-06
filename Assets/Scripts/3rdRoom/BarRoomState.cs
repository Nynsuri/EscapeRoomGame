/// <summary>
/// BarRoomState -- singleton that tracks cross-puzzle progression for the bar room.
/// No MonoBehaviour needed -- pure static state, no scene setup required.
///
/// Flow:
///   NORMAL:  Bottle puzzle done -> player receives normal syphons -> piano normal song -> normal reward
///   SECRET:  Gun puzzle done with ONLY X targets hit (no stars/circles) -> player receives secret syphons
///            -> piano secret song -> secret reward  (only if bottle puzzle NOT yet completed)
/// </summary>
public static class BarRoomState
{
    // -- Bottle puzzle ---------------------------------------------------------
    public static bool BottlePuzzleDone { get; private set; } = false;

    // -- Gun puzzle ------------------------------------------------------------
    public static bool GunPuzzleDone          { get; private set; } = false;
    public static bool GunSecretConditionMet  { get; private set; } = false; // only X targets hit

    // -- Piano syphons ---------------------------------------------------------
    public static bool HasNormalSyphons { get; private set; } = false;
    public static bool HasSecretSyphons { get; private set; } = false;

    // -- Piano -----------------------------------------------------------------
    public static bool PianoSolved { get; private set; } = false;

    // -- Called by BottlePuzzleZone (or a room manager) when all bottles placed -
    public static void OnBottlePuzzleCompleted()
    {
        if (BottlePuzzleDone) return;
        BottlePuzzleDone = true;

        // Grant normal syphons -- secret path is now locked forever
        HasNormalSyphons = true;
        HasSecretSyphons = false;

        UnityEngine.Debug.Log("[BarRoomState] Bottle puzzle done. Normal syphons granted. Secret path locked.");
    }

    // -- Called by ShootingPuzzle when the gun puzzle is completed -------------
    //    secretCondition = true means ONLY the 5 X targets were hit, nothing else
    public static void OnGunPuzzleCompleted(bool secretCondition)
    {
        if (GunPuzzleDone) return;
        GunPuzzleDone         = true;
        GunSecretConditionMet = secretCondition;

        if (secretCondition && !BottlePuzzleDone)
        {
            // Secret path: player hit only X targets AND bottle puzzle not done yet
            HasSecretSyphons = true;
            UnityEngine.Debug.Log("[BarRoomState] Gun puzzle done (secret condition). Secret syphons granted.");
        }
        else
        {
            // Normal gun completion or bottle already done -- no secret syphons
            UnityEngine.Debug.Log($"[BarRoomState] Gun puzzle done. Secret={secretCondition}, BottleDone={BottlePuzzleDone}. No secret syphons.");
        }
    }

    // -- Called by PianoPuzzle when solved ------------------------------------
    public static void OnPianoSolved()
    {
        PianoSolved = true;
    }

    // -- Helper ---------------------------------------------------------------
    public static bool CanDoSecretPiano => HasSecretSyphons && !BottlePuzzleDone;
}
