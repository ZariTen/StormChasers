/// <summary>Use normal mouse capture and s&box's own pause menu.</summary>
public sealed class StormControls : Component
{
    readonly Game.Overlay overlay = new();
    public bool MenuOpen => overlay.IsPauseMenuOpen;
    public bool MouseLookActive => !overlay.IsOpen && !(Scene.Get<SandboxMenuBridge>()?.IsOpen ?? false);

    protected override void OnStart() => ApplyMode();

    void ApplyMode()
    {
        var playing = MouseLookActive;
        var controller = Components.Get<PlayerController>();
        if (controller.IsValid())
        {
            controller.UseLookControls = playing;
            controller.UseInputControls = playing;
            if (!playing) controller.WishVelocity = Vector3.Zero;
        }
        Mouse.Visibility = playing ? MouseVisibility.Hidden : MouseVisibility.Visible;
    }

    protected override void OnUpdate()
    {
        if (Input.EscapePressed && Scene.Get<SandboxMenuBridge>()?.IsOpen != true)
        {
            Input.EscapePressed = false;
            if (!overlay.IsOpen) Game.Overlay.ShowPauseMenu();
        }
        ApplyMode();
    }

    protected override void OnDisabled()
    {
        Mouse.Visibility = MouseVisibility.Visible;
        var controller = Components.Get<PlayerController>();
        if (controller.IsValid()) controller.UseLookControls = false;
    }
}
