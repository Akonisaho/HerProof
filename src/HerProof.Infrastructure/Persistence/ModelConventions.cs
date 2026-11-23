using Microsoft.EntityFrameworkCore;

namespace HerProof.Infrastructure.Persistence;

/// <summary>Applies docs/02-DATABASE.md's repo-wide rules once instead of per entity:
/// rule 5 (every FK has an index), rule 6 (enums stored as strings with check constraints),
/// and JSON columns (any `*Json`-suffixed string property) stored as jsonb.
/// Relationships here are scalar Guid ids, not EF navigations (hackathon-speed tradeoff,
/// see docs/STUBS.md): referential integrity is enforced in the Application layer, not by DB FKs.</summary>
public static class ModelConventions
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties().ToList())
            {
                var clrProperty = property.PropertyInfo;
                if (clrProperty is null)
                {
                    continue;
                }

                var isForeignKeyLike = property.Name != "Id"
                    && property.Name.EndsWith("Id", StringComparison.Ordinal)
                    && property.ClrType == typeof(Guid)
                    && !property.IsPrimaryKey();

                if (isForeignKeyLike)
                {
                    modelBuilder.Entity(entityType.ClrType).HasIndex(property.Name);
                }

                if (property.ClrType.IsEnum)
                {
                    var allowedValues = Enum.GetNames(property.ClrType);
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasConversion<string>()
                        .HasMaxLength(64);

                    // EFCore.NamingConventions renames table/column identifiers to snake_case during
                    // model finalization, which runs after this method. Predict that final column name
                    // here so the check constraint's SQL references the real column.
                    var tableName = ToSnakeCase(entityType.ClrType.Name);
                    var columnName = ToSnakeCase(property.Name);
                    var quotedValues = string.Join(", ", allowedValues.Select(v => $"'{v}'"));
                    modelBuilder.Entity(entityType.ClrType)
                        .ToTable(t => t.HasCheckConstraint(
                            $"ck_{tableName}_{columnName}_enum",
                            $"\"{columnName}\" IN ({quotedValues})"));
                }

                if (property.ClrType == typeof(string) && property.Name.EndsWith("Json", StringComparison.Ordinal))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasColumnType("jsonb");
                }
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
