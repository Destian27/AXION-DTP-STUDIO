using UnityEngine;

/// <summary>
/// Satu pintu untuk semua input gameplay.
/// Keyboard tetap jalan (Editor / PC), layar sentuh mengisi flag Touch* lewat MobileControls.
/// </summary>
public static class GameInput
{
    // Diisi oleh tombol layar sentuh (lihat MobileControls).
    public static bool TouchForwardHeld;
    public static bool TouchBackwardHeld;

    public static bool ForwardHeld => TouchForwardHeld || Input.GetKey(KeyCode.D);
    public static bool BackwardHeld => TouchBackwardHeld || Input.GetKey(KeyCode.A);

    /// <summary>Ketuk layar / klik / Enter / Space (dipakai layar judul).</summary>
    public static bool StartPressed()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            return true;

        for (int i = 0; i < Input.touchCount; i++)
        {
            if (Input.GetTouch(i).phase == TouchPhase.Began)
                return true;
        }
        return false;
    }

    public static void ResetTouch()
    {
        TouchForwardHeld = false;
        TouchBackwardHeld = false;
    }
}
