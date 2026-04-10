using UnityEngine;

/// <summary>
/// BasePuzzle — inherit from this instead of MonoBehaviour on every puzzle script.
/// Automatically blocks the pause menu while any puzzle is open via
/// BasePuzzle.AnyPuzzleActive, which PauseMenuManager checks directly.
///
/// USAGE:
///   Change:  public class HackingPuzzle : MonoBehaviour
///   To:      public class HackingPuzzle : BasePuzzle
///
///   Call OpenPuzzle()  when the puzzle UI opens.
///   Call ClosePuzzle() when the puzzle UI closes.
///
///   Your puzzle's Update() must be:  protected override void Update()
///   and must call base.Update() as its very first line.
///   If your puzzle has OnDestroy(), call base.OnDestroy() inside it.
/// </summary>
[DefaultExecutionOrder(-200)]
public abstract class BasePuzzle : MonoBehaviour
{
    private static int _activePuzzleCount = 0;

    /// <summary>True whenever at least one puzzle UI is open.</summary>
    public static bool AnyPuzzleActive => _activePuzzleCount > 0;

    private bool _puzzleIsOpen = false;

    protected virtual void Update() { }

    /// <summary>Call when the puzzle UI opens.</summary>
    protected void OpenPuzzle()
    {
        if (_puzzleIsOpen) return;
        _puzzleIsOpen = true;
        _activePuzzleCount++;
    }

    /// <summary>Call when the puzzle UI closes.</summary>
    protected void ClosePuzzle()
    {
        if (!_puzzleIsOpen) return;
        _puzzleIsOpen = false;
        _activePuzzleCount = Mathf.Max(0, _activePuzzleCount - 1);
    }

    protected virtual void OnDestroy()
    {
        ClosePuzzle();
    }
}