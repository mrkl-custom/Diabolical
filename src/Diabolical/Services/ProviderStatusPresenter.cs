using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Computes the "Vision Provider" status box's connectivity/activity text and dot state.
/// Layers two independent signals: periodic connectivity checks (RefreshAsync) and short-lived
/// activity transitions (SetActivity) driven by the main capture flow and Quick Copy. Kept free
/// of WPF types so the text/state logic is testable without a live Window.
/// </summary>
public sealed class ProviderStatusPresenter
{
    private IVisionService? _visionService;
    private string _providerName;
    private string _connectivityText;
    private ActivityState _activityState = ActivityState.Idle;

    /// <summary>Fires whenever StatusText or IsAvailable changes, so the caller can refresh its UI.</summary>
    public event Action? Changed;

    /// <summary>null = unknown (unconfigured or a check is in flight) — maps to the status dot's
    /// neutral/gray state; true/false map to connected (green) / unreachable (red).</summary>
    public bool? IsAvailable { get; private set; }

    public string StatusText => _activityState switch
    {
        ActivityState.Capturing => $"{_connectivityText} — Capturing...",
        ActivityState.Processing => $"{_connectivityText} — Processing scan...",
        ActivityState.Error => $"{_connectivityText} — Error processing scan.",
        _ => _connectivityText
    };

    public ProviderStatusPresenter(IVisionService? visionService, string providerName)
    {
        _visionService = visionService;
        _providerName = providerName;
        _connectivityText = visionService is null ? "No vision provider configured." : "Checking...";
    }

    public void SetActivity(ActivityState state)
    {
        _activityState = state;
        Changed?.Invoke();
    }

    /// <summary>Repoints the presenter at a newly selected provider; caller should follow with
    /// RefreshAsync() to run a fresh connectivity check against it.</summary>
    public void UpdateProvider(IVisionService? visionService, string providerName)
    {
        _visionService = visionService;
        _providerName = providerName;
        IsAvailable = null;
        _connectivityText = visionService is null ? "No vision provider configured." : "Checking...";
        Changed?.Invoke();
    }

    public async Task RefreshAsync()
    {
        if (_visionService is null)
        {
            IsAvailable = null;
            _connectivityText = "No vision provider configured.";
            Changed?.Invoke();
            return;
        }

        IsAvailable = null;
        _connectivityText = $"{_providerName}: checking...";
        Changed?.Invoke();

        var result = await _visionService.CheckAvailabilityAsync();

        IsAvailable = result.IsAvailable;
        _connectivityText = result.IsAvailable
            ? $"{_providerName}: connected"
            : $"{_providerName}: unreachable{(result.Detail is null ? "" : $" — {result.Detail}")}";
        Changed?.Invoke();
    }
}
