using System.Windows;
using System.Windows.Media;

namespace DigimonSurvivors.Entities;

public class AreaEffectEntity
{
    public Point Position;
    public double Radius;
    public double DamagePerTick;
    public double TickInterval;
    public double TickTimer;
    public double Duration;
    public double Elapsed;
    public bool IsAlive = true;
    public Color VisualColor;
    public string Name = string.Empty;
    public bool SlowsEnemies = false;

    public void Update(double dt)
    {
        Elapsed += dt;
        TickTimer += dt;
        if (Elapsed >= Duration)
        {
            IsAlive = false;
        }
    }
}
