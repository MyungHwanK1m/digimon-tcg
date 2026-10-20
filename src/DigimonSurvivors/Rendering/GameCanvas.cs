using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DigimonSurvivors.Engine;
using DigimonSurvivors.Entities;
using DigimonSurvivors.Models;

namespace DigimonSurvivors.Rendering;

public class GameCanvas : FrameworkElement
{
    private readonly Typeface _fontMain = new(new FontFamily("Segoe UI, Malgun Gothic"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private readonly Typeface _fontMono = new(new FontFamily("Consolas, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public GameEngine? Engine { get; set; }

    public GameCanvas()
    {
        ClipToBounds = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0 || Engine == null) return;

        // 1. 배경 어두운 디지털 월드 심연
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 12, 22)), null, new Rect(0, 0, w, h));

        if (Engine.Player == null) return;

        // 카메라 변환 (플레이어 중심 + 화면 흔들림 오프셋)
        Point cam = new(
            Engine.CameraPosition.X + Engine.ScreenShakeOffset.X,
            Engine.CameraPosition.Y + Engine.ScreenShakeOffset.Y
        );
        double halfW = w / 2.0;
        double halfH = h / 2.0;

        dc.PushTransform(new TranslateTransform(halfW - cam.X, halfH - cam.Y));

        // 2. 살아있는 디지털 서킷 및 그리드 배경
        DrawDigitalCircuits(dc, Engine.ArenaBounds, cam, w, h, Engine.GameTime);

        // 3. 아레나 외벽 일렉트릭 네온 배리어
        DrawArenaBarrier(dc, Engine.ArenaBounds, Engine.GameTime);

        // 4. 파괴 가능한 디지털 데이터 모놀리스
        foreach (var mono in Engine.Monoliths)
        {
            DrawMonolith(dc, mono);
        }

        // 5. 특수 필드 아이템 (고기, 자석, 폭탄, 타임프리즈)
        foreach (var drop in Engine.FieldDrops)
        {
            DrawFieldDrop(dc, drop);
        }

        // 6. 지속 장판 (Area Effects)
        foreach (var area in Engine.AreaEffects)
        {
            if (!area.IsAlive) continue;
            DrawAreaEffect(dc, area);
        }

        // 7. 경험치 보석 (Exp Gems)
        foreach (var gem in Engine.ExpGems)
        {
            DrawExpGem(dc, gem);
        }

        // 8. 파티클 이펙트 (Hit Sparks, Digital Bits, Trails, Shockwaves)
        if (GameSettings.ParticlesEnabled)
        {
            Engine.Particles.Draw(dc);
        }

        // 9. 적 (Enemies)
        foreach (var enemy in Engine.Enemies)
        {
            if (!enemy.IsAlive) continue;
            DrawEnemy(dc, enemy);
        }

        // 10. 플레이어 (Player)
        DrawPlayer(dc, Engine.Player);

        // 11. 투사체 (Projectiles)
        foreach (var proj in Engine.Projectiles)
        {
            if (!proj.IsAlive) continue;
            DrawProjectile(dc, proj);
        }

        // 12. 플로팅 데미지 & 알림 텍스트
        if (GameSettings.DamageNumbersEnabled)
        {
            foreach (var ft in Engine.FloatingTexts)
            {
                if (!ft.IsAlive) continue;
                byte alpha = (byte)(ft.Alpha * 255);
                var color = Color.FromArgb(alpha, ft.Color.R, ft.Color.G, ft.Color.B);
                var text = new FormattedText(ft.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, ft.Size, new SolidColorBrush(color), 1.0);
                dc.DrawText(text, new Point(ft.Position.X - text.Width / 2.0, ft.Position.Y));
            }
        }

        dc.Pop(); // 월드 변환 복원

        // ========================== 13. UI HUD & 오버레이 (스크린 고정 좌표) ==========================
        DrawHud(dc, w, h, Engine);

        // 보스 알람 경보 화면 테두리 비네트
        if (Engine.BossWarningTimer > 0)
        {
            DrawBossAlarmVignette(dc, w, h, Engine.BossWarningTimer);
        }
    }

