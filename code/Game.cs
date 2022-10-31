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

    public CsgMaterial DefaultMaterial { get; } = ResourceLibrary.Get<CsgMaterial>( "materials/csgdemo/default.csgmat" );
    public CsgMaterial RedMaterial { get; } = ResourceLibrary.Get<CsgMaterial>( "materials/csgdemo/red.csgmat" );
    public CsgMaterial ScorchedMaterial { get; } = ResourceLibrary.Get<CsgMaterial>( "materials/csgdemo/scorched.csgmat" );

    [Net]
    public CsgSolid CsgWorld { get; private set; }

    public CsgDemoGame()
    {
    }

	public static void Explosion( Entity weapon, Entity owner, Vector3 position, float radius, float damage, float forceScale, float ownerDamageScale = 1f )
	{
		Sound.FromWorld( "gl.explode", position );
		Particles.Create( "particles/explosion/barrel_explosion/explosion_barrel.vpcf", position );

		if ( Host.IsClient ) return;

		var overlaps = FindInSphere( position, radius );

		foreach ( var overlap in overlaps )
		{
			if ( overlap is not ModelEntity entity || !entity.IsValid() )
				continue;

			if ( entity.LifeState != LifeState.Alive )
				continue;

			if ( !entity.PhysicsBody.IsValid() )
				continue;

			if ( entity.IsWorld )
				continue;

			var targetPos = entity.PhysicsBody.MassCenter;

			var dist = Vector3.DistanceBetween( position, targetPos );
			if ( dist > radius )
				continue;

			var tr = Trace.Ray( position, targetPos )
				.Ignore( weapon )
				.WorldOnly()
				.Run();

			if ( tr.Fraction < 0.98f )
				continue;

			var distanceMul = 1.0f - Math.Clamp( dist / radius, 0.0f, 1.0f );
			var dmg = damage * distanceMul;
			var force = (forceScale * distanceMul) * entity.PhysicsBody.Mass;
			var forceDir = (targetPos - position).Normal;

			if ( overlap == owner )
			{
				dmg *= ownerDamageScale;
				forceDir = (targetPos - (position + Vector3.Down * 32f)).Normal;
			}

			var damageInfo = DamageInfo.Explosion( position, forceDir * force, dmg )
				.WithFlag( DamageFlags.Blast )
				.WithWeapon( weapon )
				.WithAttacker( owner );

			entity.TakeDamage( damageInfo );
		}
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
        var pawn = new Player();
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

        CsgWorld = new CsgSolid( 1024f );

        CsgWorld.Add( CubeBrush,
            DefaultMaterial,
            scale: new Vector3( 8192f, 8192f, 1024f ) );

        for ( var i = -3; i <= 3; ++i )
        {
            BuildHouse( new Vector3( i * 512f, 512f, 512f ), Rand.Int( 2, 10 ) );
            BuildHouse( new Vector3( i * 512f, -512f, 512f ), Rand.Int( 2, 10 ) );
        }
    }

    private void AddCube( Vector3 min, Vector3 max )
    {
        CsgWorld.Add( CubeBrush, DefaultMaterial, (min + max) * 0.5f, max - min );
    }

    private void SubtractCube( Vector3 min, Vector3 max )
    {
        CsgWorld.Subtract( CubeBrush, (min + max) * 0.5f, max - min );
    }

    private void BuildHouse( Vector3 floorPos, int floorCount )
    {
        const float width = 384f;
        const float depth = 256f;
        const float floorHeight = 128f;
        const float windowHeight = 64f;
        const float windowWidth = 128f;
        const float windowFloorOffset = 32f;
        const float wallThickness = 16f;

        AddCube(
            floorPos - new Vector3( width * 0.5f, depth * 0.5f, 0f ),
            floorPos + new Vector3( width * 0.5f, depth * 0.5f, floorHeight * floorCount ) );

        Vector3 windowPos;

        for ( var i = 0; i < floorCount; ++i )
        {
            SubtractCube(
                floorPos - new Vector3( width * 0.5f - wallThickness, depth * 0.5f - wallThickness, 0f ),
                floorPos + new Vector3( width * 0.5f - wallThickness, depth * 0.5f - wallThickness, floorHeight - wallThickness ) );

            windowPos = floorPos + new Vector3( -width * 0.25f, (depth - wallThickness) * 0.5f, windowFloorOffset );

            SubtractCube(
                windowPos - new Vector3( windowWidth * 0.5f, wallThickness * 0.5f, 0f ),
                windowPos + new Vector3( windowWidth * 0.5f, wallThickness * 0.5f, windowHeight ) );

            windowPos = floorPos + new Vector3( width * 0.25f, (depth - wallThickness) * 0.5f, windowFloorOffset );

            SubtractCube(
                windowPos - new Vector3( windowWidth * 0.5f, wallThickness * 0.5f, 0f ),
                windowPos + new Vector3( windowWidth * 0.5f, wallThickness * 0.5f, windowHeight ) );

            windowPos = floorPos + new Vector3( -width * 0.25f, -(depth - wallThickness) * 0.5f, windowFloorOffset );

            SubtractCube(
                windowPos - new Vector3( windowWidth * 0.5f, wallThickness * 0.5f, 0f ),
                windowPos + new Vector3( windowWidth * 0.5f, wallThickness * 0.5f, windowHeight ) );

            windowPos = floorPos + new Vector3( width * 0.25f, -(depth - wallThickness) * 0.5f, windowFloorOffset );

            SubtractCube(
                windowPos - new Vector3( windowWidth * 0.5f, wallThickness * 0.5f, 0f ),
                windowPos + new Vector3( windowWidth * 0.5f, wallThickness * 0.5f, windowHeight ) );

            floorPos += Vector3.Up * floorHeight;
        }
    }
}
