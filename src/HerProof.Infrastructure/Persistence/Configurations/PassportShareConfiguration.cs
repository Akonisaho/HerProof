using System.Text.Json;
using HerProof.Domain.Passports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HerProof.Infrastructure.Persistence.Configurations;

/// <summary>docs/02: visible_fields is a jsonb allow-list of passport fields the share may expose.
/// The public verify page must never show a field outside this list (docs/05 P15).</summary>
public class PassportShareConfiguration : IEntityTypeConfiguration<PassportShare>
{
    public void Configure(EntityTypeBuilder<PassportShare> builder)
    {
        builder.Property(s => s.VisibleFields)
            .HasConversion(
                fields => JsonSerializer.Serialize(fields, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>(),
                new ValueComparer<IReadOnlyCollection<string>>(
                    (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
                    f => f.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    f => f.ToList()))
            .HasColumnType("jsonb");
    }
}
