using Content.Shared._Goobstation.SmartStorageMachines;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client._Goobstation.SmartStorageMachines;

public sealed class SmartStorageMachineSystem : SharedSmartStorageMachineSystem
{
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmartStorageMachineComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<SmartStorageMachineComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        SubscribeLocalEvent<SmartStorageMachineComponent, AfterAutoHandleStateEvent>(OnSmartStorageAfterState);
    }

    private void OnSmartStorageAfterState(EntityUid uid, SmartStorageMachineComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (_uiSystem.TryGetOpenUi<SmartStorageMachineBoundUserInterface>(uid, SmartStorageMachineUiKey.Key, out var bui))
        {
            bui.Refresh();
        }
    }

    private void OnAnimationCompleted(EntityUid uid, SmartStorageMachineComponent component, AnimationCompletedEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
            !_appearanceSystem.TryGetData<SmartStorageMachineVisualState>(uid, SmartStorageMachineVisuals.VisualState, out var visualState, appearance))
        {
            visualState = SmartStorageMachineVisualState.Normal;
        }

        UpdateAppearance(uid, visualState, component, sprite);
    }

    private void OnAppearanceChange(EntityUid uid, SmartStorageMachineComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.AppearanceData.TryGetValue(SmartStorageMachineVisuals.VisualState, out var visualStateObject) ||
            visualStateObject is not SmartStorageMachineVisualState visualState)
        {
            visualState = SmartStorageMachineVisualState.Normal;
        }

        UpdateAppearance(uid, visualState, component, args.Sprite);
    }

    private void UpdateAppearance(EntityUid uid, SmartStorageMachineVisualState visualState, SmartStorageMachineComponent component, SpriteComponent sprite)
    {
        SetLayerState(SmartStorageMachineVisualLayers.Base, component.OffState, sprite);

        switch (visualState)
        {
            case SmartStorageMachineVisualState.Normal:
                SetLayerState(SmartStorageMachineVisualLayers.BaseUnshaded, component.NormalState, sprite);
                SetLayerState(SmartStorageMachineVisualLayers.Screen, component.ScreenState, sprite);
                break;

            case SmartStorageMachineVisualState.Deny:
                if (component.LoopDenyAnimation)
                    SetLayerState(SmartStorageMachineVisualLayers.BaseUnshaded, component.DenyState, sprite);
                else
                    PlayAnimation(uid, SmartStorageMachineVisualLayers.BaseUnshaded, component.DenyState, component.DenyDelay, sprite);

                SetLayerState(SmartStorageMachineVisualLayers.Screen, component.ScreenState, sprite);
                break;

            case SmartStorageMachineVisualState.Eject:
                PlayAnimation(uid, SmartStorageMachineVisualLayers.BaseUnshaded, component.EjectState, component.EjectDelay, sprite);
                SetLayerState(SmartStorageMachineVisualLayers.Screen, component.ScreenState, sprite);
                break;

            case SmartStorageMachineVisualState.Broken:
                HideLayers(sprite);
                SetLayerState(SmartStorageMachineVisualLayers.Base, component.BrokenState, sprite);
                break;

            case SmartStorageMachineVisualState.Off:
                HideLayers(sprite);
                break;
        }
    }

    private static void SetLayerState(SmartStorageMachineVisualLayers layer, string? state, SpriteComponent sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        sprite.LayerSetVisible(layer, true);
        sprite.LayerSetAutoAnimated(layer, true);
        sprite.LayerSetState(layer, state);
    }

    private void PlayAnimation(EntityUid uid, SmartStorageMachineVisualLayers layer, string? state, float animationTime, SpriteComponent sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        if (!_animationPlayer.HasRunningAnimation(uid, state))
        {
            var animation = GetAnimation(layer, state, animationTime);
            sprite.LayerSetVisible(layer, true);
            _animationPlayer.Play(uid, animation, state);
        }
    }

    private static Animation GetAnimation(SmartStorageMachineVisualLayers layer, string state, float animationTime)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(animationTime),
            AnimationTracks =
                {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = layer,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state, 0f)
                        }
                    }
                }
        };
    }

    private static void HideLayers(SpriteComponent sprite)
    {
        HideLayer(SmartStorageMachineVisualLayers.BaseUnshaded, sprite);
        HideLayer(SmartStorageMachineVisualLayers.Screen, sprite);
    }

    private static void HideLayer(SmartStorageMachineVisualLayers layer, SpriteComponent sprite)
    {
        if (!sprite.LayerMapTryGet(layer, out var actualLayer))
            return;

        sprite.LayerSetVisible(actualLayer, false);
    }
}
