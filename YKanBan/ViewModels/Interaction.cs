namespace YKanBan.ViewModels;

/// <summary>Input placeholder for interactions that carry no meaningful input.</summary>
public readonly record struct Unit;

/// <summary>
/// A view-model side request that the owning view fulfils through a
/// registered handler (dialogs, pickers) — the classic MVVM interaction
/// pattern, implemented locally without extra dependencies.
/// </summary>
/// <typeparam name="TInput">The request payload.</typeparam>
/// <typeparam name="TOutput">The handler's answer.</typeparam>
public sealed class Interaction<TInput, TOutput>
{
    private Func<TInput, Task<TOutput>>? _handler;

    /// <summary>Registers the single handler; the view calls this once.</summary>
    public void RegisterHandler(Func<TInput, Task<TOutput>> handler) => _handler = handler;

    /// <summary>Invokes the registered handler.</summary>
    /// <exception cref="InvalidOperationException">No handler has been registered.</exception>
    public Task<TOutput> Handle(TInput input) =>
        _handler is null
            ? throw new InvalidOperationException("No handler registered for this interaction.")
            : _handler(input);
}
