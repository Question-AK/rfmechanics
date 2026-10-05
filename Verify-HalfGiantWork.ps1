param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot 'bin/Release/Mods/rfmechanics.dll'),
    [switch]$ExpectCreativeRegression
)

$ErrorActionPreference = 'Stop'
Add-Type -Path @(
    (Join-Path $PSScriptRoot 'src/HalfGiantReachRules.cs'),
    (Join-Path $PSScriptRoot 'src/HalfGiantQuarryRules.cs')
)
$script:checks = 0
function Assert-True([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checks++
}
function Assert-Near([float]$Actual, [float]$Expected, [string]$Name) {
    Assert-True ([Math]::Abs($Actual - $Expected) -lt 0.0001) $Name
}
Assert-True ([rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'game', 'rock-granite')) 'Half-Giant empty hand can quarry natural rock'
Assert-True ([rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'game', 'crackedrock-granite')) 'Half-Giant empty hand can quarry natural cracked rock'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($false, $true, $false, $true, 'game', 'rock-granite')) 'Quarry enable gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $false, $false, $true, 'game', 'rock-granite')) 'Race gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $true, $true, 'game', 'rock-granite')) 'Creative gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $false, 'game', 'rock-granite')) 'Active-hand gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'game', 'ore-quartz')) 'Ore gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'game', 'stonebricks-granite')) 'Worked-stone gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'examplemod', 'rock-granite')) 'Natural-domain gate'
Assert-Near ([rfmechanics.HalfGiantQuarryRules]::ApplySatietyCost(50, 10)) 40 'One successful break costs ten satiety exactly once'
Assert-Near ([rfmechanics.HalfGiantQuarryRules]::ApplySatietyCost(5, 10)) 0 'Satiety cost does not underflow'
$quarryPatch = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantQuarryPatch.cs') -Raw
Assert-True ($quarryPatch.Contains('nameof(Block.OnGettingBroken)')) 'Quarry uses empty-hand Block.OnGettingBroken seam'
Assert-True (-not $quarryPatch.Contains('GetMiningSpeed')) 'Quarry does not broaden held-item mining speed'
$quarrySystem = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantQuarryModSystem.cs') -Raw
Assert-True ($quarrySystem.Contains('DidBreakBlock += OnDidBreakBlock')) 'Satiety charge observes successful server breaks'
Assert-True ($quarrySystem.Contains('GetBlock(oldBlockId)')) 'Satiety charge validates original broken block identity'

if (-not (Test-Path $AssemblyPath)) { throw "FAIL: production assembly not found: $AssemblyPath" }
$apiAssembly = Join-Path $env:VINTAGE_STORY 'VintagestoryAPI.dll'
try {
    Add-Type -Path @($apiAssembly, $AssemblyPath)
}
catch {
    $_.Exception.LoaderExceptions | ForEach-Object { Write-Error $_.Message }
    throw
}

$fixtureSource = @'
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using rfmechanics;

public sealed class ReachRouteProxy : DispatchProxy
{
    public Func<MethodInfo, object[], object> Handler = null!;

    protected override object Invoke(MethodInfo method, object[] args)
    {
        return Handler(method, args);
    }
}

public sealed class ProductionReachFixture
{
    private sealed class State
    {
        public EnumGameMode Mode;
        public float Range;
        public float Previous;
        public int Broadcasts;
        public int ModRangeWrites;
        public int EventSubscriptions;
        public int EventUnsubscriptions;
        public Delegate ModeHandler;
        public readonly Dictionary<string, byte[]> Moddata = new Dictionary<string, byte[]>();
    }

