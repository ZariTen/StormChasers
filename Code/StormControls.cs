/// <summary>Use normal mouse capture and s&box's own pause menu.</summary>
public sealed class StormControls : Component
{
    readonly Game.Overlay overlay = new();
    public bool MenuOpen => overlay.IsPauseMenuOpen;
    public bool MouseLookActive => !overlay.IsOpen;

    protected override void OnStart() => ApplyMode();

    void ApplyMode()
    {
        var overlayOpen = overlay.IsOpen;
        var controller = Components.Get<PlayerController>();
        if (controller.IsValid())
        {
            controller.UseLookControls = !overlayOpen;
            controller.UseInputControls = !overlayOpen;
            if (overlayOpen) controller.WishVelocity = Vector3.Zero;
        }
        Mouse.Visibility = overlayOpen ? MouseVisibility.Visible : MouseVisibility.Hidden;
    }

    protected override void OnUpdate()
    {
        if (Input.EscapePressed)
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
