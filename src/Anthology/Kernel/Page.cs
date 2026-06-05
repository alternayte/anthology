namespace Anthology.Kernel;

public sealed record Page<T>(List<T> Items, string? NextCursor);
