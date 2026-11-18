using System;
using System.Linq;

/// <summary>Hosts Facepunch's spawn menu without replacing the Storm Chasers character.</summary>
public sealed class SandboxMenuBridge : Component
{
    PanelComponent host;
    Type playerType;
    GameObject character;
    public bool IsOpen => host.IsValid() && host.Panel?.HasClass("open") == true;

    protected override async void OnStart()
    {
        if (!Game.IsPlaying) return;
        try
        {
            if (await Package.MountAsync("facepunch.sandbox", true) is null)
                throw new InvalidOperationException("Sandbox package download failed.");
            if (!this.IsValid()) return;

            var menuType = TypeLibrary.GetType("Sandbox.SpawnMenuHost");
            if (menuType is null) throw new InvalidOperationException("Sandbox menu component is unavailable.");
            var playerDescription = TypeLibrary.GetType("Player") ?? throw new InvalidOperationException("Sandbox player component is unavailable.");
            playerType = playerDescription.TargetType;
            character = Scene.Get<StormControls>().GameObject;
            if (character.Network.Owner is null) character.NetworkSpawn(Connection.Local);
            if (character.Components.Get(playerType) is null)
                character.Components.Create(TypeLibrary.GetType(playerType));
            RemoveDuplicateLocalPlayer();
            host = (PanelComponent)Components.Create(menuType);
        }
        catch (Exception e)
        {
            if (this.IsValid()) Log.Warning($"Sandbox spawn menu failed: {e.Message}");
        }
    }

    protected override void OnUpdate() => RemoveDuplicateLocalPlayer();

    void RemoveDuplicateLocalPlayer()
    {
        if (playerType is null || !character.IsValid()) return;
        foreach (var controller in Scene.GetAllComponents<PlayerController>().ToArray())
        {
            var obj = controller.GameObject;
            if (obj != character && obj.Network.Owner == Connection.Local && obj.Components.Get(playerType) != null)
                obj.Destroy();
        }
    }
}
