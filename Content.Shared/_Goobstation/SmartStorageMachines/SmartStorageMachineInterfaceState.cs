using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.SmartStorageMachines
{
    [Serializable, NetSerializable]
    public sealed class SmartStorageMachineEjectMessage : BoundUserInterfaceMessage
    {
        public readonly EntityUid Entity;
        public SmartStorageMachineEjectMessage(EntityUid entity)
        {
            Entity = entity;
        }
    }

    [Serializable, NetSerializable]
    public enum SmartStorageMachineUiKey
    {
        Key,
    }
}
