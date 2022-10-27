using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Csg;

namespace CsgDemo;

/// <summary>
/// This is your game class. This is an entity that is created serverside when
/// the game starts, and is replicated to the client. 
/// 
/// You can use this to create things like HUDs and declare which player class
/// to use for spawned players.
/// </summary>
public partial class CsgDemoGame : Sandbox.Game
{
	public new static CsgDemoGame Current => Sandbox.Game.Current as CsgDemoGame;

	public CsgBrush CubeBrush { get; } = ResourceLibrary.Get<CsgBrush>( "brushes/cube.csg" );
	public CsgBrush DodecahedronBrush { get; } = ResourceLibrary.Get<CsgBrush>( "brushes/dodecahedron.csg" );

	[Net]
	public CsgSolid CsgWorld { get; private set; }

	public CsgDemoGame()
    {
	}

	/// <summary>
	/// A client has joined the server. Make them a pawn to play with
	/// </summary>
	public override void ClientJoined( Client client )
	{
		base.ClientJoined( client );

		if ( CsgWorld == null )
		{
			SpawnWorld();
		}

		// Create a pawn for this client to play with
		var pawn = new Player( client );
		client.Pawn = pawn;
		pawn.Respawn();

		// Get all of the spawnpoints
		var spawnpoints = Entity.All.OfType<SpawnPoint>();

		// chose a random one
		var randomSpawnPoint = spawnpoints.OrderBy( x => Guid.NewGuid() ).FirstOrDefault();

		// if it exists, place the pawn there
		if ( randomSpawnPoint != null )
		{
			var tx = randomSpawnPoint.Transform;
			tx.Position = tx.Position + Vector3.Up * 2048.0f; // raise it up
			pawn.Transform = tx;
		}
	}

	private void SpawnWorld()
	{
		Assert.True( IsServer );

		CsgWorld = new CsgSolid();

		CsgWorld.Modify( CubeBrush,
			CsgOperator.Add,
			position: Vector3.Up * 512f,
			scale: new Vector3( 8192f, 8192f, 1024f ) );

		CsgWorld.Modify( DodecahedronBrush,
			CsgOperator.Subtract,
			position: Vector3.Up * 1024f,
			scale: new Vector3( 1024f, 1024f, 512f ),
			rotation: Rotation.FromYaw( 45f ) );
	}
}
