using System;
using System.Windows;
using System.Windows.Media;

namespace DigimonSurvivors.Entities;

public enum FieldItemType
{
    Meat,        // 🍖 디지몬 고기: HP 35% 즉시 회복
    Magnet,      // 🧲 데이터 자석: 맵의 모든 경험치 비트 끌어당김
    Bomb,        // 💣 크래시 폭탄: 화면 내 모든 일반 적 소멸
    TimeFreeze   // ⏱️ 디지바이스 프리즈: 4초간 모든 적 빙결
}

public class FieldDropEntity
{
    public Point Position;
    public FieldItemType Type;
    public double Radius = 18.0;
    public double FloatTimer = 0.0;
    public bool IsCollected = false;
    public double LifeTime = 45.0; // 45초 후 자연 소멸

    public void Update(double dt, Point playerPos, double magnetRange)
    {
        FloatTimer += dt;
        LifeTime -= dt;
        if (LifeTime <= 0)
        {
            IsCollected = true;
            return;
        }

        // 플레이어 접근 시 획득 판정
        double dx = playerPos.X - Position.X;
        double dy = playerPos.Y - Position.Y;
        double distSq = dx * dx + dy * dy;

        // 아이템도 자석 범위의 50%에 들어오면 부드럽게 흡수
        double pullDist = Math.Max(magnetRange * 0.5, 70.0);
        if (distSq <= pullDist * pullDist)
        {
            double dist = Math.Max(1.0, Math.Sqrt(distSq));
            double speed = 400.0;
            Position.X += (dx / dist) * speed * dt;
            Position.Y += (dy / dist) * speed * dt;

            if (distSq <= 24.0 * 24.0)
            {
                IsCollected = true;
            }
        }
    }
}
