using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace rfmechanics;

public class HalfGiantCameraModSystem : ModSystem
{
    // Client-side memory: the engine resets the distance to 3 every session (Camera.cs) and never saves it.
    private const string StateFile = "rfmechanics-halfgiant-camera.json";

    private ICoreClientAPI? capi;
    private HalfGiantCameraState state = new();
    private PlayerRace lastRace;
    private bool pending;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        try { state = api.LoadModConfig<HalfGiantCameraState>(StateFile) ?? new(); }
        catch (Exception e) { api.Logger.Warning("[rfmechanics] Half-Giant camera memory unreadable, starting fresh: {0}", e.Message); }
        api.Event.RegisterGameTickListener(OnTick, 200);
    }

    private void OnTick(float dt)
    {
        IClientPlayer? player = capi?.World.Player;
        PlayerRaceBehavior? identity = player?.Entity?.GetBehavior<PlayerRaceBehavior>();
        if (player == null || identity == null) return;

        if (identity.Race != lastRace)
        {
            pending = identity.Race == PlayerRace.HalfGiant;
            lastRace = identity.Race;
        }

        PlayerCamera? camera = (capi!.World as ClientMain)?.MainCamera;
        var cfg = RFMechanicsModSystem.Config;
        if (identity.Race != PlayerRace.HalfGiant || cfg?.EnableHalfGiantCamera != true || camera == null
            || player.CameraMode == EnumCameraMode.FirstPerson) return;

        if (pending) Restore(camera, cfg);
        else Remember(camera);
    }

    private void Restore(PlayerCamera camera, RFMechanicsConfig cfg)
    {
        // Vanilla zoom steps 10 instead of 1 while Left Control (default sprint) is held.
        if (capi!.Input.KeyboardKeyState[(int)GlKeys.ControlLeft]) return;

        int current = (int)Math.Round(camera.Tppcameradistance);
        int target = Math.Clamp(state.Distance ?? current + cfg.HalfGiantCameraZoomOutSteps,
            camera.TppCameraDistanceMin, camera.TppCameraDistanceMax);
        HotKey? zoom = capi.Input.GetHotKeyByCode(target > current ? "zoomout" : "zoomin");
        if (zoom?.Handler == null) return;

        for (int i = 0; i < Math.Abs(target - current); i++) zoom.Handler(zoom.CurrentMapping);
        pending = false;
        Save(target);
    }

    private void Remember(PlayerCamera camera)
    {
        float distance = camera.Tppcameradistance;
        int settled = (int)Math.Round(distance);
        // The distance eases toward the zoom target over several frames; only a value at rest is the player's choice.
        if (Math.Abs(distance - settled) > 0.05f || settled == state.Distance) return;
        Save(settled);
    }

    private void Save(int distance)
    {
        state.Distance = distance;
        capi!.StoreModConfig(state, StateFile);
    }
}

public class HalfGiantCameraState
{
    public int? Distance { get; set; }
}
