using System.Linq;
using Content.Shared.IdentityManagement;

namespace Content.Shared._Goobstation.SmartStorageMachines;

public abstract partial class SharedSmartStorageMachineSystem : EntitySystem
{

    [Dependency] private readonly IEntityManager _entityManager = default!;

    public Dictionary<NetEntity, SmartStorageMachineInventoryEntry> GetAllInventory(EntityUid uid, SmartStorageMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        var inventory = component.Inventory;

        return inventory;
    }

    //TODO do we need this? probably not consider removal and just use GetAll
    public Dictionary<NetEntity, SmartStorageMachineInventoryEntry> GetAvailableInventory(EntityUid uid, SmartStorageMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        return GetAllInventory(uid, component);
    }

    public void AddItemToSmartStorage(EntityUid uid, EntityUid newItem, SmartStorageMachineComponent component)
    {
        if (!TryGetNetEntity(newItem, out var item))
            return;

        var itemName = Identity.Name(newItem, _entityManager);

        if (component.Inventory.ContainsKey(item.Value))
            return;

        if (!TryComp<MetaDataComponent>(newItem, out var metaData) || metaData.EntityPrototype is null)
            return;

        var prototypeId =  metaData.EntityPrototype.ID;

        component.Inventory.Add(item.Value, new SmartStorageMachineInventoryEntry(itemName, prototypeId));
    }
}
