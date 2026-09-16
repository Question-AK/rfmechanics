using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace rfmechanics;

// Orc-only, single replaceable discovery-style line. The global discovery HUD queues
// every message for six seconds, so it is unsuitable for cancellable combat state.
internal sealed class OrcBraceHud : HudElement
{
    private readonly Vec4f tint = new(1, 1, 1, 0);
    private GuiElementHoverText? label;
    private long shownAt;
    private long entityId;
    internal OrcBraceHud(ICoreClientAPI api) : base(api) { }
    public override bool Focusable => false;
    public override bool ShouldReceiveKeyboardEvents() => false;
    public override bool ShouldReceiveMouseEvents() => false;

    internal void Show(string text, long forEntity)
    {
        if (label == null)
        {
            var bounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, -170, 700, 40);
            var font = CairoFont.WhiteMediumText().WithFont(GuiStyle.DecorativeFontName)
                .WithColor(GuiStyle.DiscoveryTextColor).WithStroke(GuiStyle.DialogBorderColor, 2)
                .WithOrientation(EnumTextOrientation.Center);
            SingleComposer = capi.Gui.CreateCompo("rf-orc-brace", bounds)
                .AddTranspHoverText("", font, 700, ElementBounds.Fixed(0, 0, 700, 40), "notice").Compose();
            label = SingleComposer.GetHoverText("notice");
            label.SetFollowMouse(false); label.SetAutoWidth(false); label.SetAutoDisplay(false);
            label.fillBounds = true; label.RenderColor = tint;
        }
        label.SetNewText(text); label.SetVisible(true);
        shownAt = capi.InWorldEllapsedMilliseconds; entityId = forEntity;
        TryOpen();
    }

    public override void OnRenderGUI(float dt)
    {
        var self = capi.World.Player?.Entity;
        double age = (capi.InWorldEllapsedMilliseconds - shownAt) / 1000.0;
        if (self?.Alive != true || self.EntityId != entityId
            || self.GetBehavior<PlayerRaceBehavior>()?.Race != PlayerRace.Orc || age >= 3.6)
        { TryClose(); return; }
        tint.A = (float)Math.Clamp(Math.Min(age / 0.15, (3.6 - age) / 0.65), 0, 1);
        base.OnRenderGUI(dt);
    }
}
