using System.Windows;
using System.Windows.Media;

namespace DigimonSurvivors.Entities;

public class ProjectileEntity
{
    public Point Position;
    public Vector Velocity;
    public double Damage;
    public int PierceLeft;
    public double Radius;
    public Color ParticleColor;
    public double LifeTime;
    public double MaxLifeTime;
    public bool IsAlive = true;
    public string SkillId = string.Empty;
    public bool IsHoming = false;
    public EnemyEntity? HomingTarget = null;
    public bool IsOrbiting = false;
    public double OrbitAngle = 0.0;
    public double OrbitRadius = 70.0;
    public double OrbitSpeed = 4.0;
    public bool ExplodesOnHit = false;
    public double ExplosionRadius = 0.0;

    public void Update(double dt, Point playerPos)
    {
        LifeTime += dt;
        if (LifeTime >= MaxLifeTime)
        {
            IsAlive = false;
            return;
        }

        if (IsOrbiting)
        {
            OrbitAngle += OrbitSpeed * dt;
            Position.X = playerPos.X + Math.Cos(OrbitAngle) * OrbitRadius;
            Position.Y = playerPos.Y + Math.Sin(OrbitAngle) * OrbitRadius;
        }
        else if (IsHoming && HomingTarget != null && HomingTarget.IsAlive)
        {
            var dir = new Vector(HomingTarget.Position.X - Position.X, HomingTarget.Position.Y - Position.Y);
            if (dir.LengthSquared > 1)
            {
                dir.Normalize();
                double speed = Velocity.Length;
                if (speed < 10) speed = 280;
                Velocity = dir * speed;
            }
            Position += Velocity * dt;
        }
        else
        {
            Position += Velocity * dt;
        }
    }
}
