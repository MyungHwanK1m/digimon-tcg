using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DigimonSurvivors.Engine;

public enum ParticleType
{
    Spark,          // 타격 스파크 (불꽃/빛)
    DigitalBit,     // 적 처치 시 흩어지는 사각 데이터 조각
    Shockwave,      // 폭발 시 팽창하는 충격파 링
    CyberTrail,     // 대시 잔상 또는 궤적
    FloatingMote,   // 배경 디지털 부유 입자
    TextPop,        // 특수 팝업 (CRIT!)
    FlameEmber,     // 화염 불씨
    AirRing,        // 공기 충격파 링
    WaterBubble,    // 물방울 거품
    ElectricArc,    // 번개 스파크
    DarkMiasma,     // 암흑 안개
    FrostSparkle,   // 빙결 눈꽃
    HolySparkle     // 성스러운 별빛
}

public class Particle
{
    public ParticleType Type;
    public Point Position;
    public Vector Velocity;
    public Color Color;
    public double Size;
    public double MaxSize;
    public double Life;
    public double MaxLife;
    public double Rotation;
    public double RotationSpeed;
    public bool IsAlive => Life < MaxLife;
    public double Alpha => Math.Clamp(1.0 - (Life / MaxLife), 0.0, 1.0);
}

public class ParticleSystem
{
    private readonly List<Particle> _particles = new(1000);
    private readonly List<Particle> _ambientMotes = new(60);
    private readonly Random _rnd = new();

    public void InitAmbient(Rect arena)
    {
        _ambientMotes.Clear();
        for (int i = 0; i < 50; i++)
        {
            _ambientMotes.Add(new Particle
            {
                Type = ParticleType.FloatingMote,
                Position = new Point(
                    arena.Left + _rnd.NextDouble() * arena.Width,
                    arena.Top + _rnd.NextDouble() * arena.Height
                ),
                Velocity = new Vector((_rnd.NextDouble() - 0.5) * 15.0, -20.0 - _rnd.NextDouble() * 30.0),
                Color = Color.FromArgb((byte)(40 + _rnd.Next(60)), 0, (byte)(180 + _rnd.Next(75)), 255),
                Size = 2.0 + _rnd.NextDouble() * 3.5,
                Life = 0,
                MaxLife = 999999
            });
        }
    }

