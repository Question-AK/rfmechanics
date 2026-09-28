using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace rfmechanics;

// One noninteractive fading line, no panel, input capture, history or pending queue.
internal sealed class RaceFeedbackHud : HudElement
{
    private readonly Vec4f tint = new(1, 1, 1, 0);
    private GuiElementHoverText? label;
    private long shownAt, entityId;
    private PlayerRace race, composedRace;
    internal RaceFeedbackHud(ICoreClientAPI api) : base(api) { }
    public override bool Focusable => false;
    public override bool ShouldReceiveKeyboardEvents() => false;
    public override bool ShouldReceiveMouseEvents() => false;

    internal void Show(string text, long id, PlayerRace forRace)
    {
        if (label == null || composedRace != forRace)
        {
            ClearComposers();
            var bounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, -170, 700, 40);
            double[] color = forRace switch {
                PlayerRace.Goblin => new double[] { 0.706, 0.690, 0.471, 1 },
                PlayerRace.Orc => new double[] { 0.529, 0.608, 0.447, 1 },
                PlayerRace.Elf => new double[] { 0.761, 0.843, 0.784, 1 },
                _ => new double[] { 0.784, 0.706, 0.545, 1 }
            };
            var font = CairoFont.WhiteMediumText()
                .WithFont(forRace == PlayerRace.Goblin ? GuiStyle.StandardFontName : GuiStyle.DecorativeFontName)
                .WithColor(color).WithStroke(GuiStyle.DialogBorderColor, 2)
                .WithOrientation(EnumTextOrientation.Center);
            SingleComposer = capi.Gui.CreateCompo("rf-racial-feedback", bounds)
                .AddTranspHoverText("", font, 700, ElementBounds.Fixed(0, 0, 700, 40), "notice").Compose();
            label = SingleComposer.GetHoverText("notice");
            label.SetFollowMouse(false); label.SetAutoWidth(false); label.SetAutoDisplay(false);
            label.fillBounds = true; label.RenderColor = tint; composedRace = forRace;
        }
        label.SetNewText(text); label.SetVisible(true);
        shownAt = capi.InWorldEllapsedMilliseconds; entityId = id; race = forRace;
        TryOpen();
    }

    public override void OnRenderGUI(float dt)
    {
        var self = capi.World.Player?.Entity;
        double age = (capi.InWorldEllapsedMilliseconds - shownAt) / 1000.0;
        if (!RaceFeedbackModSystem.HasBehaviorState(self) || self?.Alive != true || self.EntityId != entityId
            || self.GetBehavior<PlayerRaceBehavior>()?.Race != race || age >= 3.6)
        { TryClose(); return; }
        tint.A = (float)Math.Clamp(Math.Min(age / 0.15, (3.6 - age) / 0.65), 0, 1);
        base.OnRenderGUI(dt);
    }
}
