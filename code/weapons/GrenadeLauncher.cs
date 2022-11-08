using Sandbox;
using Sandbox.Csg;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CsgDemo;

[Title( "Grenade Launcher" ), Category( "Weapons" )]
partial class GrenadeLauncher : BulletDropWeapon<GrenadeProjectile>
{
    public static readonly Model WorldModel = Model.Load( "weapons/rust_smg/rust_smg.vmdl" );
    public override string ViewModelPath => "weapons/rust_smg/v_rust_smg.vmdl";

    public override string ProjectileModel => "models/gameplay/projectiles/grenades/grenade.vmdl";
    public override string TrailEffect => "particles/grenade.vpcf";
    public override float? ProjectileLifeTime => null;
    public override float Spread => 0.0f;
    public override string HitSound => "gl.impact";
    public override float Gravity => 30f;
    public override float Speed => 1300f;
    public override float PrimaryRate => 10f;
    public override AmmoType AmmoType => AmmoType.Grenade;
    
    public override void Spawn()
    {
        base.Spawn();

        Model = WorldModel;
    }

    public override void AttackPrimary()
    {
        TimeSincePrimaryAttack = 0;
        TimeSinceSecondaryAttack = 0;

        if ( Owner is not Player player )
            return;

        PlaySound( "gl.shoot" );
        Reload();

        player.SetAnimParameter( "b_attack", true );

        base.AttackPrimary();
    }

    public override void SimulateAnimator( PawnAnimator anim )
    {
        anim.SetAnimParameter( "holdtype", 3 );
        anim.SetAnimParameter( "aim_body_weight", 1.0f );
    }

    protected override Vector3 AdjustProjectileVelocity( Vector3 velocity )
    {
        return velocity + Vector3.Up * 400f;
    }

    protected override void OnCreateProjectile( GrenadeProjectile projectile )
    {
        projectile.BounceSoundMinimumVelocity = 50f;
        projectile.Bounciness = 0.8f;
        projectile.BounceSound = "gl.impact";
        projectile.FromWeapon = this;

        base.OnCreateProjectile( projectile );
    }

    [ThreadStatic]
    private static List<Transform> _sCarveTransforms;

    protected override void OnProjectileHit( GrenadeProjectile projectile, TraceResult trace )
    {
        CsgDemoGame.Explosion( projectile, projectile.Attacker, projectile.Position, 140f, 100f, 1f );

        if ( !IsServer )
        {
            return;
        }

        var tr = Trace.Sphere( 256f, trace.StartPosition, trace.StartPosition )
            .Ignore( this )
            .Ignore( projectile )
            .Run();

        if ( tr is not { Hit: true } )
        {
            return;
        }

        var rotation = Rotation.Random;
        var scale = Random.Shared.NextSingle() * 32f + 96f;
        var pos = projectile.Position;

        _sCarveTransforms ??= new List<Transform>();
        _sCarveTransforms.Clear();

        _sCarveTransforms.Add( new Transform( pos, rotation, scale ) );

        for ( var i = 0; i < 8; i++ )
        {
            rotation = Rotation.Random;
            scale = Random.Shared.NextSingle() * 64f + 16f;
            pos = projectile.Position + Vector3.Random * scale;

            _sCarveTransforms.Add( new Transform( pos, rotation, scale ) );
        }

        //DebugOverlay.Sphere( pos, scale, Color.Random, 10f );

        foreach ( var solid in All.OfType<CsgSolid>() )
        {
            // Apply subtractions before painting so we don't needlessly paint something we then subtract

            foreach ( var transform in _sCarveTransforms )
            {
                solid.Subtract( CsgDemoGame.Current.DodecahedronBrush,
                    transform.Position, transform.Scale, transform.Rotation );
            }

            foreach ( var transform in _sCarveTransforms )
            {
                solid.Paint( CsgDemoGame.Current.DodecahedronBrush, CsgDemoGame.Current.ScorchedMaterial,
                    transform.Position, transform.Scale + 8f, transform.Rotation );
            }
        }
    }
}
