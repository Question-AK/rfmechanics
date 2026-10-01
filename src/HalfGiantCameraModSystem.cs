using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace rfmechanics;

public class HalfGiantCameraModSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private PlayerRace lastRace;
    private bool pending;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        api.Event.RegisterGameTickListener(OnTick, 200);
    }

    private void OnTick(float dt)
    {
        IClientPlayer? player = capi?.World.Player;
        PlayerRaceBehavior? identity = player?.Entity?.GetBehavior<PlayerRaceBehavior>();
        if (player == null || identity == null) return;

        // Session start reads as a change too: the engine resets the camera distance to 3 every session (Camera.cs).
        if (identity.Race != lastRace)
        {
            pending = identity.Race == PlayerRace.HalfGiant;
            lastRace = identity.Race;
        }

        var cfg = RFMechanicsModSystem.Config;
        if (!pending || cfg?.EnableHalfGiantCamera != true || player.CameraMode == EnumCameraMode.FirstPerson) return;

        // Vanilla zoomout steps 10 instead of 1 while Left Control (default sprint) is held.
        if (capi!.Input.KeyboardKeyState[(int)GlKeys.ControlLeft]) return;

        HotKey? zoomOut = capi.Input.GetHotKeyByCode("zoomout");
        if (zoomOut?.Handler == null) return;

        for (int i = 0; i < cfg.HalfGiantCameraZoomOutSteps; i++) zoomOut.Handler(zoomOut.CurrentMapping);
        pending = false;
    }
}
