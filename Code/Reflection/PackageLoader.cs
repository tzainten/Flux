using Sandbox;
using System;
using System.Linq;
using System.Reflection;

namespace Flux.Reflection;

internal static class PackageLoader
{
	private static Type _gameInstanceDllType;
	private static Type GameInstanceDllType => _gameInstanceDllType ??= Managed.GameInstance.GetType( "Sandbox.GameInstanceDll" );

	private static object GetInstance() =>
		GameInstanceDllType.GetProperty( "PackageLoader", BindingFlags.Static | BindingFlags.Public )?.GetValue( null );

	private static Type _loaderType;
	private static Type LoaderType
	{
		get
		{
			if ( _loaderType != null ) return _loaderType;
			var instance = GetInstance();
			if ( instance != null ) _loaderType = instance.GetType();
			return _loaderType;
		}
	}

	private static PropertyInfo _ilHotloadProperty;
	private static PropertyInfo ILHotloadProperty
	{
		get
		{
			if ( _ilHotloadProperty != null ) return _ilHotloadProperty;
			_ilHotloadProperty = LoaderType?.GetProperty( "ILHotload", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
			return _ilHotloadProperty;
		}
	}

	private static MethodInfo _addAssemblyMethod;
	private static MethodInfo AddAssemblyMethod
	{
		get
		{
			if ( _addAssemblyMethod != null ) return _addAssemblyMethod;
			_addAssemblyMethod = LoaderType?.GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
				.FirstOrDefault( m => m.Name == "AddAssembly" && m.GetParameters().Length == 4 );
			return _addAssemblyMethod;
		}
	}

	private static MethodInfo _trustUnsafeMethod;
	private static MethodInfo TrustUnsafeMethod
	{
		get
		{
			if ( _trustUnsafeMethod != null ) return _trustUnsafeMethod;
			var accessControlType = Managed.Access.GetType( "Sandbox.AccessControl" );
			if ( accessControlType == null ) return null;
			_trustUnsafeMethod = accessControlType.GetMethod( "TrustUnsafe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );
			return _trustUnsafeMethod;
		}
	}

	private static object _accessControlInstance;
	private static object AccessControlInstance
	{
		get
		{
			if ( _accessControlInstance != null ) return _accessControlInstance;
			var packageManagerType = Managed.Engine.GetType( "Sandbox.PackageManager" );
			if ( packageManagerType == null ) return null;
			var prop = packageManagerType.GetProperty( "AccessControl", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic );
			_accessControlInstance = prop?.GetValue( null );
			return _accessControlInstance;
		}
	}

	/// <summary>
	/// Directly loads a compiled assembly into the engine via PackageLoader.AddAssembly,
	/// bypassing the MemoryFileSystem watcher and access control verification.
	/// </summary>
	internal static void LoadAssemblyDirect( Package package, string assemblyName, byte[] assemblyData, byte[] codeArchiveData )
	{
		var instance = GetInstance();
		if ( instance == null )
		{
			Flux.Log.Warning( "PackageLoader instance not found" );
			return;
		}

		var trustUnsafe = TrustUnsafeMethod;
		if ( trustUnsafe == null )
		{
			Flux.Log.Warning( "TrustUnsafe method not found" );
			return;
		}

		var accessControl = AccessControlInstance;
		if ( accessControl == null )
		{
			Flux.Log.Warning( "AccessControl instance not found" );
			return;
		}

		var addAssembly = AddAssemblyMethod;
		if ( addAssembly == null )
		{
			Flux.Log.Warning( "AddAssembly(4-param) method not found" );
			return;
		}

		var trustedStream = trustUnsafe.IsStatic
			? trustUnsafe.Invoke( null, [assemblyData] )
			: trustUnsafe.Invoke( accessControl, [assemblyData] );

		addAssembly.Invoke( instance, [package, assemblyName, trustedStream, codeArchiveData] );
	}

	/// <summary>
	/// Sets PackageLoader.ILHotload if it is currently null, enabling fast IL-level hotloads.
	/// The engine only creates this in editor/unit-test mode, so we create it ourselves.
	/// </summary>
	internal static void EnsureILHotload( string name )
	{
		var instance = GetInstance();
		if ( instance == null ) return;

		var prop = ILHotloadProperty;
		if ( prop == null ) return;

		if ( prop.GetValue( instance ) != null ) return;

		var ilHotloadType = AppDomain.CurrentDomain.GetAssemblies()
			.Select( a => { try { return a.GetType( "Sandbox.ILHotload" ); } catch { return null; } } )
			.FirstOrDefault( t => t != null );

		if ( ilHotloadType == null )
		{
			Flux.Log.Warning( "Could not find ILHotload type — fast hotloads will not be available" );
			return;
		}

		var ilHotload = Activator.CreateInstance( ilHotloadType, [name] );
		prop.SetValue( instance, ilHotload );
		Flux.Log.Info( $"ILHotload enabled for '{name}'" );
	}
}
