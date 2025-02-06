using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.SmartStorageMachines
{
    [Serializable, NetSerializable]
    public sealed class SmartStorageMachineEjectMessage : BoundUserInterfaceMessage
    {
        public readonly NetEntity Entity;
        public SmartStorageMachineEjectMessage(NetEntity entity)
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
