using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.ValueObjects;
using ReturnLoad.Infrastructure.Persistence;
using ReturnLoad.Infrastructure.Security;

namespace ReturnLoad.IntegrationTests;

/// <summary>
/// Real relational persistence tests for the M3.5 mapping, on SQLite in-memory (enforces
/// unique indexes, foreign keys, and the concurrency token — which EF InMemory cannot).
/// Verifies mapping round-trips, value-object + enum persistence, Aadhaar encryption at
/// rest, index uniqueness, FK constraints, optimistic concurrency, and soft delete.
/// </summary>
public sealed class PersistenceTests : IDisposable
{
    // 32-byte AES key (base64 of "0123456789abcdef0123456789abcdef").
    private const string TestKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    private readonly SqliteConnection _connection;
    private readonly AesFieldEncryptor _encryptor;

    public PersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _encryptor = new AesFieldEncryptor(Options.Create(new EncryptionOptions { Key = TestKey }));

        using ApplicationDbContext context = CreateContext();
        context.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ApplicationDbContext(options, _encryptor);
    }

    private static UserProfile NewUser(string mobile = "9876543210", string email = "shipper@returnload.test") =>
        UserProfile.Create(Guid.NewGuid(), "Test User", MobileNumber.Create(mobile), EmailAddress.Create(email), Language.Tamil);

    [Fact]
    public async Task UserProfile_round_trips_value_objects_and_enum()
    {
        UserProfile user = NewUser();
        await using (ApplicationDbContext write = CreateContext())
        {
            write.UserProfiles.Add(user);
            await write.SaveChangesAsync();
        }

        await using ApplicationDbContext read = CreateContext();
        UserProfile? loaded = await read.UserProfiles.FirstOrDefaultAsync(u => u.Id == user.Id);

        Assert.NotNull(loaded);
        Assert.Equal("+919876543210", loaded!.Mobile.Value);
        Assert.Equal("shipper@returnload.test", loaded.Email!.Value);
        Assert.Equal(Language.Tamil, loaded.PreferredLanguage);
    }

    [Fact]
    public async Task Vehicle_round_trips_owned_capacity_and_registration()
    {
        Carrier carrier = Carrier.Register("Anna Transports", MobileNumber.Create("9876500000"));
        Vehicle vehicle = Vehicle.Register(carrier.Id, VehicleRegistrationNumber.Create("TN01AB1234"), VehicleType.Reefer,
            VehicleCapacity.Create(Weight.FromTonnes(12m), 40m));

        await using (ApplicationDbContext write = CreateContext())
        {
            write.Carriers.Add(carrier);
            write.Vehicles.Add(vehicle);
            await write.SaveChangesAsync();
        }

        await using ApplicationDbContext read = CreateContext();
        Vehicle? loaded = await read.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicle.Id);

        Assert.NotNull(loaded);
        Assert.Equal("TN01AB1234", loaded!.Registration.Value);
        Assert.Equal(VehicleType.Reefer, loaded.Type);
        Assert.Equal(12000m, loaded.Capacity.MaxPayload.Kilograms);
        Assert.Equal(40m, loaded.Capacity.VolumeCubicMetres);
    }

    [Fact]
    public async Task Enum_is_persisted_as_text()
    {
        UserProfile shipper = NewUser("9811111111", "s2@returnload.test");
        DriverProfile driver = DriverProfile.Register(shipper.Id, DrivingLicenceNumber.Create("TN0120200001234"));
        await using (ApplicationDbContext write = CreateContext())
        {
            write.UserProfiles.Add(shipper);
            write.Drivers.Add(driver);
            await write.SaveChangesAsync();
        }

        await using ApplicationDbContext read = CreateContext();
        await using SqliteCommand cmd = (SqliteCommand)read.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT \"Status\" FROM \"Drivers\" LIMIT 1";
        object? status = await cmd.ExecuteScalarAsync();
        Assert.Equal("Pending", status); // stored as readable text, not an integer
    }

    [Fact]
    public async Task Aadhaar_is_encrypted_at_rest_and_round_trips()
    {
        UserProfile person = NewUser("9822222222", "d@returnload.test");
        DriverProfile driver = DriverProfile.Register(
            person.Id, DrivingLicenceNumber.Create("TN0120200009999"), AadhaarNumber.Create("234567890123"));

        await using (ApplicationDbContext write = CreateContext())
        {
            write.UserProfiles.Add(person);
            write.Drivers.Add(driver);
            await write.SaveChangesAsync();
        }

        // Raw column must NOT contain the plaintext Aadhaar.
        await using (ApplicationDbContext raw = CreateContext())
        {
            await using SqliteCommand cmd = (SqliteCommand)raw.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT \"AadhaarEncrypted\" FROM \"Drivers\" WHERE \"Id\" = $id";
            cmd.Parameters.AddWithValue("$id", driver.Id.ToString());
            string stored = (string)(await cmd.ExecuteScalarAsync())!;
            Assert.DoesNotContain("234567890123", stored);
        }

        // But the application reads it back decrypted.
        await using ApplicationDbContext read = CreateContext();
        DriverProfile loaded = await read.Drivers.FirstAsync(d => d.Id == driver.Id);
        Assert.Equal("234567890123", loaded.Aadhaar!.Value);
        Assert.Equal("XXXX XXXX 0123", loaded.Aadhaar.Masked);
    }

    [Fact]
    public async Task Unique_index_rejects_duplicate_mobile()
    {
        await using ApplicationDbContext context = CreateContext();
        context.UserProfiles.Add(NewUser("9833333333", "a@returnload.test"));
        context.UserProfiles.Add(NewUser("9833333333", "b@returnload.test")); // same mobile

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Foreign_key_rejects_vehicle_with_unknown_carrier()
    {
        await using ApplicationDbContext context = CreateContext();
        Vehicle orphan = Vehicle.Register(Guid.NewGuid(), VehicleRegistrationNumber.Create("TN09ZZ9999"),
            VehicleType.OpenBody, VehicleCapacity.Create(Weight.FromTonnes(5m)));
        context.Vehicles.Add(orphan);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrency_token_detects_a_lost_update()
    {
        UserProfile user = NewUser("9844444444", "c@returnload.test");
        await using (ApplicationDbContext seed = CreateContext())
        {
            seed.UserProfiles.Add(user);
            await seed.SaveChangesAsync();
        }

        await using ApplicationDbContext ctx1 = CreateContext();
        await using ApplicationDbContext ctx2 = CreateContext();
        UserProfile u1 = await ctx1.UserProfiles.FirstAsync(u => u.Id == user.Id);
        UserProfile u2 = await ctx2.UserProfiles.FirstAsync(u => u.Id == user.Id);

        // Both users load as Tamil; each changes it (a real modification) so both are dirty.
        u1.SetPreferredLanguage(Language.English);
        await ctx1.SaveChangesAsync(); // wins; DB Version now advanced

        u2.SetPreferredLanguage(Language.English); // stale — original Version no longer current
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());
    }

    [Fact]
    public async Task Soft_delete_hides_row_but_retains_it()
    {
        UserProfile user = NewUser("9855555555", "e@returnload.test");
        await using (ApplicationDbContext write = CreateContext())
        {
            write.UserProfiles.Add(user);
            await write.SaveChangesAsync();
        }

        await using (ApplicationDbContext delete = CreateContext())
        {
            UserProfile loaded = await delete.UserProfiles.FirstAsync(u => u.Id == user.Id);
            delete.UserProfiles.Remove(loaded);
            await delete.SaveChangesAsync();
        }

        await using ApplicationDbContext read = CreateContext();
        Assert.Null(await read.UserProfiles.FirstOrDefaultAsync(u => u.Id == user.Id)); // filtered out
        Assert.Equal(1, await read.UserProfiles.IgnoreQueryFilters().CountAsync(u => u.Id == user.Id)); // still present
    }

    public void Dispose() => _connection.Dispose();
}
