using System.Linq;

namespace Content.Shared._Goobstation.SmartStorageMachines;

public abstract partial class SharedSmartStorageMachineSystem : EntitySystem
{

    public Dictionary<NetEntity, SmartStorageMachineInventoryEntry> GetAllInventory(EntityUid uid, SmartStorageMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        var inventory = component.Inventory;

        return inventory;
    }

    //TODO do we need this? probably not consider removal
    public Dictionary<NetEntity, SmartStorageMachineInventoryEntry> GetAvailableInventory(EntityUid uid, SmartStorageMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        return GetAllInventory(uid, component);
    }
}
