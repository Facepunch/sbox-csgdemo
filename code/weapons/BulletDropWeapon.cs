﻿using Sandbox;
using System.Collections.Generic;

namespace CsgDemo;

public abstract partial class BulletDropWeapon<T> : Weapon where T : BulletDropProjectile, new()
{
	public virtual string ProjectileModel => "";
	public virtual float ProjectileRadius => 10f;
	public virtual float ProjectileLifeTime => 10f;
	public virtual string ImpactEffect => null;
	public virtual string TrailEffect => null;
	public virtual string HitSound => null;
	public virtual float InheritVelocity => 0f;
	public virtual string MuzzleAttachment => "muzzle";
	public virtual float BulletRange => 10000f;
	public virtual float DamageFalloffStart => 2000f;
	public virtual DamageFlags DamageType => DamageFlags.Bullet;
	public virtual float DamageFalloffEnd => 6000f;
	public virtual float BaseDamage => 20f;
	public virtual float Gravity => 50f;
	public virtual float Speed => 2000f;
	public virtual float Spread => 0.05f;

	public override void AttackPrimary()
	{
		if ( Prediction.FirstTime )
        {
			Rand.SetSeed( Time.Tick );
			FireProjectile();
        }
	}

	public virtual void FireProjectile()
	{
		if ( Owner is not Player player )
			return;

		var projectile = new T()
		{
			ExplosionEffect = ImpactEffect,
			FaceDirection = true,
			IgnoreEntity = this,
			TrailEffect = TrailEffect,
			Simulator = player.Projectiles,
			Attacker = player,
			HitSound = HitSound,
			LifeTime = ProjectileLifeTime,
			Gravity = Gravity,
			ModelName = ProjectileModel
		};

		OnCreateProjectile( projectile );

		var position = player.EyePosition + player.EyeRotation.Forward * 80f;
		var muzzle = GetAttachment( MuzzleAttachment );

		if ( muzzle.HasValue )
		{
			position = muzzle.Value.Position;
		}

		var forward = player.EyeRotation.Forward;
		var endPosition = player.EyePosition + forward * BulletRange;
		var trace = Trace.Ray( player.EyePosition, endPosition )
			.WithAnyTags( "weapon", "player", "solid" )
			.Ignore( player )
			.Ignore( this )
			.Run();

		var direction = (trace.EndPosition - position).Normal;
		direction += (Vector3.Random + Vector3.Random + Vector3.Random + Vector3.Random) * Spread * 0.25f;
		direction = direction.Normal;

		var velocity = (direction * Speed) + (player.Velocity * InheritVelocity);
		velocity = AdjustProjectileVelocity( velocity );
		projectile.Initialize( position, velocity, ProjectileRadius, (p, t) => OnProjectileHit( (T)p, t ) );
	}

	protected void DealDamage( Entity target, Vector3 position, Vector3 force )
	{
		DealDamage( target, position, force, BaseDamage );
	}

	protected void DealDamage( Entity target, Vector3 position, Vector3 force, float damage )
	{
		var damageInfo = new DamageInfo()
			.WithAttacker( Owner )
			.WithWeapon( this )
			.WithPosition( position )
			.WithForce( force )
			.WithFlag( DamageType );

		damageInfo.Damage = damage;

		target.TakeDamage( damageInfo );
	}

	protected virtual float ModifyDamage( Entity victim, float damage )
	{
		return damage;
	}

	protected virtual Vector3 AdjustProjectileVelocity( Vector3 velocity )
	{
		return velocity;
	}

	protected virtual void DamageInRadius( Vector3 position, float radius, float baseDamage, float force = 1f )
	{
		CsgDemoGame.Explosion( this, Owner, position, radius, baseDamage, force );
	}

	protected virtual void OnCreateProjectile( T projectile )
	{

	}

	protected virtual void OnProjectileHit( T projectile, TraceResult trace )
	{
		if ( IsServer && trace.Entity.IsValid() )
		{
			var damage = BaseDamage;
			DealDamage( trace.Entity, projectile.Position, projectile.Velocity * 0.1f, damage );
		}
	}
}
