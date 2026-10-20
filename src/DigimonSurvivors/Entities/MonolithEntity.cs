using System.Windows;
using System.Windows.Media;

namespace DigimonSurvivors.Entities;

public class MonolithEntity
{
    public Point Position;
    public double MaxHp = 60.0;
    public double CurrentHp = 60.0;
    public double Radius = 36.0;
    public double Width = 52.0;
    public double Height = 72.0;
    public double HitFlashTimer = 0.0;
    public bool IsAlive => CurrentHp > 0;
    public double PulseTimer = 0.0;

    public bool TakeDamage(double dmg)
    {
        if (CurrentHp <= 0) return false;
        CurrentHp -= dmg;
        HitFlashTimer = 0.12;
        if (CurrentHp <= 0)
        {
            CurrentHp = 0;
            return true;
        }
        return false;
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
