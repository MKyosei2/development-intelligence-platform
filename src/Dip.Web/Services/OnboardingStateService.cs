using Microsoft.JSInterop;

namespace Dip.Web.Services;

public enum GuidedTourMode
{
    Product,
    Reviewer
}

/// <summary>
/// Browser-only onboarding state. Completion is deliberately stored in localStorage
/// because it is a UI preference and must never become project truth or evidence.
/// </summary>
public sealed class OnboardingStateService(IJSRuntime js) : IAsyncDisposable
{
    private const string CompletionKey = "dip.onboarding.completed.v1";
    private IJSObjectReference? _module;

    public bool Initialized { get; private set; }
    public bool IsOpen { get; private set; }
    public bool ShowWelcome { get; private set; }
    public int StepIndex { get; private set; }
    public GuidedTourMode Mode { get; private set; } = GuidedTourMode.Product;

    public event Action? Changed;

    public async Task InitializeAsync(GuidedTourMode defaultMode, CancellationToken cancellationToken = default)
    {
        if (Initialized)
            return;

        Initialized = true;
        Mode = defaultMode;

        try
        {
            _module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/onboarding.js");
            var completed = await _module.InvokeAsync<bool>("getBoolean", cancellationToken, CompletionKey);
            if (!completed)
            {
                IsOpen = true;
                ShowWelcome = true;
                StepIndex = 0;
            }
        }
        catch (JSException)
        {
            // A blocked/unavailable localStorage must not prevent the UI from rendering.
            IsOpen = true;
            ShowWelcome = true;
            StepIndex = 0;
        }

        Notify();
    }

    public void Start(GuidedTourMode mode, bool showWelcome = false)
    {
        Mode = mode;
        StepIndex = 0;
        ShowWelcome = showWelcome;
        IsOpen = true;
        Notify();
    }

    public void Begin()
    {
        ShowWelcome = false;
        StepIndex = 0;
        IsOpen = true;
        Notify();
    }

    public void Previous()
    {
        if (StepIndex <= 0)
            return;
        StepIndex--;
        Notify();
    }

    public void Next(int stepCount)
    {
        if (stepCount < 1 || StepIndex >= stepCount - 1)
            return;
        StepIndex++;
        Notify();
    }

    public async Task CloseAsync(bool markCompleted, CancellationToken cancellationToken = default)
    {
        IsOpen = false;
        ShowWelcome = false;
        StepIndex = 0;

        if (markCompleted && _module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("setBoolean", cancellationToken, CompletionKey, true);
            }
            catch (JSException)
            {
                // Persistence failure is a UI preference failure only.
            }
        }

        Notify();
    }

    public async Task ResetCompletionAsync(CancellationToken cancellationToken = default)
    {
        if (_module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync("removeValue", cancellationToken, CompletionKey);
        }
        catch (JSException)
        {
        }
    }

    private void Notify() => Changed?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
            return;
        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
