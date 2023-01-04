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

    public Player( IClient client )
    {
        Clothing.LoadFromClient( client );
    }

    public override void Respawn()
    {
        SetModel( "models/citizen/citizen.vmdl" );

        Controller = new WalkController();

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

    public override void Simulate( IClient client )
    {
        base.Simulate( client );

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

        SimulateAnimation(Controller);

        if ( !IsServer ) return;

        if ( Input.Pressed( InputButton.PrimaryAttack ) || Input.Pressed( InputButton.SecondaryAttack ) )
        {
            var ray = new Ray( EyePosition, EyeRotation.Forward );
            var add = Input.Pressed( InputButton.SecondaryAttack );

            if ( Trace.Ray( ray, 8192f ).Ignore( this ).Run() is { Hit: true, HitPosition: var pos } hit )
            {
                var rotation = Rotation.Random;
                var scale = Random.NextSingle() * 16f + 128f;

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
    
    // copy-paste from sandbox
    private void SimulateAnimation( PawnController controller )
    {
        if (controller == null)
            return;

        // where should we be rotated to
        var turnSpeed = 0.02f;

        Rotation rotation;

        // If we're a bot, spin us around 180 degrees.
        if ( Client.IsBot )
            rotation = ViewAngles.WithYaw(ViewAngles.yaw + 180f).ToRotation();
        else
            rotation = ViewAngles.ToRotation();

        var idealRotation = Rotation.LookAt(rotation.Forward.WithZ(0), Vector3.Up);
        Rotation = Rotation.Slerp(Rotation, idealRotation, controller.WishVelocity.Length * Time.Delta * turnSpeed);
        Rotation = Rotation.Clamp(idealRotation, 45.0f, out var shuffle); // lock facing to within 45 degrees of look direction

        CitizenAnimationHelper animHelper = new CitizenAnimationHelper(this);

        animHelper.WithWishVelocity(controller.WishVelocity);
        animHelper.WithVelocity(controller.Velocity);
        animHelper.WithLookAt(EyePosition + EyeRotation.Forward * 100.0f, 1.0f, 1.0f, 0.5f);
        animHelper.AimAngle = rotation;
        animHelper.FootShuffle = shuffle;
        animHelper.DuckLevel = MathX.Lerp(animHelper.DuckLevel, controller.HasTag("ducked") ? 1 : 0, Time.Delta * 10.0f);
        animHelper.VoiceLevel = ( IsClient && Client.IsValid() ) ? Client.Voice.LastHeard < 0.5f ? Client.Voice.CurrentLevel : 0.0f : 0.0f;
        animHelper.IsGrounded = GroundEntity != null;
        animHelper.IsSitting = controller.HasTag("sitting");
        animHelper.IsNoclipping = controller.HasTag("noclip");
        animHelper.IsClimbing = controller.HasTag("climbing");
        animHelper.IsSwimming = this.GetWaterLevel() >= 0.5f;
        animHelper.IsWeaponLowered = false;

        if (controller.HasEvent("jump")) animHelper.TriggerJump();

        animHelper.HoldType = CitizenAnimationHelper.HoldTypes.None;
        animHelper.AimBodyWeight = 0.5f;
    }

    public override void FrameSimulate( IClient client )
    {
        Camera.Rotation = ViewAngles.ToRotation();
        Camera.FieldOfView = Screen.CreateVerticalFieldOfView(Game.Preferences.FieldOfView);

        // copy-paste from sandbox
        Camera.FirstPersonViewer = null;

        Vector3 targetPos;
        var center = Position + Vector3.Up * 64;

        var pos = center;
        var rot = Camera.Rotation * Rotation.FromAxis(Vector3.Up, -16);

        var distance = 130.0f * Scale;
        targetPos = pos + rot.Right * ((CollisionBounds.Mins.x + 32) * Scale);
        targetPos += rot.Forward * -distance;

        var tr = Trace.Ray(pos, targetPos)
            .WithAnyTags("solid")
            .Ignore(this)
            .Radius(8)
            .Run();

        Camera.Position = tr.EndPosition;
    }
}
