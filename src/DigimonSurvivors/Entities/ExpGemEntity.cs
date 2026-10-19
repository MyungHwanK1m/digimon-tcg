using System.Windows;
using System.Windows.Media;

namespace DigimonSurvivors.Entities;

public enum ExpGemType
{
    SmallBit,   // 10 XP
    MediumBit,  // 50 XP
    LargeBit,   // 250 XP
    DigiEggBox  // 보스 보물상자 (골드/스킬 3개)
}

public class ExpGemEntity
{
    public Point Position;
    public ExpGemType Type;
    public int ExpValue;
    public bool IsBeingAttracted = false;
    public double AttractSpeed = 100.0;
    public bool IsCollected = false;
    public double SparkleTimer = 0.0;

    public void Update(double dt, Point playerPos, double magnetRange)
    {
        SparkleTimer += dt;
        double distSq = (playerPos.X - Position.X) * (playerPos.X - Position.X) +
                        (playerPos.Y - Position.Y) * (playerPos.Y - Position.Y);

        if (distSq <= magnetRange * magnetRange)
        {
            IsBeingAttracted = true;
        }

        if (IsBeingAttracted)
        {
            AttractSpeed += 900.0 * dt; // 가속도
            var dir = new Vector(playerPos.X - Position.X, playerPos.Y - Position.Y);
            double dist = dir.Length;
            if (dist < 24)
            {
                IsCollected = true;
            }
            else
            {
                dir.Normalize();
                Position += dir * AttractSpeed * dt;
            }
        }
    }
}

public class FloatingTextEntity
{
    public Point Position;
    public string Text = string.Empty;
    public Color Color = Colors.White;
    public double Size = 16.0;
    public double Duration = 0.8;
    public double Elapsed = 0.0;
    public double VelocityY = -45.0;
    public bool IsAlive = true;

    public void Update(double dt)
    {
        Elapsed += dt;
        Position.Y += VelocityY * dt;
        if (Elapsed >= Duration)
        {
            IsAlive = false;
        }
    }

    public double Alpha => Math.Clamp(1.0 - (Elapsed / Duration), 0.0, 1.0);
}
