using System;
using System.Linq;
using Sandbox;
using Sandbox.Csg;

namespace CsgDemo;

partial class Player : Sandbox.Player
{
	public ClothingContainer Clothing { get; } = new();

	public TimeSince LastJump { get; private set; }

	public Player()
	{

	}

	public Player( Client cl )
	{
		Clothing.LoadFromClient( cl );
	}

	public override void Respawn()
	{
		SetModel( "models/citizen/citizen.vmdl" );

		Controller = new WalkController();
		Animator = new StandardPlayerAnimator();
		CameraMode = new ThirdPersonCamera();

		Clothing.DressEntity( this );

		EnableAllCollisions = true;
		EnableDrawing = true;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;
		
		base.Respawn();
	}

	public override void OnKilled()
	{
		base.OnKilled();

		Controller = null;

		EnableAllCollisions = false;
		EnableDrawing = false;
	}
	
	public override void Simulate( Client cl )
	{
		base.Simulate( cl );

		if ( Input.Pressed( InputButton.Jump ) )
		{
			if ( LastJump < 0.25f )
			{
				if ( Controller is NoclipController )
				{
					Controller = new WalkController();
				}
				else
				{
					Controller = new NoclipController();
				}
			}

			LastJump = 0f;
		}

		if ( !IsServer ) return;
		
		if ( Input.Pressed( InputButton.PrimaryAttack ) )
		{
			var ray = new Ray( EyePosition, EyeRotation.Forward );

			if ( Trace.Ray( ray, 8192f )
				    .Ignore( this )
				    .Run() is { Hit: true, Entity: CsgSolid solid, HitPosition: var pos } )
			{
				Log.Info( "Hit!" );

				solid.Modify( CsgDemoGame.Current.DodecahedronBrush, CsgOperator.Subtract, pos, Random.Shared.NextSingle() * 128f + 128f, Rotation.Random );
			}
		}
	}
}
