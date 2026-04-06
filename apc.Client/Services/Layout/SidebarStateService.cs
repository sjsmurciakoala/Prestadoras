namespace apc.Client.Services.Layout;

public sealed class SidebarStateService
{
    public event Action? Changed;

    private bool? isOpenOverride;

    public bool IsXSmall { get; private set; }

    public bool IsOpen
    {
        get => isOpenOverride ?? !IsXSmall;
    }

    public void Toggle()
    {
        isOpenOverride = !IsOpen;
        Changed?.Invoke();
    }

    public void SetOpen(bool isOpen)
    {
        isOpenOverride = isOpen;
        Changed?.Invoke();
    }

    public void SetBreakpoint(bool isXSmall)
    {
        if (IsXSmall == isXSmall)
        {
            return;
        }

        IsXSmall = isXSmall;
        isOpenOverride = null;
        Changed?.Invoke();
    }
}
