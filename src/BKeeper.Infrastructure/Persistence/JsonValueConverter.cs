using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BKeeper.Infrastructure.Persistence;

/// <summary>Stores a Dictionary/List property as a Postgres jsonb column via System.Text.Json.</summary>
public static class JsonValueConverter
{
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> builder) where T : class, new()
    {
        builder.HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null) ?? new T());
        builder.HasColumnType("jsonb");

        var comparer = new ValueComparer<T>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);
        builder.Metadata.SetValueComparer(comparer);

        return builder;
    }
}