    private static T Proxy<T>(Func<MethodInfo, object[], object> handler) where T : class
    {
        T proxy = DispatchProxy.Create<T, ReachRouteProxy>();
        ((ReachRouteProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static object DefaultValue(MethodInfo method)
    {
        Type type = method.ReturnType;
        return type == typeof(void) ? null! : type.IsValueType ? Activator.CreateInstance(type)! : null!;
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
    }

    private sealed class Fixture
    {
        private readonly State state = new State();
        private readonly IServerPlayer serverPlayer;
        private readonly IWorldAccessor world;
        private readonly IServerEventAPI events;
        private readonly ICoreServerAPI serverApi;

        public Fixture(EnumGameMode mode, float range, float previous)
        {
            state.Mode = mode;
            state.Range = range;
            state.Previous = previous;
            serverPlayer = Proxy<IServerPlayer>((method, args) =>
            {
                if (method.Name == "get_PlayerUID") return "fixture-player";
                if (method.Name == "get_WorldData") return Proxy<IPlayerData>(WorldDataCall);
                if (method.Name == "BroadcastPlayerData") { state.Broadcasts++; return null!; }
                return DefaultValue(method);
            });
            world = Proxy<IWorldAccessor>((method, args) =>
            {
                if (method.Name == "PlayerByUid") return serverPlayer;
                return DefaultValue(method);
            });
            events = Proxy<IServerEventAPI>((method, args) =>
            {
                if (method.Name == "add_PlayerSwitchGameMode")
                {
                    state.EventSubscriptions++;
                    state.ModeHandler = Delegate.Combine(state.ModeHandler, (Delegate)args[0]);
                    return null!;
                }
                if (method.Name == "remove_PlayerSwitchGameMode")
                {
                    state.EventUnsubscriptions++;
                    state.ModeHandler = Delegate.Remove(state.ModeHandler, (Delegate)args[0]);
                    return null!;
                }
                return DefaultValue(method);
            });
            serverApi = Proxy<ICoreServerAPI>((method, args) =>
            {
                if (method.Name == "get_World") return world;
                if (method.Name == "get_Event") return events;
                return DefaultValue(method);
            });
        }

        private object WorldDataCall(MethodInfo method, object[] args)
        {
            if (method.Name == "get_CurrentGameMode") return state.Mode;
            if (method.Name == "get_PickingRange") return state.Range;
            if (method.Name == "set_PickingRange") { state.Range = (float)args[0]; state.ModRangeWrites++; return null!; }
            if (method.Name == "get_PreviousPickingRange") return state.Previous;
            if (method.Name == "set_PreviousPickingRange") { state.Previous = (float)args[0]; return null!; }
            if (method.Name == "GetModdata")
            {
                byte[] value;
                return state.Moddata.TryGetValue((string)args[0], out value) ? value : null!;
            }
            if (method.Name == "SetModdata") { state.Moddata[(string)args[0]] = (byte[])args[1]; return null!; }
            if (method.Name == "RemoveModdata") { state.Moddata.Remove((string)args[0]); return null!; }
            return DefaultValue(method);
        }

        public HalfGiantReachBehavior CreateBehavior()
        {
            var entity = new EntityPlayer();
            entity.Api = serverApi;
            entity.PlayerUID = "fixture-player";
            var identity = new PlayerRaceBehavior(entity);
            typeof(PlayerRaceBehavior).GetProperty("Race")!.SetValue(identity, PlayerRace.HalfGiant);
            entity.AddBehavior(identity);
            var behavior = new HalfGiantReachBehavior(entity);
            behavior.Initialize(new EntityProperties(), null!);
            return behavior;
        }

        public void Tick(HalfGiantReachBehavior behavior)
        {
            behavior.OnGameTick(0.05f);
        }

        public void NativeSetMode(EnumGameMode target)
        {
            if (target == EnumGameMode.Survival)
            {
                state.Previous = state.Range;
                state.Range = HalfGiantReachRules.VanillaPickingRange;
            }
            else if (state.Mode == EnumGameMode.Survival)
            {
                state.Range = state.Previous;
            }

            state.Mode = target;
            state.ModeHandler?.DynamicInvoke(serverPlayer);
            state.Broadcasts++;
        }

        public float Range => state.Range;
        public float Previous => state.Previous;
        public int Broadcasts => state.Broadcasts;
        public int ModRangeWrites => state.ModRangeWrites;
        public int EventSubscriptions => state.EventSubscriptions;
        public int EventUnsubscriptions => state.EventUnsubscriptions;
        public bool HasModdata => state.Moddata.Count != 0;
        public void SetRangeExternally(float range) => state.Range = range;
    }

    private static RFMechanicsConfig Config(float target, bool enabled = true)
    {
        var config = new RFMechanicsConfig();
        config.EnableHalfGiantReach = enabled;
        config.HalfGiantPickingRange = target;
        typeof(RFMechanicsModSystem).GetField("config", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, config);
        return config;
    }

    public static void Run(bool expectCreativeRegression)
    {
        foreach (float creativeRange in new[] { 4.5f, 9.45f, 16f })
        {
            Config(9.45f);
            var fixture = new Fixture(EnumGameMode.Creative, creativeRange, 3f);
            var behavior = fixture.CreateBehavior();
            fixture.NativeSetMode(EnumGameMode.Survival);
            fixture.Tick(behavior);
            fixture.NativeSetMode(EnumGameMode.Creative);
            fixture.Tick(behavior);
            if (expectCreativeRegression && HalfGiantReachRules.IsSamePickingRange(creativeRange, 9.45f))
            {
                Require(HalfGiantReachRules.IsSamePickingRange(fixture.Range, 4.5f), "immutable candidate must show the Creative 9.45 regression");
            }
            else
            {
                Require(HalfGiantReachRules.IsSamePickingRange(fixture.Range, creativeRange), "Creative range survives native mode round-trip: " + creativeRange);
                Require(HalfGiantReachRules.IsSamePickingRange(fixture.Previous, creativeRange), "PreviousPickingRange survives native mode round-trip: " + creativeRange);
            }
        }

        if (expectCreativeRegression) return;

        Config(9.45f);
        var rapid = new Fixture(EnumGameMode.Creative, 16f, 3f);
        rapid.CreateBehavior();
        rapid.NativeSetMode(EnumGameMode.Survival);
        Require(HalfGiantReachRules.IsSamePickingRange(rapid.Range, 9.45f), "authoritative C-to-S event applies reach without a tick");
        rapid.NativeSetMode(EnumGameMode.Creative);
        Require(HalfGiantReachRules.IsSamePickingRange(rapid.Range, 16f), "rapid no-tick S-to-C preserves Creative range");
        Require(HalfGiantReachRules.IsSamePickingRange(rapid.Previous, 16f), "rapid no-tick S-to-C preserves PreviousPickingRange");

        var cleanup = new Fixture(EnumGameMode.Survival, 4.5f, 4.5f);
        var cleanupBehavior = cleanup.CreateBehavior();
        cleanup.Tick(cleanupBehavior);
        Require(HalfGiantReachRules.IsSamePickingRange(cleanup.Range, 9.45f), "initial eligible Survival acquires reach");
        Config(9.45f, false);
        cleanup.Tick(cleanupBehavior);
        Require(HalfGiantReachRules.IsSamePickingRange(cleanup.Range, 4.5f), "Survival disable restores captured baseline");

        Config(9.45f);
        var custom = new Fixture(EnumGameMode.Survival, 16f, 4.5f);
        var customBehavior = custom.CreateBehavior();
        custom.Tick(customBehavior);
        Require(HalfGiantReachRules.IsSamePickingRange(custom.Range, 16f), "initial custom Survival range is preserved");
        custom.SetRangeExternally(16f);
        custom.Tick(customBehavior);
        Require(HalfGiantReachRules.IsSamePickingRange(custom.Range, 16f), "observable external custom range is preserved");

        var target = new Fixture(EnumGameMode.Survival, 4.5f, 4.5f);
        var targetBehavior = target.CreateBehavior();
        target.Tick(targetBehavior);
        Config(10f);
        target.Tick(targetBehavior);
        Require(HalfGiantReachRules.IsSamePickingRange(target.Range, 10f), "owned target setting change updates current override");
        Config(10f, false);
        target.Tick(targetBehavior);
        Require(HalfGiantReachRules.IsSamePickingRange(target.Range, 4.5f), "target setting change keeps original baseline");

        Config(9.45f);
        var lifecycle = new Fixture(EnumGameMode.Survival, 4.5f, 4.5f);
        var lifecycleBehavior = lifecycle.CreateBehavior();
        lifecycleBehavior.Initialize(new EntityProperties(), null!);
        Require(lifecycle.EventSubscriptions == 1, "behavior registers one mode handler");
        lifecycle.Tick(lifecycleBehavior);
        lifecycleBehavior.OnEntityDeath(null!);
        Require(!lifecycle.HasModdata, "death clears reach ownership");
        Require(lifecycle.EventUnsubscriptions == 1, "death unregisters mode handler");
        lifecycleBehavior.OnEntityDespawn(null!);
        Require(lifecycle.EventUnsubscriptions == 1, "despawn does not unregister the same handler twice");
    }
}
'@

Add-Type -TypeDefinition $fixtureSource -ReferencedAssemblies @($apiAssembly, $AssemblyPath)
[ProductionReachFixture]::Run($ExpectCreativeRegression)
$script:checks++
Write-Output "PASS: $script:checks Half-Giant reach/quarry assertions using initialized production behavior and native mode ordering. Gameplay remains a player check."
