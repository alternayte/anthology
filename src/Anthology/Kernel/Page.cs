namespace Anthology.Kernel;

public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
