using System.Linq;
using System.Numerics;
using Content.Server.Advertise;
using Content.Server.Advertise.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Emp;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Popups;
using Content.Server.Popups;
using Content.Shared.Power;
using Content.Shared.Throwing;
using Content.Shared.UserInterface;
using Content.Shared._Goobstation.SmartStorageMachines;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SmartStorageMachines
{
    public sealed class SmartStorageMachineSystem : SharedSmartStorageMachineSystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AccessReaderSystem _accessReader = default!;
        [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
        [Dependency] private readonly ThrowingSystem _throwingSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SpeakOnUIClosedSystem _speakOnUIClosed = default!;
        [Dependency] private readonly SharedPointLightSystem _light = default!;
        [Dependency] private readonly AudioSystem _audio = default!;
        [Dependency] private readonly PopupSystem _popup = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SmartStorageMachineComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<SmartStorageMachineComponent, BreakageEventArgs>(OnBreak);
            SubscribeLocalEvent<SmartStorageMachineComponent, GotEmaggedEvent>(OnEmagged);
            SubscribeLocalEvent<SmartStorageMachineComponent, DamageChangedEvent>(OnDamageChanged);
            SubscribeLocalEvent<SmartStorageMachineComponent, EmpPulseEvent>(OnEmpPulse);

            SubscribeLocalEvent<SmartStorageMachineComponent, ActivatableUIOpenAttemptEvent>(OnActivatableUIOpenAttempt);

            Subs.BuiEvents<SmartStorageMachineComponent>(SmartStorageMachineUiKey.Key, subs =>
            {
                subs.Event<SmartStorageMachineEjectMessage>(OnInventoryEjectMessage);
            });

            SubscribeLocalEvent<SmartStorageMachineComponent, SmartStorageMachineSelfDispenseEvent>(OnSelfDispense);

        }

        private void OnMapInit(EntityUid uid, SmartStorageMachineComponent component, MapInitEvent args)
        {
            if (HasComp<ApcPowerReceiverComponent>(uid))
            {
                TryUpdateVisualState(uid, component);
            }
        }

        private void OnActivatableUIOpenAttempt(EntityUid uid, SmartStorageMachineComponent component, ActivatableUIOpenAttemptEvent args)
        {
            if (component.Broken)
                args.Cancel();
        }

        private void OnInventoryEjectMessage(EntityUid uid, SmartStorageMachineComponent component, SmartStorageMachineEjectMessage args)
        {
            if (!this.IsPowered(uid, EntityManager))
                return;

            if (args.Actor is not { Valid: true } entity || Deleted(entity))
                return;

            AuthorizedVend(uid, entity, args.Entity, component);
        }

        private void OnPowerChanged(EntityUid uid, SmartStorageMachineComponent component, ref PowerChangedEvent args)
        {
            TryUpdateVisualState(uid, component);
        }

        private void OnBreak(EntityUid uid, SmartStorageMachineComponent vendComponent, BreakageEventArgs eventArgs)
        {
            vendComponent.Broken = true;
            TryUpdateVisualState(uid, vendComponent);
        }

        private void OnEmagged(EntityUid uid, SmartStorageMachineComponent component, ref GotEmaggedEvent args)
        {
            args.Handled = true;
        }

        private void OnDamageChanged(EntityUid uid, SmartStorageMachineComponent component, DamageChangedEvent args)
        {
            if (!args.DamageIncreased && component.Broken)
            {
                component.Broken = false;
                TryUpdateVisualState(uid, component);
                return;
            }

            if (component.Broken || component.DispenseOnHitCoolingDown ||
                component.DispenseOnHitChance == null || args.DamageDelta == null)
                return;

            if (args.DamageIncreased && args.DamageDelta.GetTotal() >= component.DispenseOnHitThreshold &&
                _random.Prob(component.DispenseOnHitChance.Value))
            {
                if (component.DispenseOnHitCooldown > 0f)
                    component.DispenseOnHitCoolingDown = true;
                EjectRandom(uid, throwItem: true, forceEject: true, component);
            }
        }

        private void OnSelfDispense(EntityUid uid, SmartStorageMachineComponent component, SmartStorageMachineSelfDispenseEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            EjectRandom(uid, throwItem: true, forceEject: false, component);
        }

        private void OnDoAfter(EntityUid uid, SmartStorageMachineComponent component, DoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || args.Args.Used == null)
                return;

            //TODO check if item has a listed tag
            //if (!TryComp<SmartStorageMachineRestockComponent>(args.Args.Used, out var restockComponent))
            //{
            //    Log.Error($"{ToPrettyString(args.Args.User)} tried to restock {ToPrettyString(uid)} with {ToPrettyString(args.Args.Used.Value)} which did not have a SmartStorageMachineRestockComponent.");
            //    return;
            //}

            //TODO add function for adding individual items

            //TODO change to more appropriate sounds/message
            //_popup.PopupEntity(Loc.GetString("vending-machine-restock-done", ("this", args.Args.Used), ("user", args.Args.User), ("target", uid)), args.Args.User, PopupType.Medium);

            //_audio.PlayPvs(restockComponent.SoundRestockDone, uid, AudioParams.Default.WithVolume(-2f).WithVariation(0.2f));

            args.Handled = true;
        }

        /// <summary>
        /// Sets the <see cref="SmartStorageMachineComponent.CanShoot"/> property of the SmartStorage machine.
        /// </summary>
        public void SetShooting(EntityUid uid, bool canShoot, SmartStorageMachineComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.CanShoot = canShoot;
        }

        public void Deny(EntityUid uid, SmartStorageMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            if (vendComponent.Denying)
                return;

            vendComponent.Denying = true;
            _audio.PlayPvs(vendComponent.SoundDeny, uid, AudioParams.Default.WithVolume(-2f));
            TryUpdateVisualState(uid, vendComponent);
        }

        /// <summary>
        /// Checks if the user is authorized to access the item
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="sender">Entity trying to use the SmartStorage machine</param>
        /// <param name="vendComponent"></param>
        public bool IsAuthorized(EntityUid uid, EntityUid sender, NetEntity item, SmartStorageMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return false;

            if (!TryComp<AccessReaderComponent>(GetEntity(item), out var accessReader))
                return true;

            //TODO give each potential storage item accessReader? Possibly on insertion (that works)
            if (_accessReader.IsAllowed(sender, uid, accessReader) || HasComp<EmaggedComponent>(uid))
                return true;

            _popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-access-denied"), uid);
            Deny(uid, vendComponent);
            return false;
        }

        /// <summary>
        /// Tries to eject the provided item. Will do nothing if the SmartStorage machine is incapable of ejecting, already ejecting
        /// or the item doesn't exist in its inventory.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="type">The type of inventory the item is from</param>
        /// <param name="item">The item entity</param>
        /// <param name="throwItem">Whether the item should be thrown in a random direction after ejection</param>
        /// <param name="vendComponent"></param>
        public void TryEjectVendorItem(EntityUid uid, NetEntity item, bool throwItem, SmartStorageMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            if (vendComponent.Ejecting || vendComponent.Broken || !this.IsPowered(uid, EntityManager))
            {
                return;
            }

            var entry = GetEntry(uid, item, vendComponent);

            if (entry == null)
            {
                _popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-invalid-item"), uid);
                Deny(uid, vendComponent);
                return;
            }

            if (entry.Amount <= 0)
            {
                _popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-out-of-stock"), uid);
                Deny(uid, vendComponent);
                return;
            }

            if (string.IsNullOrEmpty(entry.ID))
                return;


            // Start Ejecting, and prevent users from ordering while anim playing
            vendComponent.Ejecting = true;
            vendComponent.NextItemToEject = entry.ID;
            vendComponent.ThrowNextItem = throwItem;

            if (TryComp(uid, out SpeakOnUIClosedComponent? speakComponent))
                _speakOnUIClosed.TrySetFlag((uid, speakComponent));

            entry.Amount--;
            Dirty(uid, vendComponent);
            TryUpdateVisualState(uid, vendComponent);
            _audio.PlayPvs(vendComponent.SoundVend, uid);
        }

        /// <summary>
        /// Checks whether the user is authorized to access the item in the smart storage machine
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="sender">Entity that is trying to use the SmartStorage machine</param>
        /// <param name="type">The type of inventory the item is from</param>
        /// <param name="item">The item being requested</param>
        /// <param name="component"></param>
        public void AuthorizedVend(EntityUid uid, EntityUid sender, NetEntity item, SmartStorageMachineComponent component)
        {
            if (IsAuthorized(uid, sender, item, component))
            {
                TryEjectVendorItem(uid, item, component.CanShoot, component);
            }
        }

        /// <summary>
        /// Tries to update the visuals of the component based on its current state.
        /// </summary>
        public void TryUpdateVisualState(EntityUid uid, SmartStorageMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            var finalState = SmartStorageMachineVisualState.Normal;
            if (vendComponent.Broken)
            {
                finalState = SmartStorageMachineVisualState.Broken;
            }
            else if (vendComponent.Ejecting)
            {
                finalState = SmartStorageMachineVisualState.Eject;
            }
            else if (vendComponent.Denying)
            {
                finalState = SmartStorageMachineVisualState.Deny;
            }
            else if (!this.IsPowered(uid, EntityManager))
            {
                finalState = SmartStorageMachineVisualState.Off;
            }

            if (_light.TryGetLight(uid, out var pointlight))
            {
                var lightState = finalState != SmartStorageMachineVisualState.Broken && finalState != SmartStorageMachineVisualState.Off;
                _light.SetEnabled(uid, lightState, pointlight);
            }

            _appearanceSystem.SetData(uid, SmartStorageMachineVisuals.VisualState, finalState);
        }

        /// <summary>
        /// Ejects a random item from the available stock. Will do nothing if the SmartStorage machine is empty.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="throwItem">Whether to throw the item in a random direction after dispensing it.</param>
        /// <param name="forceEject">Whether to skip the regular ejection checks and immediately dispense the item without animation.</param>
        /// <param name="vendComponent"></param>
        public void EjectRandom(EntityUid uid, bool throwItem, bool forceEject = false, SmartStorageMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            var availableItems = GetAvailableInventory(uid, vendComponent);
            if (availableItems.Count <= 0)
                return;

            var item = _random.Pick(availableItems);

            if (forceEject)
            {
                //TODO fix GetAvailableInventory to return InventoryEntry again (AND entity uid)
                //vendComponent.NextItemToEject = item;
                vendComponent.ThrowNextItem = throwItem;
                var entry = GetEntry(uid, item.Key, vendComponent);
                if (entry != null)
                    entry.Amount--;
                EjectItem(uid, vendComponent, forceEject);
            }
            else
            {
                TryEjectVendorItem(uid, item.Key, throwItem, vendComponent);
            }
        }

        private void EjectItem(EntityUid uid, SmartStorageMachineComponent? vendComponent = null, bool forceEject = false)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            // No need to update the visual state because we never changed it during a forced eject
            if (!forceEject)
                TryUpdateVisualState(uid, vendComponent);

            if (string.IsNullOrEmpty(vendComponent.NextItemToEject))
            {
                vendComponent.ThrowNextItem = false;
                return;
            }

            // Default spawn coordinates
            var spawnCoordinates = Transform(uid).Coordinates;

            var ent = Spawn(vendComponent.NextItemToEject, spawnCoordinates);

            if (vendComponent.ThrowNextItem)
            {
                var range = vendComponent.NonLimitedEjectRange;
                var direction = new Vector2(_random.NextFloat(-range, range), _random.NextFloat(-range, range));
                _throwingSystem.TryThrow(ent, direction, vendComponent.NonLimitedEjectForce);
            }

            vendComponent.NextItemToEject = null;
            vendComponent.ThrowNextItem = false;
        }

        private SmartStorageMachineInventoryEntry? GetEntry(EntityUid uid, NetEntity entry, SmartStorageMachineComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return null;

            return component.Inventory.GetValueOrDefault(entry);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<SmartStorageMachineComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.Ejecting)
                {
                    comp.EjectAccumulator += frameTime;
                    if (comp.EjectAccumulator >= comp.EjectDelay)
                    {
                        comp.EjectAccumulator = 0f;
                        comp.Ejecting = false;

                        EjectItem(uid, comp);
                    }
                }

                if (comp.Denying)
                {
                    comp.DenyAccumulator += frameTime;
                    if (comp.DenyAccumulator >= comp.DenyDelay)
                    {
                        comp.DenyAccumulator = 0f;
                        comp.Denying = false;

                        TryUpdateVisualState(uid, comp);
                    }
                }

                if (comp.DispenseOnHitCoolingDown)
                {
                    comp.DispenseOnHitAccumulator += frameTime;
                    if (comp.DispenseOnHitAccumulator >= comp.DispenseOnHitCooldown)
                    {
                        comp.DispenseOnHitAccumulator = 0f;
                        comp.DispenseOnHitCoolingDown = false;
                    }
                }
            }
            var disabled = EntityQueryEnumerator<EmpDisabledComponent, SmartStorageMachineComponent>();
            while (disabled.MoveNext(out var uid, out _, out var comp))
            {
                if (comp.NextEmpEject < _timing.CurTime)
                {
                    EjectRandom(uid, true, false, comp);
                    comp.NextEmpEject += TimeSpan.FromSeconds(5 * comp.EjectDelay);
                }
            }
        }

        private void OnEmpPulse(EntityUid uid, SmartStorageMachineComponent component, ref EmpPulseEvent args)
        {
            if (!component.Broken && this.IsPowered(uid, EntityManager))
            {
                args.Affected = true;
                args.Disabled = true;
                component.NextEmpEject = _timing.CurTime;
            }
        }
    }
}
