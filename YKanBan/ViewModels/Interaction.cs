namespace YKanBan.ViewModels;

/// <summary>
/// Input placeholder for interactions that carry no meaningful payload.
/// </summary>
public readonly record struct Unit {
    /// <summary>
    /// Gets the single unit value.
    /// </summary>
    public static Unit Value => default;
}

/// <summary>
/// A view-model side request with no result that the owning view fulfils
/// through a registered handler (opening a dialog, for example) — the classic
/// MVVM interaction pattern, implemented locally without extra dependencies.
/// The view model never holds a reference to a view.
/// </summary>
/// <typeparam name="TInput">The request payload.</typeparam>
public sealed class Interaction<TInput> {
    private Func<TInput, Task>? _handler;

    /// <summary>
    /// Registers the single handler; the view calls this once.
    /// </summary>
    /// <param name="handler">The handler that fulfils the request.</param>
    public void RegisterHandler(Func<TInput, Task> handler) => _handler = handler;

    /// <summary>
    /// Invokes the registered handler.
    /// </summary>
    /// <param name="input">The request payload.</param>
    /// <returns>A task that completes when the handler completes.</returns>
    /// <exception cref="InvalidOperationException">No handler has been registered.</exception>
    public Task Handle(TInput input) =>
        _handler is null
            ? throw new InvalidOperationException("No handler registered for this interaction.")
            : _handler(input);
}

/// <summary>
/// A view-model side request that produces a result, fulfilled through a
/// registered handler.
/// </summary>
/// <typeparam name="TInput">The request payload.</typeparam>
/// <typeparam name="TOutput">The handler's answer.</typeparam>
public sealed class Interaction<TInput, TOutput> {
    private Func<TInput, Task<TOutput>>? _handler;

    /// <summary>
    /// Registers the single handler; the view calls this once.
    /// </summary>
    /// <param name="handler">The handler that fulfils the request.</param>
    public void RegisterHandler(Func<TInput, Task<TOutput>> handler) => _handler = handler;

    /// <summary>
    /// Invokes the registered handler.
    /// </summary>
    /// <param name="input">The request payload.</param>
    /// <returns>The handler's answer.</returns>
    /// <exception cref="InvalidOperationException">No handler has been registered.</exception>
    public Task<TOutput> Handle(TInput input) =>
        _handler is null
            ? throw new InvalidOperationException("No handler registered for this interaction.")
            : _handler(input);
}
