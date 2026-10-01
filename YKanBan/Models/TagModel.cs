namespace YKanBan.Models;

/// <summary>A tag assignable to cards. Tags are never garbage-collected: unreferenced tags stay until deleted explicitly.</summary>
/// <param name="Id">Row id.</param>
/// <param name="Name">Arbitrary non-empty text; no charset or length rules.</param>
/// <param name="Color">Fixed #RRGGBB form.</param>
/// <param name="Description">Free-form description, possibly empty.</param>
public sealed record TagModel(long Id, string Name, string Color, string Description);
