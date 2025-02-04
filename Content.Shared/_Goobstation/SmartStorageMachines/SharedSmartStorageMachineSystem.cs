using System.Linq;

namespace Content.Shared._Goobstation.SmartStorageMachines;

public abstract partial class SharedSmartStorageMachineSystem : EntitySystem
{

    public List<EntityUid> GetAllInventory(EntityUid uid, SmartStorageMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        var inventory = component.Inventory.Keys.ToList();

        return inventory;
    }

    public List<EntityUid> GetAvailableInventory(EntityUid uid, SmartStorageMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        return GetAllInventory(uid, component);
    }
}
