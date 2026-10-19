using System.Windows;
using System.Windows.Media;

namespace DigimonSurvivors.Entities;

public class MonolithEntity
{
    public Point Position;
    public double MaxHp = 150.0;
    public double CurrentHp = 150.0;
    public double Radius = 28.0;
    public double Width = 46.0;
    public double Height = 64.0;
    public double HitFlashTimer = 0.0;
    public bool IsAlive => CurrentHp > 0;
    public double PulseTimer = 0.0;

    public void TakeDamage(double dmg)
    {
        CurrentHp -= dmg;
        HitFlashTimer = 0.12;
        if (CurrentHp < 0) CurrentHp = 0;
    }

    public void Update(double dt)
    {
        PulseTimer += dt;
        if (HitFlashTimer > 0)
        {
            HitFlashTimer -= dt;
        }
    }
}
