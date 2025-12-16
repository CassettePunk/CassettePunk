using System.Diagnostics;
using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Shared.Reloading.Systems;

public sealed class ReloadingSystem: EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.Reload, InputCmdHandler.FromDelegate(HandleReload, handle: false, outsidePrediction: false))
            .Register<ReloadingSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<ReloadingSystem>();
    }

    private void HandleReload(ICommonSession? session)
    {
        Log.Debug("Reloading!");
    }
}
