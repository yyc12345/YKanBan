namespace YKanBan.Storage;

/// <summary>A single incremental schema step applied when the stored user_version is one below <see cref="Version"/>.</summary>
/// <param name="Version">Target user_version after applying this migration (contiguous, starting at 1).</param>
/// <param name="Name">Short human-readable identifier used for diagnostics.</param>
/// <param name="Sql">SQL script executed inside one transaction together with the user_version bump.</param>
public sealed record SchemaMigration(int Version, string Name, string Sql);
