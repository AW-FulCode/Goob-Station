using Content.Server.Wires;
using Content.Shared._Goobstation.SmartStorageMachines;
using Content.Shared.Wires;

namespace Content.Server._Goobstation.SmartStorageMachines;

public sealed partial class SmartStorageMachineEjectItemWireAction : ComponentWireAction<SmartStorageMachineComponent>
{
    private SmartStorageMachineSystem _SmartStorageMachineSystem = default!;

    public override Color Color { get; set; } = Color.Red;
    public override string Name { get; set; } = "wire-name-SmartStorage-eject";

    public override object? StatusKey { get; } = EjectWireKey.StatusKey;

    public override StatusLightState? GetLightState(Wire wire, SmartStorageMachineComponent comp)
        => comp.CanShoot ? StatusLightState.BlinkingFast : StatusLightState.On;

    public override void Initialize()
    {
        base.Initialize();

        _SmartStorageMachineSystem = EntityManager.System<SmartStorageMachineSystem>();
    }

    public override bool Cut(EntityUid user, Wire wire, SmartStorageMachineComponent SmartStorage)
    {
        _SmartStorageMachineSystem.SetShooting(wire.Owner, true, SmartStorage);
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, SmartStorageMachineComponent SmartStorage)
    {
        _SmartStorageMachineSystem.SetShooting(wire.Owner, false, SmartStorage);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, SmartStorageMachineComponent SmartStorage)
    {
        _SmartStorageMachineSystem.EjectRandom(wire.Owner, true, vendComponent: SmartStorage);
    }
}