    #region 디지털 월드 배경 & 아레나 배리어
    private void DrawDigitalCircuits(DrawingContext dc, Rect arena, Point cam, double viewW, double viewH, double time)
    {
        double step = 80.0;
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(24, 0, 160, 255)), 1.0);
        gridPen.Freeze();

        double startX = Math.Max(arena.Left, Math.Floor((cam.X - viewW / 2.0) / step) * step);
        double endX = Math.Min(arena.Right, Math.Ceiling((cam.X + viewW / 2.0) / step) * step);
        double startY = Math.Max(arena.Top, Math.Floor((cam.Y - viewH / 2.0) / step) * step);
        double endY = Math.Min(arena.Bottom, Math.Ceiling((cam.Y + viewH / 2.0) / step) * step);

        for (double x = startX; x <= endX; x += step)
        {
            dc.DrawLine(gridPen, new Point(x, arena.Top), new Point(x, arena.Bottom));
        }
        for (double y = startY; y <= endY; y += step)
        {
            dc.DrawLine(gridPen, new Point(arena.Left, y), new Point(arena.Right, y));
        }

        // 회로를 따라 이동하는 빛의 펄스 노드 점
        var pulseBrush = new SolidColorBrush(Color.FromArgb(80, 0, 229, 255));
        pulseBrush.Freeze();
        double pulseCycle = (time * 120.0) % (step * 4.0);

        for (double x = startX; x <= endX; x += step * 3.0)
        {
            double py = arena.Top + pulseCycle;
            if (py >= startY && py <= endY)
            {
                dc.DrawEllipse(pulseBrush, null, new Point(x, py), 3.0, 3.0);
            }
        }
    }

    private void DrawArenaBarrier(DrawingContext dc, Rect arena, double time)
    {
        byte pulseAlpha = (byte)(140 + Math.Sin(time * 6.0) * 60);
        var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(pulseAlpha, 0, 229, 255)), 4.0);
        glowPen.Freeze();
        dc.DrawRectangle(null, glowPen, arena);

        var innerPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(pulseAlpha * 0.4), 0, 255, 200)), 12.0);
        innerPen.Freeze();
        dc.DrawRectangle(null, innerPen, arena);
    }
    #endregion

    #region 모놀리스 & 특수 필드 드롭 아이템
    private void DrawMonolith(DrawingContext dc, MonolithEntity mono)
    {
        double pulse = Math.Sin(mono.PulseTimer * 4.0) * 0.15 + 0.85;
        var baseBrush = new SolidColorBrush(Color.FromArgb((byte)(pulse * 180), 0, 180, 255));
        var glowBrush = new RadialGradientBrush(Color.FromArgb(90, 0, 229, 255), Color.FromArgb(0, 0, 229, 255));

        dc.DrawEllipse(glowBrush, null, mono.Position, mono.Radius * 1.8, mono.Radius * 1.8);

        // 다면체 크리스탈 오벨리스크 드로잉
        double hw = mono.Width / 2.0;
        double hh = mono.Height / 2.0;
        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(mono.Position.X, mono.Position.Y - hh), IsClosed = true };
        fig.Segments.Add(new LineSegment(new Point(mono.Position.X + hw, mono.Position.Y), true));
        fig.Segments.Add(new LineSegment(new Point(mono.Position.X, mono.Position.Y + hh), true));
        fig.Segments.Add(new LineSegment(new Point(mono.Position.X - hw, mono.Position.Y), true));
        geo.Figures.Add(fig);

        var pen = new Pen(mono.HitFlashTimer > 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(0, 240, 255)), 2.0);
        dc.DrawGeometry(baseBrush, pen, geo);

        // 체력바 (손상 시)
        if (mono.CurrentHp < mono.MaxHp)
        {
            double barW = 44;
            double barH = 5;
            double barX = mono.Position.X - barW / 2;
            double barY = mono.Position.Y - hh - 12;
            double ratio = Math.Clamp(mono.CurrentHp / mono.MaxHp, 0.0, 1.0);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)), null, new Rect(barX, barY, barW, barH));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0, 229, 255)), null, new Rect(barX, barY, barW * ratio, barH));
        }
    }

    private void DrawFieldDrop(DrawingContext dc, FieldDropEntity drop)
    {
        double bobbing = Math.Sin(drop.FloatTimer * 6.0) * 4.0;
        Point pos = new(drop.Position.X, drop.Position.Y + bobbing);

        Color mainCol = drop.Type switch
        {
            FieldItemType.Meat => Color.FromRgb(255, 82, 82),
            FieldItemType.Magnet => Color.FromRgb(33, 150, 243),
            FieldItemType.Bomb => Color.FromRgb(255, 110, 64),
            FieldItemType.TimeFreeze => Color.FromRgb(0, 229, 255),
            _ => Colors.White
        };

        // 회전 후광
        var glow = new RadialGradientBrush(mainCol, Color.FromArgb(0, mainCol.R, mainCol.G, mainCol.B));
        dc.DrawEllipse(glow, null, pos, drop.Radius * 1.6, drop.Radius * 1.6);

        // 아이콘 뱃지
        string iconStr = drop.Type switch
        {
            FieldItemType.Meat => "🍖",
            FieldItemType.Magnet => "🧲",
            FieldItemType.Bomb => "💣",
            FieldItemType.TimeFreeze => "⏱️",
            _ => "✨"
        };

        var text = new FormattedText(iconStr, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 20, Brushes.White, 1.0);
        dc.DrawText(text, new Point(pos.X - text.Width / 2.0, pos.Y - text.Height / 2.0));
    }
    #endregion

    #region 엔티티 드로잉 (적, 플레이어, 투사체, 보석)
    private void DrawExpGem(DrawingContext dc, ExpGemEntity gem)
    {
        Color c = gem.Type switch
        {
            ExpGemType.SmallBit => Color.FromRgb(40, 200, 255),
            ExpGemType.MediumBit => Color.FromRgb(40, 255, 120),
            ExpGemType.LargeBit => Color.FromRgb(255, 215, 0),
            ExpGemType.DigiEggBox => Color.FromRgb(255, 80, 200),
            _ => Colors.Cyan
        };

        double size = gem.Type switch
        {
            ExpGemType.SmallBit => 6.0,
            ExpGemType.MediumBit => 9.0,
            ExpGemType.LargeBit => 13.0,
            ExpGemType.DigiEggBox => 20.0,
            _ => 7.0
        };

        double pulse = 1.0 + Math.Sin(gem.SparkleTimer * 8.0) * 0.15;
        double s = size * pulse;

        var brush = new SolidColorBrush(c);
        var pen = new Pen(new SolidColorBrush(Colors.White), 1.0);

        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(gem.Position.X, gem.Position.Y - s), IsClosed = true };
        fig.Segments.Add(new LineSegment(new Point(gem.Position.X + s, gem.Position.Y), true));
        fig.Segments.Add(new LineSegment(new Point(gem.Position.X, gem.Position.Y + s), true));
        fig.Segments.Add(new LineSegment(new Point(gem.Position.X - s, gem.Position.Y), true));
        geo.Figures.Add(fig);

        dc.DrawGeometry(brush, pen, geo);
    }

    private void DrawEnemy(DrawingContext dc, EnemyEntity enemy)
    {
        // 자연스러운 발밑 접지 타원 그림자 (원형 코인 테두리 배제)
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null,
            new Point(enemy.Position.X, enemy.Position.Y + enemy.Radius * 0.8), enemy.Radius * 0.9, enemy.Radius * 0.38);

        // 보스/미니보스 전용 위압적 다크 오라
        if (enemy.IsBoss || enemy.IsMiniBoss)
        {
            Color auraCol = enemy.IsBoss ? Color.FromRgb(255, 30, 30) : Color.FromRgb(180, 40, 255);
            var auraBrush = new RadialGradientBrush(Color.FromArgb(100, auraCol.R, auraCol.G, auraCol.B), Color.FromArgb(0, auraCol.R, auraCol.G, auraCol.B));
            dc.DrawEllipse(auraBrush, null, enemy.Position, enemy.Radius * 1.6, enemy.Radius * 1.6);
        }

        var sprite = AssetManager.GetSprite(enemy.SpriteFileName);
        double drawSize = enemy.Radius * 2.8;
        Rect drawRect = new(enemy.Position.X - drawSize / 2.0, enemy.Position.Y - drawSize / 2.0, drawSize, drawSize);

        if (sprite != null)
        {
            dc.DrawImage(sprite, drawRect);

            // 피격 시 백색-적색 발광 하이라이트 플래시 (원형 테두리가 아닌 피격 순간 섬광)
            if (enemy.HitFlashTimer > 0)
            {
                var flashBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
                dc.PushClip(new RectangleGeometry(drawRect, 8, 8));
                dc.DrawRectangle(flashBrush, null, drawRect);
                dc.Pop();
            }

            // 빙결 상태 크리스탈 얼음 결정체 오버레이 (원형 테두리 대신 각진 얼음 결정)
            if (enemy.FreezeTimer > 0)
            {
                var icePen = new Pen(new SolidColorBrush(Color.FromRgb(200, 245, 255)), 1.5);
                icePen.Freeze();
                double s = enemy.Radius * 0.9;
                var iceGeo = new PathGeometry();
                var fig = new PathFigure { StartPoint = new Point(enemy.Position.X, enemy.Position.Y - s), IsClosed = true };
                fig.Segments.Add(new LineSegment(new Point(enemy.Position.X + s * 0.7, enemy.Position.Y - s * 0.2), true));
                fig.Segments.Add(new LineSegment(new Point(enemy.Position.X + s * 0.5, enemy.Position.Y + s * 0.8), true));
                fig.Segments.Add(new LineSegment(new Point(enemy.Position.X - s * 0.5, enemy.Position.Y + s * 0.8), true));
                fig.Segments.Add(new LineSegment(new Point(enemy.Position.X - s * 0.7, enemy.Position.Y - s * 0.2), true));
                iceGeo.Figures.Add(fig);

                var iceFill = new SolidColorBrush(Color.FromArgb(90, 0, 229, 255));
                dc.DrawGeometry(iceFill, icePen, iceGeo);
            }
        }
        else
        {
            // 폴백: 세련된 다이아몬드 크리스탈 가디언 노드 (원형 완전 탈피)
            var geo = new PathGeometry();
            double r = enemy.Radius;
            var fig = new PathFigure { StartPoint = new Point(enemy.Position.X, enemy.Position.Y - r * 1.2), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(enemy.Position.X + r, enemy.Position.Y), true));
            fig.Segments.Add(new LineSegment(new Point(enemy.Position.X, enemy.Position.Y + r * 1.2), true));
            fig.Segments.Add(new LineSegment(new Point(enemy.Position.X - r, enemy.Position.Y), true));
            geo.Figures.Add(fig);

            var fallbackBrush = new SolidColorBrush(enemy.IsBoss ? Color.FromRgb(220, 40, 40) : Color.FromRgb(160, 40, 200));
            dc.DrawGeometry(fallbackBrush, new Pen(Brushes.White, 1.5), geo);
        }

        // HP 바 (피격되었거나 보스인 경우 표시)
        if (enemy.CurrentHp < enemy.MaxHp || enemy.IsBoss || enemy.IsMiniBoss)
        {
            double barW = enemy.Radius * 2.4;
            double barH = enemy.IsBoss ? 8.0 : 4.5;
            double barX = enemy.Position.X - barW / 2.0;
            double barY = enemy.Position.Y - enemy.Radius - 14.0;
            double hpRatio = Math.Clamp(enemy.CurrentHp / enemy.MaxHp, 0.0, 1.0);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)), null, new Rect(barX, barY, barW, barH));
            dc.DrawRectangle(new SolidColorBrush(enemy.IsBoss ? Color.FromRgb(255, 60, 60) : Color.FromRgb(255, 180, 0)), null, new Rect(barX, barY, barW * hpRatio, barH));

            if (enemy.IsBoss || enemy.IsMiniBoss)
            {
                var nameText = new FormattedText(enemy.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 12, Brushes.White, 1.0);
                dc.DrawText(nameText, new Point(enemy.Position.X - nameText.Width / 2.0, barY - 16.0));
            }
        }
    }

    private void DrawPlayer(DrawingContext dc, PlayerEntity player)
    {
        // 1. 발밑 자연스러운 접지 그림자 (360도 원형 링을 제거하고 자연스러운 타원 그림자만 적용)
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), null,
            new Point(player.Position.X, player.Position.Y + player.Radius * 0.85), player.Radius * 1.15, player.Radius * 0.42);

        // 2. 대시 준비 완료 시 플레이어 전방을 가리키는 날렵한 전술 쉐브론 가이드 (원형 링 테두리 배제)
        double cdRatio = Math.Clamp(1.0 - (player.DashCooldownTimer / player.DashCooldownMax), 0.0, 1.0);
        if (cdRatio >= 1.0)
        {
            double facing = player.FacingDirection;
            Point arrowBase = new(player.Position.X + facing * player.Radius * 0.85, player.Position.Y + player.Radius * 0.82);
            var chevronPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 229, 255)), 2.0);
            chevronPen.Freeze();
            dc.DrawLine(chevronPen, new Point(arrowBase.X - facing * 8, arrowBase.Y - 5), arrowBase);
            dc.DrawLine(chevronPen, new Point(arrowBase.X - facing * 8, arrowBase.Y + 5), arrowBase);
        }

        double bobbing = player.IsMoving ? Math.Sin(player.WalkCycleTimer) * 4.0 : 0.0;
        var sprite = AssetManager.GetDigimonSprite(player.Profile);
        double drawSize = player.Radius * 3.2;
        Rect drawRect = new(player.Position.X - drawSize / 2.0, player.Position.Y - drawSize / 2.0 + bobbing, drawSize, drawSize);

        // 무적 시간 깜빡임
        if (player.InvincibleTimer > 0 && ((int)(player.InvincibleTimer * 20.0) % 2 == 0))
        {
            // Blink skip
        }
        else
        {
            if (sprite != null)
            {
                if (player.FacingDirection < 0)
                {
                    dc.PushTransform(new ScaleTransform(-1, 1, player.Position.X, player.Position.Y));
                }

                dc.DrawImage(sprite, drawRect);

                if (player.FacingDirection < 0)
                {
                    dc.Pop();
                }
            }
            else
            {
                // 폴백: 세련된 다이아몬드 사이버 홀로그램 (원형 제거)
                var geo = new PathGeometry();
                var fig = new PathFigure { StartPoint = new Point(player.Position.X, player.Position.Y - player.Radius * 1.2), IsClosed = true };
                fig.Segments.Add(new LineSegment(new Point(player.Position.X + player.Radius * 0.9, player.Position.Y), true));
                fig.Segments.Add(new LineSegment(new Point(player.Position.X, player.Position.Y + player.Radius * 1.2), true));
                fig.Segments.Add(new LineSegment(new Point(player.Position.X - player.Radius * 0.9, player.Position.Y), true));
                geo.Figures.Add(fig);
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(180, 255, 80, 0)), new Pen(Brushes.White, 2.0), geo);
            }
        }

        // 플레이어 머리 위 레벨 뱃지
        string badgeText = $"Lv.{player.Level} {player.Profile.Name}";
        var fmtBadge = new FormattedText(badgeText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 12, Brushes.Gold, 1.0);
        double badgeY = player.Position.Y - player.Radius - 18.0 + bobbing;
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), null,
            new Rect(player.Position.X - fmtBadge.Width / 2.0 - 4, badgeY - 2, fmtBadge.Width + 8, fmtBadge.Height + 4));
        dc.DrawText(fmtBadge, new Point(player.Position.X - fmtBadge.Width / 2.0, badgeY));
    }

    #region 고유 스킬 원소별 전용 시각 이펙트 렌더링
    private void DrawProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        string id = proj.SkillId?.ToLowerInvariant() ?? "";

        if (proj.IsOrbiting || id is "petit_fire" or "fox_fire" or "brave_shield")
        {
            DrawOrbitingOrb(dc, proj);
        }
        else if (id is "baby_flame" or "mega_flame" or "spit_fire" or "crimson_flare")
        {
            DrawFlameProjectile(dc, proj);
        }
        else if (id is "air_shot" or "sparking_air" or "spiral_twister")
        {
            DrawAirShockwaveProjectile(dc, proj);
        }
        else if (id is "surfing_fish" or "water_breath" or "bubble_blow")
        {
            DrawWaterWaveProjectile(dc, proj);
        }
        else if (id is "petit_thunder" or "hammer_spark" or "mega_blaster" or "giga_blaster" or "horn_buster" or "dynamo_shock")
        {
            DrawLightningProjectile(dc, proj);
        }
        else if (id is "pico_dart" or "darkness_wave" or "dark_matter" or "night_raid" or "bat_flutter" or "dark_whisper")
        {
            DrawDarkBatProjectile(dc, proj);
        }
        else if (id is "goburi_strike" or "slamming_attack" or "club_rush" or "woody_smash" or "leaf_cutter")
        {
            DrawEarthBluntProjectile(dc, proj);
        }
        else if (id is "ice_arrow" or "cocytus_breath" or "sorrow_blue" or "ice_dash" or "grace_cross_freezer")
        {
            DrawIceCrystalProjectile(dc, proj);
        }
        else if (id is "holy_arrow" or "heavens_knuckle" or "heaven_gate" or "starlight_explosion" or "strike_of_the_seven_stars" or "holy_rod")
        {
            DrawHolySpearProjectile(dc, proj);
        }
        else if (id is "giga_destroyer" or "hyper_cannon" or "infinity_cannon" or "ground_zero" or "trident_arm" or "harpoon_torpedo" or "machine_gun_fire")
        {
            DrawMissileProjectile(dc, proj);
        }
        else if (id is "shadow_wing" or "shadow_wing_ex" or "kaiser_nail" or "grey_sword" or "trump_sword" or "dramon_killer" or "haouken" or "metal_slash" or "bear_claw")
        {
            DrawCrescentBladeProjectile(dc, proj);
        }
        else
        {
            DrawGenericElementalProjectile(dc, proj);
        }
    }

    // 1. 화염형 스킬 (베이비 플레임, 메가 플레임, 스핏 파이어) - 유선형 초고열 플라즈마 혜성
    private void DrawFlameProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double angleDeg = proj.Velocity.LengthSquared > 0.01 ? Math.Atan2(proj.Velocity.Y, proj.Velocity.X) * 180.0 / Math.PI : 0;
        double r = proj.Radius;
        double t = proj.LifeTime * 18.0;

        dc.PushTransform(new RotateTransform(angleDeg, proj.Position.X, proj.Position.Y));

        // 1. 광역 화염 대기 후광 (Ambient Heat Distortion Bloom)
        var heatGlow = new RadialGradientBrush(Color.FromArgb(90, 255, 90, 0), Color.FromArgb(0, 255, 10, 0));
        dc.DrawEllipse(heatGlow, null, proj.Position, r * 2.8, r * 1.8);

        // 2. 다층 유선형 플라즈마 혜성 꼬리 (외곽 홍염)
        var outerTail = new PathGeometry();
        var oFig = new PathFigure { StartPoint = new Point(proj.Position.X + r * 0.9, proj.Position.Y), IsClosed = true };
        oFig.Segments.Add(new BezierSegment(
            new Point(proj.Position.X - r * 0.4, proj.Position.Y - r * 1.2),
            new Point(proj.Position.X - r * 2.2, proj.Position.Y - r * 0.6 + Math.Sin(t) * 3),
            new Point(proj.Position.X - r * 3.6, proj.Position.Y), true));
        oFig.Segments.Add(new BezierSegment(
            new Point(proj.Position.X - r * 2.2, proj.Position.Y + r * 0.6 - Math.Sin(t) * 3),
            new Point(proj.Position.X - r * 0.4, proj.Position.Y + r * 1.2),
            new Point(proj.Position.X + r * 0.9, proj.Position.Y), true));
        outerTail.Figures.Add(oFig);

        var outerFlameBrush = new LinearGradientBrush(Color.FromArgb(230, 255, 120, 10), Color.FromArgb(0, 220, 20, 0), 0.0);
        dc.DrawGeometry(outerFlameBrush, null, outerTail);

        // 3. 내부 초고열 황금빛 불길 혀 (Inner Golden Flame Tongue)
        var innerTail = new PathGeometry();
        var iFig = new PathFigure { StartPoint = new Point(proj.Position.X + r * 0.7, proj.Position.Y), IsClosed = true };
        iFig.Segments.Add(new BezierSegment(
            new Point(proj.Position.X - r * 0.3, proj.Position.Y - r * 0.7),
            new Point(proj.Position.X - r * 1.6, proj.Position.Y - r * 0.35 - Math.Cos(t) * 2),
            new Point(proj.Position.X - r * 2.6, proj.Position.Y), true));
        iFig.Segments.Add(new BezierSegment(
            new Point(proj.Position.X - r * 1.6, proj.Position.Y + r * 0.35 + Math.Cos(t) * 2),
            new Point(proj.Position.X - r * 0.3, proj.Position.Y + r * 0.7),
            new Point(proj.Position.X + r * 0.7, proj.Position.Y), true));
        innerTail.Figures.Add(iFig);

        var innerFlameBrush = new LinearGradientBrush(Color.FromArgb(255, 255, 240, 100), Color.FromArgb(0, 255, 80, 0), 0.0);
        dc.DrawGeometry(innerFlameBrush, null, innerTail);

        // 4. 전면 초신성 백색 발광 핵 (White-Hot Incandescent Flare)
        var coreGlow = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 255, 160, 20));
        dc.DrawEllipse(coreGlow, null, new Point(proj.Position.X + r * 0.25, proj.Position.Y), r * 0.8, r * 0.8);

        dc.Pop();
    }

    // 2. 공기 / 충격파형 스킬 (공기팡, 파닥몬) - 초음속 대기 굴절 구체와 압축 충격파 리본
    private void DrawAirShockwaveProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double angleDeg = proj.Velocity.LengthSquared > 0.01 ? Math.Atan2(proj.Velocity.Y, proj.Velocity.X) * 180.0 / Math.PI : 0;
        double r = proj.Radius;

        dc.PushTransform(new RotateTransform(angleDeg, proj.Position.X, proj.Position.Y));

        // 1. 대기 굴절 포톤 오라 (Soft Refraction Atmospheric Bloom)
        var auraBrush = new RadialGradientBrush(Color.FromArgb(100, 220, 245, 255), Color.FromArgb(0, 80, 180, 255));
        dc.DrawEllipse(auraBrush, null, proj.Position, r * 2.6, r * 2.0);

        // 2. 초음속 마하 콘 압축 기류 (Mach Compression Ribbons)
        for (int i = 0; i < 3; i++)
        {
            double backOffset = -i * (r * 0.85);
            double waveW = r * (1.2 + i * 0.45);
            double alpha = Math.Clamp(1.0 - i * 0.32, 0.2, 0.95);

            var ribbonGeo = new PathGeometry();
            var rFig = new PathFigure { StartPoint = new Point(proj.Position.X + backOffset, proj.Position.Y - waveW), IsClosed = false };
            rFig.Segments.Add(new BezierSegment(
                new Point(proj.Position.X + backOffset + r * 1.1, proj.Position.Y - waveW * 0.5),
                new Point(proj.Position.X + backOffset + r * 1.1, proj.Position.Y + waveW * 0.5),
                new Point(proj.Position.X + backOffset, proj.Position.Y + waveW), true));
            ribbonGeo.Figures.Add(rFig);

            var wavePen = new Pen(new LinearGradientBrush(
                Color.FromArgb(0, 220, 250, 255),
                Color.FromArgb((byte)(alpha * 230), 255, 255, 255),
                0.0), 3.0 - i * 0.7);
            wavePen.Freeze();
            dc.DrawGeometry(null, wavePen, ribbonGeo);
        }

        // 3. 고압 진공 렌즈 핵 (High-Pressure Atmospheric Sphere)
        var vacuumCore = new RadialGradientBrush(Colors.White, Color.FromArgb(30, 100, 220, 255));
        vacuumCore.GradientStops.Add(new GradientStop(Color.FromArgb(220, 210, 245, 255), 0.35));
        vacuumCore.GradientStops.Add(new GradientStop(Color.FromArgb(80, 80, 200, 255), 0.75));
        dc.DrawEllipse(vacuumCore, null, proj.Position, r * 0.95, r * 0.95);

        // 4. 전면 음속 돌파 십자 섬광 (Sonic Breaking Star Glint)
        var glintPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1.5);
        glintPen.Freeze();
        dc.DrawLine(glintPen, new Point(proj.Position.X - r * 0.7, proj.Position.Y), new Point(proj.Position.X + r * 0.7, proj.Position.Y));
        dc.DrawLine(glintPen, new Point(proj.Position.X, proj.Position.Y - r * 0.7), new Point(proj.Position.X, proj.Position.Y + r * 0.7));

        dc.Pop();
    }

    // 3. 수류 / 파도형 스킬 (물고기 행진, 고마몬) - 역동적인 푸른 수룡 파도와 빛나는 유영 수령(Water Spirits)
    private void DrawWaterWaveProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double angleDeg = proj.Velocity.LengthSquared > 0.01 ? Math.Atan2(proj.Velocity.Y, proj.Velocity.X) * 180.0 / Math.PI : 0;
        double r = proj.Radius;
        double waveTime = proj.LifeTime * 14.0;

        dc.PushTransform(new RotateTransform(angleDeg, proj.Position.X, proj.Position.Y));

        // 1. 심해 아쿠아 발광 후광 (Aquamarine Hydro Aura)
        var hydroGlow = new RadialGradientBrush(Color.FromArgb(110, 0, 220, 255), Color.FromArgb(0, 0, 50, 180));
        dc.DrawEllipse(hydroGlow, null, proj.Position, r * 2.8, r * 1.8);

        // 2. 출렁이는 해일 파도 크레스트 (Surging Tidal Torrents)
        var waveGeo = new PathGeometry();
        var wFig = new PathFigure { StartPoint = new Point(proj.Position.X + r * 1.3, proj.Position.Y), IsClosed = true };
        wFig.Segments.Add(new BezierSegment(
            new Point(proj.Position.X + r * 0.3, proj.Position.Y - r * 1.3 + Math.Sin(waveTime) * 4),
            new Point(proj.Position.X - r * 1.4, proj.Position.Y - r * 0.9),
            new Point(proj.Position.X - r * 2.8, proj.Position.Y), true));
        wFig.Segments.Add(new BezierSegment(
            new Point(proj.Position.X - r * 1.4, proj.Position.Y + r * 0.9),
            new Point(proj.Position.X + r * 0.3, proj.Position.Y + r * 1.3 - Math.Sin(waveTime) * 4),
            new Point(proj.Position.X + r * 1.3, proj.Position.Y), true));
        waveGeo.Figures.Add(wFig);

        var tidalBrush = new LinearGradientBrush(Color.FromArgb(230, 180, 245, 255), Color.FromArgb(40, 0, 70, 220), 0.0);
        dc.DrawGeometry(tidalBrush, null, waveGeo);

        // 3. 유영하는 2마리의 빛나는 청록 수령(Water Spirits) - MS그림판 만화 물고기가 아닌 순수 빛의 유선형 수령
        for (int s = 0; s < 2; s++)
        {
            double phase = waveTime * 1.5 + (s * Math.PI);
            double sy = Math.Sin(phase) * (r * 0.42);
            double sx = (s == 0 ? r * 0.4 : -r * 0.2);
            Point sHead = new(proj.Position.X + sx, proj.Position.Y + sy);

            // 유선형 빛의 정령 몸체
            var spiritGeo = new PathGeometry();
            var sf = new PathFigure { StartPoint = new Point(sHead.X + r * 0.5, sHead.Y), IsClosed = true };
            sf.Segments.Add(new BezierSegment(
                new Point(sHead.X, sHead.Y - r * 0.35),
                new Point(sHead.X - r * 0.8, sHead.Y - r * 0.25),
                new Point(sHead.X - r * 1.2, sHead.Y), true));
            sf.Segments.Add(new BezierSegment(
                new Point(sHead.X - r * 0.8, sHead.Y + r * 0.25),
                new Point(sHead.X, sHead.Y + r * 0.35),
                new Point(sHead.X + r * 0.5, sHead.Y), true));
            spiritGeo.Figures.Add(sf);

            var spiritBrush = new LinearGradientBrush(Colors.White, Color.FromArgb(80, 0, 170, 255), 0.0);
            dc.DrawGeometry(spiritBrush, null, spiritGeo);

            // 정령 핵 플레어
            var sCore = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 0, 240, 255));
            dc.DrawEllipse(sCore, null, sHead, r * 0.35, r * 0.35);
        }

        dc.Pop();
    }

    // 4. 전격 / 번개형 스킬 (꼬마 벼락, 텐토몬, 해머 스파크) - 초고전압 프랙탈 방전 아크와 펄스 플라즈마 구체
    private void DrawLightningProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double r = proj.Radius;
        double t = proj.LifeTime * 48.0;

        // 1. 고전압 플라즈마 후광 오라 (Electric Plasma Bloom)
        var elecGlow = new RadialGradientBrush(Color.FromArgb(160, 0, 230, 255), Color.FromArgb(0, 50, 0, 255));
        dc.DrawEllipse(elecGlow, null, proj.Position, r * 2.8, r * 2.8);

        // 2. 실시간 프랙탈 지터 벼락 분기 (Dynamic Jagged Lightning Bolts)
        var coronaPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 210, 255)), 5.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        coronaPen.Freeze();
        var corePen = new Pen(Brushes.White, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        corePen.Freeze();

        for (int i = 0; i < 4; i++)
        {
            double baseAngle = (Math.PI * 0.5 * i) + Math.Sin(t * 0.4 + i) * 0.4;
            double len1 = r * 0.75;
            double len2 = r * 1.5;

            Point p0 = proj.Position;
            double jx1 = Math.Sin(t + i * 2.3) * (r * 0.45);
            double jy1 = Math.Cos(t + i * 1.7) * (r * 0.45);
            Point p1 = new(p0.X + Math.Cos(baseAngle) * len1 + jx1, p0.Y + Math.Sin(baseAngle) * len1 + jy1);

            double jx2 = Math.Cos(t * 1.4 + i * 3.1) * (r * 0.55);
            double jy2 = Math.Sin(t * 1.4 + i * 2.7) * (r * 0.55);
            Point p2 = new(p0.X + Math.Cos(baseAngle) * len2 + jx2, p0.Y + Math.Sin(baseAngle) * len2 + jy2);

            // Pass 1: 외곽 청록 발광 코로나
            dc.DrawLine(coronaPen, p0, p1);
            dc.DrawLine(coronaPen, p1, p2);

            // Pass 2: 내부 순백 방전 심선
            dc.DrawLine(corePen, p0, p1);
            dc.DrawLine(corePen, p1, p2);

            // 분기 서브 스파크
            double forkAngle = baseAngle + (i % 2 == 0 ? 0.6 : -0.6);
            Point pFork = new(p1.X + Math.Cos(forkAngle) * (r * 0.6), p1.Y + Math.Sin(forkAngle) * (r * 0.6));
            dc.DrawLine(coronaPen, p1, pFork);
            dc.DrawLine(corePen, p1, pFork);
        }

        // 3. 중앙 초고열 플라즈마 코어 (White-Hot Lightning Core)
        var ballCore = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 0, 240, 255));
        dc.DrawEllipse(ballCore, null, proj.Position, r * 0.75, r * 0.75);
    }

    // 5. 암흑 / 공허형 스킬 (피코 다트, 피코데블몬) - 칠흑의 공허 크리스탈 단검과 저주 안개 소용돌이
    private void DrawDarkBatProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double spinAngle = proj.LifeTime * 580.0;
        double r = proj.Radius;

        dc.PushTransform(new RotateTransform(spinAngle, proj.Position.X, proj.Position.Y));

        // 1. 공허 저주 미아즈마 성운 (Cursed Void Nebula Bloom)
        var nebula = new RadialGradientBrush(Color.FromArgb(170, 30, 0, 65), Color.FromArgb(0, 160, 0, 230));
        nebula.GradientStops.Add(new GradientStop(Color.FromArgb(90, 120, 0, 180), 0.55));
        dc.DrawEllipse(nebula, null, proj.Position, r * 2.8, r * 2.8);

        // 2. 3방향 대칭 팬텀 다크니스 크리스탈 블레이드 (Phantom Void Needles)
        for (int i = 0; i < 3; i++)
        {
            double bladeAngle = i * 120.0;
            dc.PushTransform(new RotateTransform(bladeAngle, proj.Position.X, proj.Position.Y));

            var shardGeo = new PathGeometry();
            var sf = new PathFigure { StartPoint = new Point(proj.Position.X, proj.Position.Y - r * 1.8), IsClosed = true };
            sf.Segments.Add(new LineSegment(new Point(proj.Position.X + r * 0.45, proj.Position.Y - r * 0.5), true));
            sf.Segments.Add(new BezierSegment(
                new Point(proj.Position.X + r * 0.2, proj.Position.Y - r * 0.2),
                new Point(proj.Position.X + r * 0.1, proj.Position.Y),
                new Point(proj.Position.X, proj.Position.Y), true));
            sf.Segments.Add(new BezierSegment(
                new Point(proj.Position.X - r * 0.1, proj.Position.Y),
                new Point(proj.Position.X - r * 0.2, proj.Position.Y - r * 0.2),
                new Point(proj.Position.X - r * 0.45, proj.Position.Y - r * 0.5), true));
            shardGeo.Figures.Add(sf);

            var shardBrush = new LinearGradientBrush(Color.FromRgb(255, 40, 160), Color.FromRgb(20, 0, 35), 90.0);
            dc.DrawGeometry(shardBrush, null, shardGeo);

            // 날렵한 마젠타 엣지 하이라이트
            var edgePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 100, 220)), 1.2);
            edgePen.Freeze();
            dc.DrawGeometry(null, edgePen, shardGeo);

            dc.Pop();
        }

        // 3. 중심 심연의 눈동자 핵 (Crimson Void Singularity)
        var singCore = new RadialGradientBrush(Color.FromRgb(255, 50, 140), Color.FromArgb(0, 40, 0, 80));
        dc.DrawEllipse(singCore, null, proj.Position, r * 0.55, r * 0.55);
        dc.DrawEllipse(Brushes.White, null, proj.Position, r * 0.2, r * 0.2);

        dc.Pop();
    }

    // 6. 타격 / 대지형 스킬 (고블 스트라이크, 고블몬) - 고대 비취 룬 혜성과 회전하는 대지 파편
    private void DrawEarthBluntProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double spinAngle = proj.LifeTime * 380.0;
        double r = proj.Radius;

        dc.PushTransform(new RotateTransform(spinAngle, proj.Position.X, proj.Position.Y));

        // 1. 대지/비취 원소 후광 (Verdant Amber Earth Bloom)
        var earthGlow = new RadialGradientBrush(Color.FromArgb(120, 80, 230, 120), Color.FromArgb(0, 40, 120, 40));
        dc.DrawEllipse(earthGlow, null, proj.Position, r * 2.5, r * 2.5);

        // 2. 다면체 비취 룬 혜성 코어 (Runic Jade Geode)
        var rockGeo = new PathGeometry();
        var rf = new PathFigure { StartPoint = new Point(proj.Position.X, proj.Position.Y - r * 1.3), IsClosed = true };
        rf.Segments.Add(new LineSegment(new Point(proj.Position.X + r * 1.0, proj.Position.Y - r * 0.5), true));
        rf.Segments.Add(new LineSegment(new Point(proj.Position.X + r * 1.2, proj.Position.Y + r * 0.4), true));
        rf.Segments.Add(new LineSegment(new Point(proj.Position.X + r * 0.2, proj.Position.Y + r * 1.3), true));
        rf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 0.9, proj.Position.Y + r * 0.9), true));
        rf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 1.1, proj.Position.Y - r * 0.4), true));
        rockGeo.Figures.Add(rf);

        var rockBrush = new LinearGradientBrush(Color.FromRgb(100, 240, 140), Color.FromRgb(30, 80, 45), 45.0);
        dc.DrawGeometry(rockBrush, null, rockGeo);

        // 황금빛 룬 크랙 라인
        var crackPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 230, 100)), 1.5);
        crackPen.Freeze();
        dc.DrawLine(crackPen, new Point(proj.Position.X, proj.Position.Y - r * 1.3), proj.Position);
        dc.DrawLine(crackPen, proj.Position, new Point(proj.Position.X + r * 1.2, proj.Position.Y + r * 0.4));
        dc.DrawLine(crackPen, proj.Position, new Point(proj.Position.X - r * 0.9, proj.Position.Y + r * 0.9));

        // 3. 회전하는 3개의 비취 칼날 잎사귀 (Orbiting Razor Shards)
        for (int i = 0; i < 3; i++)
        {
            double a = i * (Math.PI * 2.0 / 3.0);
            Point pShard = new(proj.Position.X + Math.Cos(a) * (r * 1.6), proj.Position.Y + Math.Sin(a) * (r * 1.6));
            var sBrush = new RadialGradientBrush(Color.FromRgb(180, 255, 180), Color.FromArgb(0, 40, 180, 80));
            dc.DrawEllipse(sBrush, null, pShard, r * 0.4, r * 0.4);
        }

        dc.Pop();
    }

    // 7. 참격 / 날개형 스킬 (섀도우 윙, 카이저 네일, 그레이 소드) - 초고속 플라즈마 초승달 검기와 잔상 리본
    private void DrawCrescentBladeProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double angleDeg = proj.Velocity.LengthSquared > 0.01 ? Math.Atan2(proj.Velocity.Y, proj.Velocity.X) * 180.0 / Math.PI : 0;
        double r = proj.Radius;

        dc.PushTransform(new RotateTransform(angleDeg, proj.Position.X, proj.Position.Y));

        // 1. 진홍/황금 플라즈마 후광 오라 (Solar Blade Bloom)
        var auraBrush = new RadialGradientBrush(Color.FromArgb(140, 255, 80, 10), Color.FromArgb(0, 255, 0, 0));
        dc.DrawEllipse(auraBrush, null, proj.Position, r * 2.6, r * 1.6);

        // 2. 날렵한 초승달 플라즈마 참격 본체 (Incandescent Crescent Blade)
        var bladeGeo = new PathGeometry();
        var bf = new PathFigure { StartPoint = new Point(proj.Position.X + r * 1.3, proj.Position.Y), IsClosed = true };
        bf.Segments.Add(new BezierSegment(
            new Point(proj.Position.X, proj.Position.Y - r * 1.5),
            new Point(proj.Position.X - r * 1.4, proj.Position.Y - r * 1.8),
            new Point(proj.Position.X - r * 2.2, proj.Position.Y - r * 1.9), true));
        bf.Segments.Add(new BezierSegment(
            new Point(proj.Position.X - r * 1.1, proj.Position.Y - r * 0.9),
            new Point(proj.Position.X - r * 0.3, proj.Position.Y),
            new Point(proj.Position.X - r * 1.1, proj.Position.Y + r * 0.9), true));
        bf.Segments.Add(new BezierSegment(
            new Point(proj.Position.X - r * 1.4, proj.Position.Y + r * 1.8),
            new Point(proj.Position.X, proj.Position.Y + r * 1.5),
            new Point(proj.Position.X + r * 1.3, proj.Position.Y), true));
        bladeGeo.Figures.Add(bf);

        var bladeBrush = new LinearGradientBrush(Colors.White, Color.FromRgb(255, 30, 20), 0.0);
        bladeBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 230, 80), 0.35));
        dc.DrawGeometry(bladeBrush, null, bladeGeo);

        // 3. 전면 절단면 순백의 초고열 엣지 (White-Hot Cutting Edge)
        var cutEdge = new PathGeometry();
        var cf = new PathFigure { StartPoint = new Point(proj.Position.X - r * 2.2, proj.Position.Y - r * 1.9), IsClosed = false };
        cf.Segments.Add(new BezierSegment(
            new Point(proj.Position.X - r * 1.4, proj.Position.Y - r * 1.8),
            new Point(proj.Position.X, proj.Position.Y - r * 1.5),
            new Point(proj.Position.X + r * 1.3, proj.Position.Y), true));
        cf.Segments.Add(new BezierSegment(
            new Point(proj.Position.X, proj.Position.Y + r * 1.5),
            new Point(proj.Position.X - r * 1.4, proj.Position.Y + r * 1.8),
            new Point(proj.Position.X - r * 2.2, proj.Position.Y + r * 1.9), true));
        cutEdge.Figures.Add(cf);

        var cutPen = new Pen(Brushes.White, 2.2);
        cutPen.Freeze();
        dc.DrawGeometry(null, cutPen, cutEdge);

        // 4. 전면 정점 십자 섬광 (Blade Tip Cross Glint)
        var tipGlint = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 255, 200, 50));
        dc.DrawEllipse(tipGlint, null, new Point(proj.Position.X + r * 1.3, proj.Position.Y), r * 0.7, r * 0.7);

        dc.Pop();
    }

    // 8. 빙결 / 크리스탈형 스킬 (아이스 애로우, 소로우 블루, 코큐토스 브레스) - 다면체 프리즘 얼음 창과 빙결 오로라
    private void DrawIceCrystalProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double angleDeg = proj.Velocity.LengthSquared > 0.01 ? Math.Atan2(proj.Velocity.Y, proj.Velocity.X) * 180.0 / Math.PI : 0;
        double r = proj.Radius;

        dc.PushTransform(new RotateTransform(angleDeg, proj.Position.X, proj.Position.Y));

        // 1. 극지방 빙결 오로라 후광 (Glacial Aurora Bloom)
        var aurora = new RadialGradientBrush(Color.FromArgb(120, 100, 230, 255), Color.FromArgb(0, 0, 80, 220));
        dc.DrawEllipse(aurora, null, proj.Position, r * 2.8, r * 1.7);

        // 2. 다면 프리즘 얼음 창 본체 (Upper Crystal Facet)
        var topFacet = new PathGeometry();
        var tf = new PathFigure { StartPoint = new Point(proj.Position.X + r * 2.0, proj.Position.Y), IsClosed = true };
        tf.Segments.Add(new LineSegment(new Point(proj.Position.X, proj.Position.Y - r * 0.65), true));
        tf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 2.0, proj.Position.Y), true));
        topFacet.Figures.Add(tf);
        var topBrush = new LinearGradientBrush(Colors.White, Color.FromRgb(160, 240, 255), 0.0);
        dc.DrawGeometry(topBrush, null, topFacet);

        // 하부 프리즘 패싯 (Lower Crystal Facet - 음영)
        var btmFacet = new PathGeometry();
        var bf = new PathFigure { StartPoint = new Point(proj.Position.X + r * 2.0, proj.Position.Y), IsClosed = true };
        bf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 2.0, proj.Position.Y), true));
        bf.Segments.Add(new LineSegment(new Point(proj.Position.X, proj.Position.Y + r * 0.65), true));
        btmFacet.Figures.Add(bf);
        var btmBrush = new LinearGradientBrush(Color.FromRgb(80, 190, 255), Color.FromRgb(0, 90, 220), 0.0);
        dc.DrawGeometry(btmBrush, null, btmFacet);

        // 3. 중심 크리스탈 융기선 (Ridge Glint Line)
        var ridgePen = new Pen(Brushes.White, 1.8);
        ridgePen.Freeze();
        dc.DrawLine(ridgePen, new Point(proj.Position.X - r * 2.0, proj.Position.Y), new Point(proj.Position.X + r * 2.0, proj.Position.Y));

        // 4. 첨두 4각 다이아몬드 섬광 (Tip Diamond Glint)
        var glint = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 100, 220, 255));
        dc.DrawEllipse(glint, null, new Point(proj.Position.X + r * 2.0, proj.Position.Y), r * 0.65, r * 0.65);

        dc.Pop();
    }

    // 9. 신성 / 빛형 스킬 (홀리 애로우, 헤븐즈 너클, 세븐스타즈) - 눈부신 태양광의 신성 광선창과 8각 성흔 스타
    private void DrawHolySpearProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double angleDeg = proj.Velocity.LengthSquared > 0.01 ? Math.Atan2(proj.Velocity.Y, proj.Velocity.X) * 180.0 / Math.PI : 0;
        double r = proj.Radius;

        dc.PushTransform(new RotateTransform(angleDeg, proj.Position.X, proj.Position.Y));

        // 1. 천상의 성스러운 후광 (Sanctum Solar Bloom)
        var holyGlow = new RadialGradientBrush(Color.FromArgb(140, 255, 240, 120), Color.FromArgb(0, 255, 180, 0));
        dc.DrawEllipse(holyGlow, null, proj.Position, r * 3.2, r * 1.8);

        // 2. 유선형 신성 광선창 (Solar Light Lance)
        var spearGeo = new PathGeometry();
        var sf = new PathFigure { StartPoint = new Point(proj.Position.X + r * 2.2, proj.Position.Y), IsClosed = true };
        sf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 0.2, proj.Position.Y - r * 0.6), true));
        sf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 2.4, proj.Position.Y), true));
        sf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 0.2, proj.Position.Y + r * 0.6), true));
        spearGeo.Figures.Add(sf);

        var spearBrush = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 215, 0), 0.0);
        spearBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 245, 160), 0.35));
        dc.DrawGeometry(spearBrush, null, spearGeo);

        // 3. 성스러운 황금 빛의 크로스가드 링 (Sanctum Halo Rings)
        var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 240, 120)), 2.0);
        ringPen.Freeze();
        dc.DrawEllipse(null, ringPen, new Point(proj.Position.X - r * 0.2, proj.Position.Y), r * 0.3, r * 1.1);

        // 4. 첨두 8각 성흔 스타 플레어 (Holy 8-Point Lens Flare)
        Point tip = new(proj.Position.X + r * 2.2, proj.Position.Y);
        var starGlow = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 255, 220, 50));
        dc.DrawEllipse(starGlow, null, tip, r * 0.9, r * 0.9);

        var rayPen = new Pen(Brushes.White, 1.8);
        rayPen.Freeze();
        dc.DrawLine(rayPen, new Point(tip.X - r * 1.1, tip.Y), new Point(tip.X + r * 1.1, tip.Y));
        dc.DrawLine(rayPen, new Point(tip.X, tip.Y - r * 1.1), new Point(tip.X, tip.Y + r * 1.1));

        var subRayPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 245, 180)), 1.2);
        subRayPen.Freeze();
        double d = r * 0.7;
        dc.DrawLine(subRayPen, new Point(tip.X - d, tip.Y - d), new Point(tip.X + d, tip.Y + d));
        dc.DrawLine(subRayPen, new Point(tip.X - d, tip.Y + d), new Point(tip.X + d, tip.Y - d));

        dc.Pop();
    }

    // 10. 기계 / 미사일형 스킬 (기가 디스트로이어, 하이퍼 캐논) - 유선형 티타늄 크루즈 미사일과 플라즈마 부스터
    private void DrawMissileProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double angleDeg = proj.Velocity.LengthSquared > 0.01 ? Math.Atan2(proj.Velocity.Y, proj.Velocity.X) * 180.0 / Math.PI : 0;
        double r = proj.Radius;

        dc.PushTransform(new RotateTransform(angleDeg, proj.Position.X, proj.Position.Y));

        // 1. 초고속 로켓 추진 제트 플레어 (Supersonic Rocket Thruster Cone)
        var thrustGeo = new PathGeometry();
        var tf = new PathFigure { StartPoint = new Point(proj.Position.X - r * 1.2, proj.Position.Y - r * 0.4), IsClosed = true };
        tf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 3.4, proj.Position.Y), true));
        tf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 1.2, proj.Position.Y + r * 0.4), true));
        thrustGeo.Figures.Add(tf);

        var thrustBrush = new LinearGradientBrush(Color.FromRgb(255, 245, 120), Color.FromArgb(0, 255, 40, 0), 0.0);
        dc.DrawGeometry(thrustBrush, null, thrustGeo);

        // 추진구 백색 초고열 다이아몬드 쇼크
        var shockCore = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 0, 220, 255));
        dc.DrawEllipse(shockCore, null, new Point(proj.Position.X - r * 1.3, proj.Position.Y), r * 0.45, r * 0.45);

        // 2. 유선형 티타늄 미사일 동체 (Aerodynamic Sci-Fi Hull)
        var hullGeo = new PathGeometry();
        var hf = new PathFigure { StartPoint = new Point(proj.Position.X + r * 1.6, proj.Position.Y), IsClosed = true };
        hf.Segments.Add(new BezierSegment(
            new Point(proj.Position.X + r * 0.9, proj.Position.Y - r * 0.5),
            new Point(proj.Position.X - r * 0.8, proj.Position.Y - r * 0.5),
            new Point(proj.Position.X - r * 1.2, proj.Position.Y - r * 0.4), true));
        hf.Segments.Add(new LineSegment(new Point(proj.Position.X - r * 1.2, proj.Position.Y + r * 0.4), true));
        hf.Segments.Add(new BezierSegment(
            new Point(proj.Position.X - r * 0.8, proj.Position.Y + r * 0.5),
            new Point(proj.Position.X + r * 0.9, proj.Position.Y + r * 0.5),
            new Point(proj.Position.X + r * 1.6, proj.Position.Y), true));
        hullGeo.Figures.Add(hf);

        var hullBrush = new LinearGradientBrush(Color.FromRgb(230, 235, 245), Color.FromRgb(80, 95, 115), 90.0);
        dc.DrawGeometry(hullBrush, null, hullGeo);

        // 3. 전술 센서 네온 라인 (Cyan Energy Tracer Slit)
        var tracerPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 240, 255)), 1.5);
        tracerPen.Freeze();
        dc.DrawLine(tracerPen, new Point(proj.Position.X - r * 0.7, proj.Position.Y), new Point(proj.Position.X + r * 0.8, proj.Position.Y));

        // 4. 후방 비행 안정 날개 (Stabilator Fins)
        var finPen = new Pen(new SolidColorBrush(Color.FromRgb(50, 60, 75)), 1.8);
        finPen.Freeze();
        dc.DrawLine(finPen, new Point(proj.Position.X - r * 0.7, proj.Position.Y - r * 0.45), new Point(proj.Position.X - r * 1.2, proj.Position.Y - r * 0.9));
        dc.DrawLine(finPen, new Point(proj.Position.X - r * 0.7, proj.Position.Y + r * 0.45), new Point(proj.Position.X - r * 1.2, proj.Position.Y + r * 0.9));

        dc.Pop();
    }

    // 11. 회전 도깨비불 배리어 (쁘띠 파이어, 파피몬) - 나선형 빛의 리본과 펄스 영혼 화염
    private void DrawOrbitingOrb(DrawingContext dc, ProjectileEntity proj)
    {
        double r = proj.Radius;

        // 1. 공전 궤적을 따라 흐르는 푸른 성운 리본 (Orbit Sweeping Light Ribbon)
        double tailLen = 0.55;
        var ribbonGeo = new PathGeometry();
        var rf = new PathFigure { StartPoint = proj.Position, IsClosed = false };
        int segments = 6;
        for (int i = 1; i <= segments; i++)
        {
            double a = proj.OrbitAngle - (tailLen * i / segments);
            Point tp = new(proj.Position.X - Math.Sin(proj.OrbitAngle) * (r * 0.9 * i) + (Math.Sin(a) - Math.Sin(proj.OrbitAngle)) * (r * 2.0),
                           proj.Position.Y + Math.Cos(proj.OrbitAngle) * (r * 0.9 * i) - (Math.Cos(a) - Math.Cos(proj.OrbitAngle)) * (r * 2.0));
            rf.Segments.Add(new LineSegment(tp, true));
        }
        ribbonGeo.Figures.Add(rf);

        var ribbonPen = new Pen(new LinearGradientBrush(
            Color.FromArgb(200, 0, 240, 255),
            Color.FromArgb(0, 0, 60, 220), 0.0), 3.5);
        ribbonPen.Freeze();
        dc.DrawGeometry(null, ribbonPen, ribbonGeo);

        // 2. 푸른 영혼 화염 후광 (Soul Fire Bloom)
        var orbGlow = new RadialGradientBrush(Color.FromArgb(160, 0, 230, 255), Color.FromArgb(0, 0, 80, 255));
        dc.DrawEllipse(orbGlow, null, proj.Position, r * 2.0, r * 2.0);

        // 3. 내핵 초고열 백색 발광 (Incandescent Nucleus)
        var core = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 0, 220, 255));
        dc.DrawEllipse(core, null, proj.Position, r * 0.75, r * 0.75);
    }

    // 범용 원소 투사체 - 눈부신 고밀도 에너지 오브
    private void DrawGenericElementalProjectile(DrawingContext dc, ProjectileEntity proj)
    {
        double r = proj.Radius;
        var col = proj.ParticleColor;

        // 1. 외곽 원소 에너지 오라
        var glow = new RadialGradientBrush(Color.FromArgb(140, col.R, col.G, col.B), Color.FromArgb(0, col.R, col.G, col.B));
        dc.DrawEllipse(glow, null, proj.Position, r * 2.4, r * 2.4);

        // 2. 중심 고밀도 에너지 핵
        var core = new RadialGradientBrush(Colors.White, Color.FromArgb(0, col.R, col.G, col.B));
        dc.DrawEllipse(core, null, proj.Position, r * 0.8, r * 0.8);
    }

    // 12. 고유 지속 장판 (AreaEffect) 전용 시각화 - 밋밋한 단색 원형 탈피 및 환상적인 마법진 연출
    private void DrawAreaEffect(DrawingContext dc, AreaEffectEntity area)
    {
        string name = area.Name ?? "";
        double r = area.Radius;
        double time = area.Elapsed * 3.0;

        if (name == "BlizzardZone") // 소로우 블루 - 회전하는 얼음 마법진
        {
            // 빙결 대기 안개
            var mist = new RadialGradientBrush(Color.FromArgb(90, 80, 210, 255), Color.FromArgb(0, 0, 100, 240));
            dc.DrawEllipse(mist, null, area.Position, r, r);

            // 외곽 룬 링
            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 180, 245, 255)), 2.0);
            ringPen.Freeze();
            dc.DrawEllipse(null, ringPen, area.Position, r * 0.92, r * 0.92);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(120, 100, 220, 255)), 1.2), area.Position, r * 0.72, r * 0.72);

            // 회전하는 6각 눈꽃 마법 격자
            for (int i = 0; i < 6; i++)
            {
                double a = (Math.PI / 3.0 * i) + time * 0.15;
                Point p1 = new(area.Position.X + Math.Cos(a) * (r * 0.9), area.Position.Y + Math.Sin(a) * (r * 0.9));
                Point p2 = new(area.Position.X + Math.Cos(a + Math.PI) * (r * 0.9), area.Position.Y + Math.Sin(a + Math.PI) * (r * 0.9));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(140, 200, 245, 255)), 1.5), p1, p2);

                // 눈꽃 첨두 다이아몬드
                dc.DrawEllipse(Brushes.White, null, p1, 3.0, 3.0);
            }
        }
        else if (name == "GaiaBurst") // 가이아 포스 - 태양 플라즈마 초신성 폭발
        {
            // 플라즈마 폭발 오라
            var sunGlow = new RadialGradientBrush(Color.FromArgb(230, 255, 140, 20), Color.FromArgb(0, 255, 30, 0));
            sunGlow.GradientStops.Add(new GradientStop(Color.FromArgb(160, 255, 60, 0), 0.65));
            dc.DrawEllipse(sunGlow, null, area.Position, r, r);

            // 초고열 백색 광핵
            var coreGlow = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 255, 200, 50));
            dc.DrawEllipse(coreGlow, null, area.Position, r * 0.55, r * 0.55);

            // 맥동하는 태양 플레어 링
            double pulseR = r * (0.8 + Math.Sin(time * 3.0) * 0.08);
            var shockPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 230, 100)), 2.5);
            shockPen.Freeze();
            dc.DrawEllipse(null, shockPen, area.Position, pulseR, pulseR);
        }
        else if (name == "SlashStorm") // 참격 폭풍
        {
            for (int i = 0; i < 4; i++)
            {
                double slashAngle = i * (Math.PI * 0.25) + time * 0.6;
                var sPen = new Pen(new LinearGradientBrush(
                    Color.FromArgb(0, 255, 50, 20),
                    Color.FromArgb(240, 255, 240, 180),
                    0.0), 3.0);
                sPen.Freeze();
                Point p1 = new(area.Position.X - Math.Cos(slashAngle) * (r * 0.95), area.Position.Y - Math.Sin(slashAngle) * (r * 0.95));
                Point p2 = new(area.Position.X + Math.Cos(slashAngle) * (r * 0.95), area.Position.Y + Math.Sin(slashAngle) * (r * 0.95));
                dc.DrawLine(sPen, p1, p2);
            }
        }
        else if (name == "Lightning") // 낙뢰 방전
        {
            var coronaPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 220, 255)), 4.5);
            coronaPen.Freeze();
            var corePen = new Pen(Brushes.White, 1.8);
            corePen.Freeze();

            for (int i = 0; i < 4; i++)
            {
                double a = i * (Math.PI * 0.5) + Math.Sin(time + i) * 0.2;
                Point pEnd = new(area.Position.X + Math.Cos(a) * (r * 0.85), area.Position.Y + Math.Sin(a) * (r * 0.85));
                dc.DrawLine(coronaPen, area.Position, pEnd);
                dc.DrawLine(corePen, area.Position, pEnd);
            }
        }
        else
        {
            var brush = new RadialGradientBrush(area.VisualColor, Color.FromArgb(0, area.VisualColor.R, area.VisualColor.G, area.VisualColor.B));
            dc.DrawEllipse(brush, null, area.Position, r, r);
        }
    }
    #endregion
    #endregion

    #region UI HUD & 홀로그램 미니맵 레이더
    private void DrawHud(DrawingContext dc, double viewW, double viewH, GameEngine engine)
    {
        var player = engine.Player;
        if (player == null) return;

        // 1. 상단 경험치 바 (전폭)
        double expBarH = 14.0;
        double expRatio = Math.Clamp((double)player.CurrentExp / player.NextExp, 0.0, 1.0);

        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 15, 20, 35)), null, new Rect(0, 0, viewW, expBarH));
        var expGradient = new LinearGradientBrush(Color.FromRgb(0, 190, 255), Color.FromRgb(40, 255, 180), 0.0);
        dc.DrawRectangle(expGradient, null, new Rect(0, 0, viewW * expRatio, expBarH));

        var expText = new FormattedText($"LV {player.Level}  ({player.CurrentExp} / {player.NextExp})",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 11, Brushes.White, 1.0);
        dc.DrawText(expText, new Point(viewW / 2.0 - expText.Width / 2.0, 1));

        // 2. 좌상단 플레이어 프로필 패널
        double panelX = 20;
        double panelY = 24;
        double panelW = 280;
        double panelH = 75;

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210, 12, 18, 30)),
            new Pen(new SolidColorBrush(Color.FromArgb(100, 0, 180, 255)), 1.5),
            new Rect(panelX, panelY, panelW, panelH), 8, 8);

        // 디지몬 초상화
        var portrait = AssetManager.GetDigimonSprite(player.Profile);
        Rect portraitRect = new(panelX + 8, panelY + 8, 58, 58);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, portraitRect, 6, 6);
        if (portrait != null)
        {
            dc.DrawImage(portrait, portraitRect);
        }

        string stageLabel = player.Profile.Stage switch
        {
            DigimonStage.Rookie => "성장기",
            DigimonStage.Champion => "성숙기",
            DigimonStage.Ultimate => "완전체",
            DigimonStage.Mega => "궁극체",
            DigimonStage.Ultra => "초궁극체",
            _ => "디지몬"
        };

        var nameText = new FormattedText($"{player.Profile.Name} [{stageLabel}]",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 14, Brushes.Gold, 1.0);
        dc.DrawText(nameText, new Point(panelX + 74, panelY + 10));

        // HP 게이지 바
        double hpW = 190;
        double hpH = 12;
        double hpX = panelX + 74;
        double hpY = panelY + 34;
        double hpRatio = Math.Clamp(player.CurrentHp / player.MaxHp, 0.0, 1.0);

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(180, 30, 30, 30)), null, new Rect(hpX, hpY, hpW, hpH), 4, 4);
        var hpBrush = new LinearGradientBrush(Color.FromRgb(255, 60, 60), Color.FromRgb(255, 140, 20), 0.0);
        dc.DrawRoundedRectangle(hpBrush, null, new Rect(hpX, hpY, hpW * hpRatio, hpH), 4, 4);

        var hpNumText = new FormattedText($"{(int)player.CurrentHp} / {(int)player.MaxHp}",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 10, Brushes.White, 1.0);
        dc.DrawText(hpNumText, new Point(hpX + hpW / 2.0 - hpNumText.Width / 2.0, hpY - 1));

        // DP 및 SPD 표시
        var dpText = new FormattedText($"⚔️ DP: {(int)player.Dp}   ⚡ SPD: {(int)player.Speed}",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 11, Brushes.LightSkyBlue, 1.0);
        dc.DrawText(dpText, new Point(panelX + 74, panelY + 52));

        // 3. 우상단 시간 & 킬 카운트
        int minutes = (int)(engine.GameTime / 60.0);
        int seconds = (int)(engine.GameTime % 60.0);
        string timeStr = $"⏱️ {minutes:00}:{seconds:00}";

        var timeText = new FormattedText(timeStr, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 24, Brushes.White, 1.0);
        dc.DrawText(timeText, new Point(viewW - timeText.Width - 170, 24));

        var killText = new FormattedText($"💀 {player.KillCount}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 16, Brushes.OrangeRed, 1.0);
        dc.DrawText(killText, new Point(viewW - killText.Width - 170, 56));

        // 4. 우상단 홀로그램 미니맵 레이더
        if (GameSettings.MinimapEnabled)
        {
            DrawMinimapRadar(dc, viewW - 140, 22, 120, engine);
        }

        // 5. 상단 보스 경고 배너
        if (engine.BossWarningTimer > 0 && !string.IsNullOrEmpty(engine.BossWarningText))
        {
            double warnW = 540;
            double warnH = 42;
            double warnX = viewW / 2.0 - warnW / 2.0;
            double warnY = 24;

            double pulse = Math.Sin(engine.BossWarningTimer * 12.0) * 0.15 + 0.85;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb((byte)(pulse * 230), 220, 20, 20)),
                new Pen(Brushes.Yellow, 2.5), new Rect(warnX, warnY, warnW, warnH), 6, 6);

            var warnFormatted = new FormattedText(engine.BossWarningText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 15, Brushes.White, 1.0);
            dc.DrawText(warnFormatted, new Point(viewW / 2.0 - warnFormatted.Width / 2.0, warnY + warnH / 2.0 - warnFormatted.Height / 2.0));
        }

        // 6. 좌하단 보유 무기 및 대시(SPACE) 상태 바
        double slotX = 20;
        double slotY = viewH - 58;
        double slotSize = 46;
        double slotGap = 8;

        // 대시 슬롯
        Rect dashRect = new(slotX, slotY, slotSize * 1.5, slotSize);
        double dashRatio = Math.Clamp(1.0 - (player.DashCooldownTimer / player.DashCooldownMax), 0.0, 1.0);
        bool dashReady = dashRatio >= 1.0;

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 15, 24, 40)),
            new Pen(new SolidColorBrush(dashReady ? Color.FromRgb(0, 229, 255) : Color.FromRgb(80, 90, 100)), 2.0),
            dashRect, 6, 6);

        // 대시 프로그레스 채우기
        if (!dashReady)
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(90, 0, 180, 255)), null,
                new Rect(dashRect.X + 2, dashRect.Bottom - (slotSize - 4) * dashRatio, dashRect.Width - 4, (slotSize - 4) * dashRatio), 4, 4);
        }

        var dashText = new FormattedText(dashReady ? "⚡ DASH\n[SPACE]" : $"DASH\n{player.DashCooldownTimer:0.0}s",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 10, dashReady ? Brushes.Cyan : Brushes.Gray, 1.0)
        {
            TextAlignment = TextAlignment.Center
        };
        dc.DrawText(dashText, new Point(dashRect.X + dashRect.Width / 2.0, dashRect.Y + 8));

        // 고유 시그니처 무기 슬롯
        double wpnStartX = dashRect.Right + 12;
        var sigWpn = player.SignatureWeapon;
        if (sigWpn != null)
        {
            Rect sigRect = new(wpnStartX, slotY, slotSize, slotSize);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(230, 35, 25, 10)),
                new Pen(Brushes.Gold, 2.5), sigRect, 6, 6);

            var cardImg = AssetManager.GetCardImage(sigWpn.Definition.IconCardCode) ?? AssetManager.GetSkillSprite(sigWpn.Definition.Id);
            if (cardImg != null)
            {
                dc.DrawImage(cardImg, new Rect(sigRect.X + 2, sigRect.Y + 2, slotSize - 4, slotSize - 4));
            }

            var sigBadge = new FormattedText("고유", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMain, 9, Brushes.Gold, 1.0);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)), null, new Rect(sigRect.X + 2, sigRect.Y + 2, sigBadge.Width + 4, sigBadge.Height + 2));
            dc.DrawText(sigBadge, new Point(sigRect.X + 4, sigRect.Y + 3));

            var lvlText = new FormattedText($"Lv{sigWpn.Level}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 10, Brushes.Yellow, 1.0);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), null,
                new Rect(sigRect.Right - lvlText.Width - 2, sigRect.Bottom - lvlText.Height - 1, lvlText.Width + 2, lvlText.Height + 1));
            dc.DrawText(lvlText, new Point(sigRect.Right - lvlText.Width - 1, sigRect.Bottom - lvlText.Height));

            wpnStartX = sigRect.Right + slotGap;
        }

        // 서브 액티브 무기 슬롯들 (최대 5개)
        for (int i = 0; i < PlayerEntity.MaxActiveSkills; i++)
        {
            Rect slotRect = new(wpnStartX + i * (slotSize + slotGap), slotY, slotSize, slotSize);
            if (i < player.ActiveWeapons.Count)
            {
                var wpn = player.ActiveWeapons[i];
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(200, 20, 25, 40)),
                    new Pen(new SolidColorBrush(wpn.GetColor()), 1.5), slotRect, 6, 6);

                var cardImg = AssetManager.GetCardImage(wpn.Definition.IconCardCode) ?? AssetManager.GetSkillSprite(wpn.Definition.Id);
                if (cardImg != null)
                {
                    dc.DrawImage(cardImg, new Rect(slotRect.X + 2, slotRect.Y + 2, slotSize - 4, slotSize - 4));
                }

                var lvlText = new FormattedText($"Lv{wpn.Level}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fontMono, 10, Brushes.Yellow, 1.0);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), null,
                    new Rect(slotRect.Right - lvlText.Width - 2, slotRect.Bottom - lvlText.Height - 1, lvlText.Width + 2, lvlText.Height + 1));
                dc.DrawText(lvlText, new Point(slotRect.Right - lvlText.Width - 1, slotRect.Bottom - lvlText.Height));
            }
            else
            {
                // 빈 슬롯
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(80, 15, 20, 30)),
                    new Pen(new SolidColorBrush(Color.FromArgb(60, 100, 120, 140)), 1.0), slotRect, 6, 6);
            }
        }
    }

    private void DrawMinimapRadar(DrawingContext dc, double x, double y, double size, GameEngine engine)
    {
        double radius = size / 2.0;
        Point center = new(x + radius, y + radius);

        // 레이더 배경
        var radarBg = new RadialGradientBrush(Color.FromArgb(220, 10, 20, 35), Color.FromArgb(240, 5, 12, 22));
        dc.DrawEllipse(radarBg, new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 229, 255)), 1.5), center, radius, radius);

        // 십자선
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 229, 255)), 1.0);
        dc.DrawLine(linePen, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
        dc.DrawLine(linePen, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));

        if (engine.Player == null) return;

        double radarRange = 1200.0; // 레이더 감지 반경
        double scale = (radius - 6.0) / radarRange;

        // 적 표시
        var enemyBrush = new SolidColorBrush(Color.FromRgb(255, 60, 60));
        var bossBrush = new SolidColorBrush(Color.FromRgb(255, 215, 0));

        foreach (var e in engine.Enemies)
        {
            if (!e.IsAlive) continue;
            double dx = e.Position.X - engine.Player.Position.X;
            double dy = e.Position.Y - engine.Player.Position.Y;
            if (dx * dx + dy * dy <= radarRange * radarRange)
            {
                Point dot = new(center.X + dx * scale, center.Y + dy * scale);
                if (e.IsBoss || e.IsMiniBoss)
                {
                    dc.DrawEllipse(bossBrush, new Pen(Brushes.White, 1.0), dot, 4.0, 4.0);
                }
                else
                {
                    dc.DrawEllipse(enemyBrush, null, dot, 2.0, 2.0);
                }
            }
        }

        // 필드 드롭 아이템 표시
        var meatBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        foreach (var d in engine.FieldDrops)
        {
            double dx = d.Position.X - engine.Player.Position.X;
            double dy = d.Position.Y - engine.Player.Position.Y;
            if (dx * dx + dy * dy <= radarRange * radarRange)
            {
                dc.DrawEllipse(meatBrush, null, new Point(center.X + dx * scale, center.Y + dy * scale), 3.0, 3.0);
            }
        }

        // 중앙 플레이어 (청록색)
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0, 229, 255)), new Pen(Brushes.White, 1.5), center, 3.5, 3.5);
    }

    private void DrawBossAlarmVignette(DrawingContext dc, double w, double h, double timer)
    {
        double pulse = (Math.Sin(timer * 14.0) + 1.0) * 0.5;
        byte alpha = (byte)(pulse * 100);

        var borderBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromArgb(alpha, 255, 0, 0), 0.0),
                new(Color.FromArgb(0, 255, 0, 0), 0.15),
                new(Color.FromArgb(0, 255, 0, 0), 0.85),
                new(Color.FromArgb(alpha, 255, 0, 0), 1.0)
            }
        };

        dc.DrawRectangle(borderBrush, null, new Rect(0, 0, w, h));
    }
    #endregion
}