    public void AddHitSparks(Point pos, Vector hitDir, Color color, int count = 8)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = Math.Atan2(hitDir.Y, hitDir.X) + (_rnd.NextDouble() - 0.5) * 1.6;
            double speed = 100.0 + _rnd.NextDouble() * 250.0;
            _particles.Add(new Particle
            {
                Type = ParticleType.Spark,
                Position = pos,
                Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
                Color = color,
                Size = 3.5 + _rnd.NextDouble() * 3.0,
                Life = 0,
                MaxLife = 0.25 + _rnd.NextDouble() * 0.2,
                Rotation = _rnd.NextDouble() * Math.PI * 2,
                RotationSpeed = (_rnd.NextDouble() - 0.5) * 20.0
            });
        }
    }

    public void AddDigitalDeathBits(Point pos, Color color, int count = 16)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rnd.NextDouble() * Math.PI * 2.0;
            double speed = 80.0 + _rnd.NextDouble() * 180.0;
            _particles.Add(new Particle
            {
                Type = ParticleType.DigitalBit,
                Position = pos,
                Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
                Color = color,
                Size = 4.0 + _rnd.NextDouble() * 5.0,
                Life = 0,
                MaxLife = 0.45 + _rnd.NextDouble() * 0.35,
                Rotation = _rnd.NextDouble() * Math.PI * 2,
                RotationSpeed = (_rnd.NextDouble() - 0.5) * 12.0
            });
        }
    }

    public void AddShockwave(Point pos, double maxRadius, Color color, double duration = 0.4)
    {
        _particles.Add(new Particle
        {
            Type = ParticleType.Shockwave,
            Position = pos,
            Size = 5.0,
            MaxSize = maxRadius,
            Color = color,
            Life = 0,
            MaxLife = duration
        });
    }

    public void AddCyberTrail(Point pos, double size, Color color)
    {
        _particles.Add(new Particle
        {
            Type = ParticleType.CyberTrail,
            Position = pos,
            Velocity = new Vector(0, 0),
            Color = color,
            Size = size,
            Life = 0,
            MaxLife = 0.28
        });
    }

    public void AddSkillTrail(Point pos, Vector vel, string skillId, Color color)
    {
        string id = skillId?.ToLowerInvariant() ?? "";
        Vector trailVel = -vel * 0.12 + new Vector((_rnd.NextDouble() - 0.5) * 35.0, (_rnd.NextDouble() - 0.5) * 35.0);

        if (id is "baby_flame" or "mega_flame" or "spit_fire" or "crimson_flare")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.FlameEmber,
                Position = pos,
                Velocity = trailVel + new Vector(0, -25.0),
                Color = _rnd.NextDouble() < 0.5 ? Color.FromRgb(255, 140, 0) : Color.FromRgb(255, 230, 40),
                Size = 4.0 + _rnd.NextDouble() * 4.0,
                Life = 0,
                MaxLife = 0.25 + _rnd.NextDouble() * 0.15
            });
        }
        else if (id is "air_shot" or "sparking_air" or "spiral_twister")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.AirRing,
                Position = pos,
                Velocity = new Vector(0, 0),
                Color = Color.FromArgb(140, 220, 245, 255),
                Size = 5.0,
                MaxSize = 22.0,
                Life = 0,
                MaxLife = 0.2
            });
        }
        else if (id is "surfing_fish" or "water_breath" or "bubble_blow")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.WaterBubble,
                Position = pos + new Vector((_rnd.NextDouble() - 0.5) * 14.0, (_rnd.NextDouble() - 0.5) * 14.0),
                Velocity = trailVel * 0.4 + new Vector(0, -15.0),
                Color = Color.FromArgb(180, 60, 200, 255),
                Size = 3.0 + _rnd.NextDouble() * 4.0,
                Life = 0,
                MaxLife = 0.35
            });
        }
        else if (id is "petit_thunder" or "hammer_spark" or "mega_blaster" or "giga_blaster" or "horn_buster" or "dynamo_shock")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.ElectricArc,
                Position = pos,
                Velocity = new Vector((_rnd.NextDouble() - 0.5) * 80.0, (_rnd.NextDouble() - 0.5) * 80.0),
                Color = Color.FromRgb(80, 235, 255),
                Size = 4.0 + _rnd.NextDouble() * 5.0,
                Life = 0,
                MaxLife = 0.15
            });
        }
        else if (id is "pico_dart" or "darkness_wave" or "dark_matter" or "night_raid" or "bat_flutter" or "dark_whisper")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.DarkMiasma,
                Position = pos,
                Velocity = trailVel * 0.5,
                Color = Color.FromArgb(160, 170, 0, 255),
                Size = 5.0 + _rnd.NextDouble() * 5.0,
                Life = 0,
                MaxLife = 0.3
            });
        }
        else if (id is "ice_arrow" or "cocytus_breath" or "sorrow_blue" or "ice_dash" or "grace_cross_freezer")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.FrostSparkle,
                Position = pos,
                Velocity = trailVel * 0.3,
                Color = Color.FromRgb(160, 240, 255),
                Size = 3.5 + _rnd.NextDouble() * 3.0,
                Life = 0,
                MaxLife = 0.28
            });
        }
        else if (id is "holy_arrow" or "heavens_knuckle" or "heaven_gate" or "starlight_explosion" or "strike_of_the_seven_stars" or "holy_rod")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.HolySparkle,
                Position = pos,
                Velocity = trailVel * 0.4,
                Color = Color.FromRgb(255, 230, 80),
                Size = 4.0 + _rnd.NextDouble() * 3.5,
                Life = 0,
                MaxLife = 0.3
            });
        }
        else if (id is "giga_destroyer" or "hyper_cannon" or "infinity_cannon" or "ground_zero" or "harpoon_torpedo")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.FlameEmber,
                Position = pos - vel * 0.04,
                Velocity = -vel * 0.25 + new Vector((_rnd.NextDouble() - 0.5) * 40.0, (_rnd.NextDouble() - 0.5) * 40.0),
                Color = _rnd.NextDouble() < 0.6 ? Color.FromRgb(255, 90, 0) : Color.FromArgb(120, 80, 80, 80),
                Size = 5.0 + _rnd.NextDouble() * 5.0,
                Life = 0,
                MaxLife = 0.25
            });
        }
        else if (id is "shadow_wing" or "shadow_wing_ex" or "kaiser_nail" or "grey_sword" or "trump_sword" or "dramon_killer" or "haouken")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.Spark,
                Position = pos,
                Velocity = trailVel * 0.4 + new Vector((_rnd.NextDouble() - 0.5) * 25.0, (_rnd.NextDouble() - 0.5) * 25.0),
                Color = Color.FromRgb(255, 180, 60),
                Size = 3.0 + _rnd.NextDouble() * 3.5,
                Life = 0,
                MaxLife = 0.22
            });
        }
        else if (id is "goburi_strike" or "woody_smash" or "leaf_cutter" or "poison_ivy")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.Spark,
                Position = pos + new Vector((_rnd.NextDouble() - 0.5) * 12.0, (_rnd.NextDouble() - 0.5) * 12.0),
                Velocity = trailVel * 0.35,
                Color = Color.FromRgb(80, 240, 130),
                Size = 3.0 + _rnd.NextDouble() * 3.0,
                Life = 0,
                MaxLife = 0.25
            });
        }
        else if (id is "petit_fire" or "fox_fire")
        {
            _particles.Add(new Particle
            {
                Type = ParticleType.FlameEmber,
                Position = pos,
                Velocity = -vel * 0.08 + new Vector((_rnd.NextDouble() - 0.5) * 16.0, (_rnd.NextDouble() - 0.5) * 16.0),
                Color = id == "petit_fire" ? Color.FromRgb(60, 220, 255) : Color.FromRgb(140, 230, 255),
                Size = 3.5 + _rnd.NextDouble() * 3.0,
                Life = 0,
                MaxLife = 0.3
            });
        }
    }

    public void Update(double dt, Rect arena)
    {
        // 1. 일반 파티클 업데이트
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life += dt;
            if (!p.IsAlive)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;
            p.Velocity *= Math.Max(0.0, 1.0 - dt * 3.5); // 감속
            p.Rotation += p.RotationSpeed * dt;

            if (p.Type == ParticleType.Shockwave)
            {
                double progress = p.Life / p.MaxLife;
                p.Size = 5.0 + (p.MaxSize - 5.0) * Math.Sin(progress * Math.PI * 0.5);
            }
            else if (p.Type == ParticleType.AirRing)
            {
                double progress = p.Life / p.MaxLife;
                p.Size = 5.0 + (p.MaxSize - 5.0) * progress;
            }
        }

        // 2. 배경 부유 데이터 모트 업데이트
        foreach (var m in _ambientMotes)
        {
            m.Position += m.Velocity * dt;
            if (m.Position.Y < arena.Top)
            {
                m.Position.Y = arena.Bottom;
                m.Position.X = arena.Left + _rnd.NextDouble() * arena.Width;
            }
            if (m.Position.X < arena.Left) m.Position.X = arena.Right;
            if (m.Position.X > arena.Right) m.Position.X = arena.Left;
        }
    }

    public void Draw(DrawingContext dc)
    {
        // 1. 부유 데이터 모트 드로잉 (은은한 사각 디지털 픽셀)
        foreach (var m in _ambientMotes)
        {
            var brush = new SolidColorBrush(m.Color);
            brush.Freeze();
            dc.DrawRectangle(brush, null, new Rect(m.Position.X - m.Size / 2, m.Position.Y - m.Size / 2, m.Size, m.Size));
        }

        // 2. 활성 파티클 드로잉
        foreach (var p in _particles)
        {
            if (!p.IsAlive) continue;
            byte alpha = (byte)(p.Alpha * 255);
            var col = Color.FromArgb(alpha, p.Color.R, p.Color.G, p.Color.B);

            switch (p.Type)
            {
                case ParticleType.Spark:
                case ParticleType.FlameEmber:
                {
                    var b = new RadialGradientBrush(
                        Color.FromArgb(alpha, 255, 255, 230),
                        Color.FromArgb(0, p.Color.R, p.Color.G, p.Color.B));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alpha * 0.75), p.Color.R, p.Color.G, p.Color.B), 0.45));
                    b.Freeze();
                    dc.DrawEllipse(b, null, p.Position, p.Size, p.Size);
                    break;
                }
                case ParticleType.DigitalBit:
                {
                    var b = new SolidColorBrush(col);
                    b.Freeze();
                    dc.PushTransform(new RotateTransform(p.Rotation * 180.0 / Math.PI, p.Position.X, p.Position.Y));
                    dc.DrawRectangle(b, null, new Rect(p.Position.X - p.Size / 2, p.Position.Y - p.Size / 2, p.Size, p.Size));
                    dc.Pop();
                    break;
                }
                case ParticleType.Shockwave:
                case ParticleType.AirRing:
                {
                    double lifeRatio = Math.Clamp(1.0 - p.Life / p.MaxLife, 0.0, 1.0);
                    var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha * lifeRatio), p.Color.R, p.Color.G, p.Color.B)), 2.0 * lifeRatio);
                    pen.Freeze();
                    dc.DrawEllipse(null, pen, p.Position, p.Size, p.Size);
                    break;
                }
                case ParticleType.CyberTrail:
                {
                    var b = new RadialGradientBrush(Color.FromArgb((byte)(alpha * 0.5), p.Color.R, p.Color.G, p.Color.B), Color.FromArgb(0, p.Color.R, p.Color.G, p.Color.B));
                    b.Freeze();
                    dc.DrawEllipse(b, null, p.Position, p.Size, p.Size);
                    break;
                }
                case ParticleType.WaterBubble:
                {
                    var b = new RadialGradientBrush(
                        Color.FromArgb((byte)(alpha * 0.7), 230, 250, 255),
                        Color.FromArgb(0, p.Color.R, p.Color.G, p.Color.B));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alpha * 0.35), p.Color.R, p.Color.G, p.Color.B), 0.6));
                    b.Freeze();
                    dc.DrawEllipse(b, null, p.Position, p.Size, p.Size);
                    break;
                }
                case ParticleType.ElectricArc:
                {
                    double dx = (_rnd.NextDouble() - 0.5) * p.Size * 2.2;
                    double dy = (_rnd.NextDouble() - 0.5) * p.Size * 2.2;
                    Point pEnd = new(p.Position.X + dx, p.Position.Y + dy);

                    var penGlow = new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.5), 0, 220, 255)), 4.0);
                    penGlow.Freeze();
                    dc.DrawLine(penGlow, p.Position, pEnd);

                    var penCore = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255)), 1.5);
                    penCore.Freeze();
                    dc.DrawLine(penCore, p.Position, pEnd);
                    break;
                }
                case ParticleType.DarkMiasma:
                {
                    var b = new RadialGradientBrush(
                        Color.FromArgb((byte)(alpha * 0.6), p.Color.R, p.Color.G, p.Color.B),
                        Color.FromArgb(0, 20, 0, 40));
                    b.Freeze();
                    dc.DrawEllipse(b, null, p.Position, p.Size * 1.6, p.Size * 1.6);
                    break;
                }
                case ParticleType.FrostSparkle:
                {
                    double s = p.Size;
                    var pen = new Pen(new SolidColorBrush(col), 1.5);
                    pen.Freeze();
                    dc.DrawLine(pen, new Point(p.Position.X - s, p.Position.Y), new Point(p.Position.X + s, p.Position.Y));
                    dc.DrawLine(pen, new Point(p.Position.X, p.Position.Y - s), new Point(p.Position.X, p.Position.Y + s));

                    var glint = new RadialGradientBrush(Color.FromArgb(alpha, 255, 255, 255), Color.FromArgb(0, 160, 240, 255));
                    glint.Freeze();
                    dc.DrawEllipse(glint, null, p.Position, s * 0.6, s * 0.6);
                    break;
                }
                case ParticleType.HolySparkle:
                {
                    double s = p.Size * 1.4;
                    var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 240)), 1.8);
                    pen.Freeze();
                    dc.DrawLine(pen, new Point(p.Position.X - s, p.Position.Y), new Point(p.Position.X + s, p.Position.Y));
                    dc.DrawLine(pen, new Point(p.Position.X, p.Position.Y - s), new Point(p.Position.X, p.Position.Y + s));

                    var glow = new RadialGradientBrush(Color.FromArgb(alpha, 255, 255, 255), Color.FromArgb(0, 255, 215, 0));
                    glow.Freeze();
                    dc.DrawEllipse(glow, null, p.Position, s * 0.7, s * 0.7);
                    break;
                }
            }
        }
    }

    public void Clear()
    {
        _particles.Clear();
    }
}
