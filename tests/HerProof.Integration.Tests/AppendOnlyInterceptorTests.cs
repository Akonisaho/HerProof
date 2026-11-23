using HerProof.Domain.Consent;
using HerProof.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>Proves docs/02 rule 1 / CLAUDE.md rule 8: append-only entities can be inserted but
/// never updated or deleted through the DbContext, regardless of caller.</summary>
public class AppendOnlyInterceptorTests
{
    private static HerProofDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HerProofDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AppendOnlyInterceptor())
            .Options;
        return new HerProofDbContext(options);
    }

    [Fact]
    public async Task Insert_of_append_only_entity_succeeds()
    {
        await using var db = CreateContext();
        var record = new ConsentRecord(Guid.NewGuid(), "data-processing", "v1", granted: true, DateTime.UtcNow);

        db.ConsentRecords.Add(record);
        await db.SaveChangesAsync();

        Assert.Single(db.ConsentRecords);
    }

    [Fact]
    public async Task Update_of_append_only_entity_is_rejected()
    {
        await using var db = CreateContext();
        var record = new ConsentRecord(Guid.NewGuid(), "data-processing", "v1", granted: true, DateTime.UtcNow);
        db.ConsentRecords.Add(record);
        await db.SaveChangesAsync();

        db.Entry(record).State = EntityState.Modified;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Delete_of_append_only_entity_is_rejected()
    {
        await using var db = CreateContext();
        var record = new ConsentRecord(Guid.NewGuid(), "data-processing", "v1", granted: true, DateTime.UtcNow);
        db.ConsentRecords.Add(record);
        await db.SaveChangesAsync();

        db.ConsentRecords.Remove(record);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
