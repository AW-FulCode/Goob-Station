using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Goobstation.SmartStorageMachines
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
    public sealed partial class SmartStorageMachineComponent : Component
    {

        /// <summary>
        /// Used by the server to determine how long the smartstorage machine stays in the "Deny" state.
        /// Used by the client to determine how long the deny animation should be played.
        /// </summary>
        [DataField]
        public float DenyDelay = 2.0f;

        /// <summary>
        /// Used by the server to determine how long the smartstorage machine stays in the "Eject" state.
        /// The selected item is dispensed afer this delay.
        /// Used by the client to determine how long the deny animation should be played.
        /// </summary>
        [DataField]
        public float EjectDelay = 1.2f;

        [DataField, AutoNetworkedField]
        public Dictionary<NetEntity, SmartStorageMachineInventoryEntry> Inventory = new();

        [DataField]
        public List<ItemSlot> StorageSlots = new List<ItemSlot>();
        [DataField]
        public List<string> StorageSlotIds = new List<string>();

        public static string BaseStorageSlotId = "SmartStorage-storageSlot";

        [DataField("numStorageSlots")]
        public int NumSlots = 200;

        public bool Ejecting;
        public bool Denying;
        public bool DispenseOnHitCoolingDown;

        public String? NextItemToEject;

        public bool Broken;

        /// <summary>
        /// When true, will forcefully throw any object it dispenses
        /// </summary>
        [DataField("speedLimiter")]
        public bool CanShoot = false;

        public bool ThrowNextItem = false;

        /// <summary>
        ///     The chance that a smartstorage machine will randomly dispense an item on hit.
        ///     Chance is 0 if null.
        /// </summary>
        [DataField("dispenseOnHitChance")]
        public float? DispenseOnHitChance;

        /// <summary>
        ///     The minimum amount of damage that must be done per hit to have a chance
        ///     of dispensing an item.
        /// </summary>
        [DataField("dispenseOnHitThreshold")]
        public float? DispenseOnHitThreshold;

        /// <summary>
        ///     Amount of time in seconds that need to pass before damage can cause a smartstorage machine to eject again.
        ///     This value is separate to <see cref="SmartStorageMachineComponent.EjectDelay"/> because that value might be
        ///     0 for a smartstorage machine for legitimate reasons (no desired delay/no eject animation)
        ///     and can be circumvented with forced ejections.
        /// </summary>
        [DataField("dispenseOnHitCooldown")]
        public float? DispenseOnHitCooldown = 1.0f;

        /// <summary>
        ///     Sound that plays when ejecting an item
        /// </summary>
        [DataField("soundVend")]
        // Grabbed from: https://github.com/tgstation/tgstation/blob/d34047a5ae911735e35cd44a210953c9563caa22/sound/machines/machine_vend.ogg
        public SoundSpecifier SoundVend = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg")
        {
            Params = new AudioParams
            {
                Volume = -4f,
                Variation = 0.15f
            }
        };

        /// <summary>
        ///     Sound that plays when an item can't be ejected
        /// </summary>
        [DataField("soundDeny")]
        // Yoinked from: https://github.com/discordia-space/CEV-Eris/blob/35bbad6764b14e15c03a816e3e89aa1751660ba9/sound/machines/Custom_deny.ogg
        public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

        public float NonLimitedEjectForce = 7.5f;

        public float NonLimitedEjectRange = 5f;

        public float EjectAccumulator = 0f;
        public float DenyAccumulator = 0f;
        public float DispenseOnHitAccumulator = 0f;

        /// <summary>
        ///     While disabled by EMP it randomly ejects items
        /// </summary>
        [DataField("nextEmpEject", customTypeSerializer: typeof(TimeOffsetSerializer))]
        public TimeSpan NextEmpEject = TimeSpan.Zero;

        #region Client Visuals
        /// <summary>
        /// RSI state for when the smartstorage machine is unpowered.
        /// Will be displayed on the layer <see cref="SmartStorageMachineVisualLayers.Base"/>
        /// </summary>
        [DataField("offState")]
        public string? OffState;

        /// <summary>
        /// RSI state for the screen of the smartstorage machine
        /// Will be displayed on the layer <see cref="SmartStorageMachineVisualLayers.Screen"/>
        /// </summary>
        [DataField("screenState")]
        public string? ScreenState;

        /// <summary>
        /// RSI state for the smartstorage machine's normal state. Usually a looping animation.
        /// Will be displayed on the layer <see cref="SmartStorageMachineVisualLayers.BaseUnshaded"/>
        /// </summary>
        [DataField("normalState")]
        public string? NormalState;

        /// <summary>
        /// RSI state for the smartstorage machine's eject animation.
        /// Will be displayed on the layer <see cref="SmartStorageMachineVisualLayers.BaseUnshaded"/>
        /// </summary>
        [DataField("ejectState")]
        public string? EjectState;

        /// <summary>
        /// RSI state for the smartstorage machine's deny animation. Will either be played once as sprite flick
        /// or looped depending on how <see cref="LoopDenyAnimation"/> is set.
        /// Will be displayed on the layer <see cref="SmartStorageMachineVisualLayers.BaseUnshaded"/>
        /// </summary>
        [DataField("denyState")]
        public string? DenyState;

        /// <summary>
        /// RSI state for when the smartstorage machine is unpowered.
        /// Will be displayed on the layer <see cref="SmartStorageMachineVisualLayers.Base"/>
        /// </summary>
        [DataField("brokenState")]
        public string? BrokenState;

        /// <summary>
        /// If set to <c>true</c> (default) will loop the animation of the <see cref="DenyState"/> for the duration
        /// of <see cref="SmartStorageMachineComponent.DenyDelay"/>. If set to <c>false</c> will play a sprite
        /// flick animation for the state and then linger on the final frame until the end of the delay.
        /// </summary>
        [DataField("loopDeny")]
        public bool LoopDenyAnimation = true;
        #endregion
    }

    [Serializable, NetSerializable]
    public sealed class SmartStorageMachineInventoryEntry
    {
        public string ID;
        public string Name;
        public bool PublicAccess;
        
        public SmartStorageMachineInventoryEntry(string name, string id)
        {
            ID = id;
            Name = name;
            PublicAccess = false;
        }
    }

    [Serializable, NetSerializable]
    public enum SmartStorageMachineVisuals
    {
        VisualState
    }

    [Serializable, NetSerializable]
    public enum SmartStorageMachineVisualState
    {
        Normal,
        Off,
        Broken,
        Eject,
        Deny,
    }

    public enum SmartStorageMachineVisualLayers : byte
    {
        /// <summary>
        /// Off / Broken. The other layers will overlay this if the machine is on.
        /// </summary>
        Base,
        /// <summary>
        /// Normal / Deny / Eject
        /// </summary>
        BaseUnshaded,
        /// <summary>
        /// Screens that are persistent (where the machine is not off or broken)
        /// </summary>
        Screen
    }

    [Serializable, NetSerializable]
    public enum ContrabandWireKey : byte
    {
        StatusKey,
        TimeoutKey
    }

    [Serializable, NetSerializable]
    public enum EjectWireKey : byte
    {
        StatusKey,
    }

    public sealed partial class SmartStorageMachineSelfDispenseEvent : InstantActionEvent
    {

    };
}
