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
        
        if ( Input.Pressed( InputButton.PrimaryAttack ) || Input.Pressed( InputButton.SecondaryAttack ) )
        {
            var ray = new Ray( EyePosition, EyeRotation.Forward );
            var add = Input.Pressed( InputButton.SecondaryAttack );

            if ( Trace.Ray( ray, 8192f ).Ignore( this ).Run() is { Hit: true, HitPosition: var pos } hit )
            {
                var rotation = Rotation.Random;
                var scale = Random.Shared.NextSingle() * 16f + 128f;

                if ( add )
                {
                    if ( hit.Entity is CsgSolid solid )
                    {
                        solid.Add( CsgDemoGame.Current.CubeBrush, CsgDemoGame.Current.RedMaterial, pos, scale, rotation );
                    }
                }
                else
                {
                    foreach ( var solid in Entity.All.OfType<CsgSolid>() )
                    {
                        solid.Subtract( CsgDemoGame.Current.DodecahedronBrush, pos, scale, rotation );
                        solid.Paint( CsgDemoGame.Current.DodecahedronBrush, CsgDemoGame.Current.ScorchedMaterial, pos, scale + 16f, rotation );
                    }
                }
            }
        }
    }
}
