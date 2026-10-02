namespace YKanBan.Storage;

/// <summary>
/// A single incremental schema step. Migrations form a contiguous list
/// starting at version 1; each step is applied when the stored
/// <c>user_version</c> is exactly one below <see cref="Version"/>.
/// </summary>
/// <param name="Version">Target <c>user_version</c> after this migration is applied.</param>
/// <param name="Name">Short human-readable identifier used for diagnostics.</param>
/// <param name="Sql">SQL script executed atomically together with the version bump.</param>
public sealed record SchemaMigration(int Version, string Name, string Sql);
