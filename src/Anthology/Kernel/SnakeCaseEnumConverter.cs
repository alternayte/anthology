using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Anthology.Kernel;

public sealed class SnakeCaseEnumConverter<T>()
    : ValueConverter<T, string>(
        v => ToSnakeCase(v),
        v => FromSnakeCase(v))
    where T : struct, Enum
{
    private static readonly Dictionary<T, string> SnakeCaseMap =
        Enum.GetValues<T>().ToDictionary(
            v => v,
            v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()));

    private static readonly Dictionary<string, T> EnumMap =
        Enum.GetValues<T>().ToDictionary(
            v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()),
            v => v);

    private static string ToSnakeCase(T value) => SnakeCaseMap[value];
    private static T FromSnakeCase(string value) => EnumMap[value];
}
