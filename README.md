# Harmony Shim for Caves of Qud
Harmony Shim is a mod for [Caves of Qud](https://store.steampowered.com/app/333640/Caves_of_Qud/) that enables the use of [Harmony](https://github.com/pardeike/Harmony) through a simple shim. This allows other mods to patch parts of the game that are difficult to modify using Caves of Qud's current modding system.

This is a modder's resource; this mod does not tangibly alter the game on its own. Other mods may require Harmony Shim to be installed before they will function correctly.

For more information on Harmony, please visit the [project's Github](https://github.com/pardeike/Harmony).

In order to create a Harmony patch in your script-mod, add the `using HarmonyShim` directive to the script's source. Next, create a class with a static constructor. In the constructor's body, use the `Harmony.Patch` method to perform your patches. Harmony Shim will run your static constructor when it initializes, allowing your patch to be applied.

Due to the way Harmony needs to be loaded, Harmony Shim can only provide basic manual patching. Attribute-based patching isn't supported and useful things like the `AccessTools` are also not available.

## Special Note
This mod prevents the game from recompiling the mod-assembly if you alter your mods after starting the game; this prevents undefined behavior from reapplying patches and causing corruption or other nasty things.

Recompilation usually happens upon exiting the mod configuration screen after enabling a script-mod. The game will attempt to continue to run and display a warning when you start or load a game after a recompilation attempt has been detected, suggesting that you restart before continuing.

There is no guarantee that it can continue to run, however.  The best practice is to restart the game immediately after enabling new mods.

## Usage
Use a static constructor to create your patches. Harmony Shim will ensure your static constructor is run after Harmony has initialized with a quick iteration of all types in the mod-assembly.

If at any point your mod-assembly fails to compile, the `output_log.txt` file will have hundreds of errors that look like the following:
`Logged exception Unknown :System.Exception: Unknown part HarmonyShim_Noop!`

Harmony Shim attaches a part that provides no behavior to all creatures in the game.  It's only purpose is to force C# to call its static initializer as soon as possible, which will in-turn initialize Harmony Shim and apply the patches from other mods.  If the mod-assembly fails to compile, then this part does not exist, and it will therefore create a great deal many of the above errors.

Please don't be confused by the `HarmonyShim_Noop` spam.  It is unlikely Harmony Shim is the cause of the mod-assembly failing to compile.  Search the log for the text `==== COMPILER ERRORS ====` to find out how things went awry.

I recommend using [dnSpy](https://github.com/0xd4d/dnSpy) to examine Caves of Qud's code when deciding how to best create a patch. CoQ's codebase is not the easiest to follow and the copious use of god-functions everywhere makes it a challenge to patch in some cases, but anything is possible with some clever thinking.

## API Documentation
The API made available is extremely simple. It is exposed as static methods that can be used with a simple import. Simply add `using HarmonyShim;` to the top of your script's source code, and the methods below will become available.

### `Harmony.Patch`
```
Harmony.Patch(MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null) => Void
```
Corresponds to HarmonyInstance's `Patch` method. Patches a given method. Allows you to specify the methods used for prefix, postfix, and transpiler patches. The [named parameters feature](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments) can be used instead of passing `null` if you only need to provide one type of patch.

Harmony requires that methods used as patches follow a very specific signature. Please refer to [Harmony's wiki for more information](https://github.com/pardeike/Harmony/wiki/Patching).

### `Harmony.ApplyPrefix`
```
Harmony.ApplyPrefix(MethodBase original, MethodInfo prefix) => Void
```
Short-hand method for applying a prefix patch. If `prefix` is null, a warning will be logged to `output_log.txt`.

### `Harmony.ApplyPostfix`
```
Harmony.ApplyPostfix(MethodBase original, MethodInfo postfix) => Void
```
Short-hand method for applying a postfix patch. If `postfix` is null, a warning will be logged to `output_log.txt`.

### `Harmony.ApplyTranspiler`
```
Harmony.ApplyTranspiler(MethodBase original, MethodInfo transpiler) => Void
```
Short-hand method for applying a transpiler patch. If `transpiler` is null, a warning will be logged to `output_log.txt`.  

### `Harmony.Unpatch`
```
Harmony.Unpatch(MethodBase original, MethodInfo patch) => Void
```
Corresponds to HarmonyInstance's `Unpatch` method. Removes a patch on a method, when given the original method's `MethodBase` and the patch's `MethodInfo`.

### `Harmony.GetPatchedMethods`
```
Harmony.GetPatchedMethods() => IEnumerable<MethodBase>
```
Corresponds to HarmonyInstance's `GetPatchedMethods` method. Produces an enumeration of all methods that have been patched.