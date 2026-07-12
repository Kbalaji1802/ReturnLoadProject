using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.UnitTests.Domain;

public sealed class FleetTests
{
    private static VehicleCapacity TenTonnes() => VehicleCapacity.Create(Weight.FromTonnes(10m));

    private static Vehicle NewVehicle() =>
        Vehicle.Register(Guid.NewGuid(), VehicleRegistrationNumber.Create("TN01AB1234"), VehicleType.OpenBody, TenTonnes());

    [Fact]
    public void VehicleRegistrationNumber_normalises_and_validates()
    {
        Assert.Equal("TN01AB1234", VehicleRegistrationNumber.Create("tn 01 ab 1234").Value);
        Assert.Throws<DomainException>(() => VehicleRegistrationNumber.Create("1234"));
    }

    [Fact]
    public void VehicleCapacity_enforces_positive_payload_and_checks_carry()
    {
        VehicleCapacity capacity = TenTonnes();
        Assert.True(capacity.CanCarry(Weight.FromTonnes(9m)));
        Assert.False(capacity.CanCarry(Weight.FromTonnes(11m)));
        Assert.Throws<DomainException>(() => VehicleCapacity.Create(Weight.FromTonnes(1m), volumeCubicMetres: 0m));
    }

    [Fact]
    public void Vehicle_registers_as_draft_and_raises_event()
    {
        Vehicle vehicle = NewVehicle();
        Assert.Equal(VehicleStatus.Draft, vehicle.Status);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleRegistered);
    }

    [Fact]
    public void Vehicle_cannot_activate_with_invalid_documents()
    {
        Vehicle vehicle = NewVehicle();
        Assert.Throws<DomainException>(() => vehicle.Activate(mandatoryDocumentsValid: false));
        Assert.Equal(VehicleStatus.Draft, vehicle.Status);
    }

    [Fact]
    public void Vehicle_activates_when_documents_valid()
    {
        Vehicle vehicle = NewVehicle();
        vehicle.Activate(mandatoryDocumentsValid: true);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
    }

    [Fact]
    public void Vehicle_suspended_cannot_be_activated()
    {
        Vehicle vehicle = NewVehicle();
        vehicle.Suspend();
        Assert.Throws<DomainException>(() => vehicle.Activate(mandatoryDocumentsValid: true));
    }

    [Fact]
    public void Vehicle_requires_a_carrier()
    {
        Assert.Throws<DomainException>(() =>
            Vehicle.Register(Guid.Empty, VehicleRegistrationNumber.Create("TN01AB1234"), VehicleType.OpenBody, TenTonnes()));
    }
}
