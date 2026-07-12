using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.UnitTests.Domain;

public sealed class IdentityTests
{
    private static DrivingLicenceNumber Licence() => DrivingLicenceNumber.Create("TN0120200001234");

    [Fact]
    public void DrivingLicenceNumber_normalises_and_validates()
    {
        Assert.Equal("TN0120200001234", DrivingLicenceNumber.Create("tn01 2020000 1234").Value);
        Assert.Throws<DomainException>(() => DrivingLicenceNumber.Create("XY"));
    }

    [Fact]
    public void AadhaarNumber_validates_and_masks()
    {
        AadhaarNumber aadhaar = AadhaarNumber.Create("2234 5678 9012");
        Assert.Equal("XXXX XXXX 9012", aadhaar.Masked);
        Assert.Equal("XXXX XXXX 9012", aadhaar.ToString()); // never renders raw
        Assert.Throws<DomainException>(() => AadhaarNumber.Create("1234")); // wrong length
        Assert.Throws<DomainException>(() => AadhaarNumber.Create("023456789012")); // starts with 0/1
    }

    [Fact]
    public void DriverProfile_registers_pending_and_raises_event()
    {
        DriverProfile driver = DriverProfile.Register(Guid.NewGuid(), Licence());

        Assert.Equal(DriverStatus.Pending, driver.Status);
        Assert.False(driver.IsTransactable);
        Assert.Contains(driver.DomainEvents, e => e is DriverRegistered);
    }

    [Fact]
    public void DriverProfile_verify_activates_and_raises_event()
    {
        DriverProfile driver = DriverProfile.Register(Guid.NewGuid(), Licence());

        driver.MarkVerified();

        Assert.Equal(DriverStatus.Active, driver.Status);
        Assert.True(driver.IsTransactable);
        Assert.Contains(driver.DomainEvents, e => e is DriverVerified);
    }

    [Fact]
    public void DriverProfile_blocked_cannot_be_verified_or_reinstated()
    {
        DriverProfile driver = DriverProfile.Register(Guid.NewGuid(), Licence());
        driver.Block();

        Assert.Equal(DriverStatus.Blocked, driver.Status);
        Assert.Throws<DomainException>(() => driver.MarkVerified());
        Assert.Throws<DomainException>(() => driver.Reinstate());
    }

    [Fact]
    public void DriverProfile_only_pending_can_be_verified()
    {
        DriverProfile driver = DriverProfile.Register(Guid.NewGuid(), Licence());
        driver.MarkVerified();
        Assert.Throws<DomainException>(() => driver.MarkVerified()); // already active
    }

    [Fact]
    public void Carrier_registers_pending_and_blocks_activation_when_blocked()
    {
        Carrier carrier = Carrier.Register("Anna Transports", MobileNumber.Create("9876543210"));
        Assert.Equal(CarrierStatus.Pending, carrier.Status);
        Assert.Contains(carrier.DomainEvents, e => e is CarrierRegistered);

        carrier.Block();
        Assert.Throws<DomainException>(() => carrier.Activate());
    }

    [Fact]
    public void Association_cannot_be_reactivated_after_revoke()
    {
        Association association = Association.Create(Guid.NewGuid(), Guid.NewGuid(), AssociationRole.Driver);
        association.Revoke();
        Assert.Throws<DomainException>(() => association.Activate());
    }

    [Fact]
    public void UserProfile_requires_auth_link_and_name()
    {
        Assert.Throws<DomainException>(() =>
            UserProfile.Create(Guid.Empty, "A", MobileNumber.Create("9876543210")));
        Assert.Throws<DomainException>(() =>
            UserProfile.Create(Guid.NewGuid(), "  ", MobileNumber.Create("9876543210")));
    }
}
