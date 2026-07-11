namespace Diabolical.Models;

/// <summary>
/// App activity layered on top of the Vision Provider status box's connectivity text —
/// driven by both the main capture flow (ScreenCaptureService) and Quick Copy
/// (QuickCopyService). See CLAUDE.md's Decisions Log.
/// </summary>
public enum ActivityState
{
    Idle,
    Capturing,
    Processing,
    Error
}
