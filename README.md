# Flux

This is a simple [BepInSbox](https://github.com/CiarenceW/BepInSbox) plugin that hooks into a package right before it's compiled, letting you inject your own code along with it.

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
1. Make sure you have [BepInSbox](https://github.com/CiarenceW/BepInSbox) installed.
2. Download and extract a release into your `%FACEPUNCH_ENGINE%` directory
3. Open s&box. Open the console and type `flux_new <projectName> <targetPackage>` (i.e: `flux_new MyMod facepunch.sandbox`)
4. Head into `%FACEPUNCH_ENGINE%BepInSbox\plugins\` and a folder containing everything for your mod should be in there, ready to go.

# Setting Up IntelliSense (Optional)
You'll notice that there's a `ThirdParty` folder in your mod's folder. This is where you can place a compiled .dll for an s&box package, and then you'll have intellisense for that package.

NOTE: You don't need intellisense in order for your mod to work. If your code is correct, s&box will compile your mod correctly alongside the package you're injecting into.

# Limitations

- There's currently no hotloading support. I do want this, so if anyone has any ideas, don't be a stranger.
- You are still limited to the whitelist access control. I'm not sure if I can get around this, so again, I'm open to ideas!
- BepInSbox does not support loading into sbox.exe directly. You'll need to patch it yourself. ([It's stupid easy, I promise](https://github.com/CiarenceW/BepInSbox/blob/f7e066fe18211d33fe72e80b92bca0dbe3c4b72e/Doorstop/dllmain.cpp#L292))
- BepInSbox also seems to have a bug that causes a GameObject leak, completely crashing s&box. This seems to be an upstream issue, with no workaround at the moment.
