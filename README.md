# Flux

This is a simple injector for [s&box](https://sbox.game/) that hooks into a package right before it's compiled, letting you inject your own code along with it.

This lets you write mods with full type safety and IntelliSense instead of relying on reflection.

For example, here's a [sandbox](https://sbox.game/facepunch/sandbox) mod that accesses the Physgun component directly:

```c#
namespace MyMod;

internal class MySystem : GameObjectSystem<MySystem>
{
	public MySystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, OnFinishUpdate, "OnFinishUpdate" );
	}

	void OnFinishUpdate()
	{
		// I'm able to access the Physgun component directly. No reflection.
		Log.Info( Scene.GetAll<Physgun>().Count() );
	}
}
```

# How To Use
1. Download and extract the [latest release](https://github.com/tzainten/Flux/releases/latest) into your `%FACEPUNCH_ENGINE%` directory
2. Open s&box. Open the console and type `flux_new <projectName> <targetPackage>` (i.e: `flux_new MyMod facepunch.sandbox`)
3. Head into `%FACEPUNCH_ENGINE%Flux\Mods\` and a folder containing everything for your mod should be in there, ready to go.
4. When you load that package in-game, all of it's source code will be extracted into any mods targeting that package.

# Limitations

- You are still limited to the whitelist access control.
