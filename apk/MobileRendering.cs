using Android.Graphics;

namespace HyperLane.Android;

internal sealed partial class GameSurface
{
    private double[] judgementDepths = [];
    private readonly Dictionary<int, double> beamRelease = new();

    private void DrawNativePlaying(Canvas canvas)
    {
        if (session is null || player is null) return;
        if (!audioStarted && PlaybackSeconds >= 0)
        {
            player.Start(); videoPlayer?.Start(); audioStarted = true;
            if (!practicePlaying) playbackBase = 0;
            playbackClock.Restart();
        }
        double seconds = ChartPlaybackSeconds;
        AdvanceMobileReplay(seconds);
        session.AutoPlay = !mobileReplaying && settings.AutoPlay;
        if (!PracticePreroll) session.Advance(seconds, held);
        if(danPlaying)danEligible&=!settings.AutoPlay;
        UpdatePracticeVolume(seconds);
        MobileDifficulty diff = session.Difficulty;
        MobileChart chart = session.Song.Chart;
        if (practicePlaying) ScreenButton(canvas, "↻ リスタート", new RectF(10, 10, 195, 110), 24);
        double beat = MobileChart.BeatAtSeconds(diff, chart.Bpm, seconds - chart.OffsetFor(diff));
        var activeFever = diff.FeverSections.FirstOrDefault(section => beat >= section.Start && beat <= section.End);
        double feverBeat = beat - activeFever.Start;
        if (gpuFrame is not null) gpuFrame.FeverPulse = activeFever.End > activeFever.Start
            ? Math.Pow(1 - (feverBeat - Math.Floor(feverBeat)), 2) * settings.FeverAuraBrightness / 100.0 : 0;
        MobileProjection projection = MobileProjection.At(diff, beat);
        if (gpuFrame is not null)
        {
            gpuFrame.Background = background??menuBackground;
            gpuFrame.Video = videoPlayer is not null;
            gpuFrame.BackgroundBrightness = settings.BackgroundBrightness / 100f;
            if(background==null&&videoPlayer==null){double phase=(Environment.TickCount64%5000)/1000.0;gpuFrame.BackgroundBrightness*=(float)(.25+(phase<2?.075*(1-Math.Cos(Math.PI*phase)):0));DrawMenuParticles(canvas);}
            gpuFrame.Seconds = seconds;
            gpuFrame.Filter = FilterAt(diff.FilterChanges, beat);
        }
        if (gpuFrame is null && background is not null && videoPlayer is null)
        {
            paint.Color = Color.White;
            canvas.DrawBitmap(background, null, new RectF(0, 0, 1920, 1080), paint);
        }
        if (gpuFrame is null) Fill(canvas, new RectF(0, 0, 1920, 1080), Color.Argb((int)Math.Round(255 * (1 - settings.BackgroundBrightness / 100.0)), 0, 0, 0));
        int lanes = diff.Lanes;
        double laneWidth = 2.3 / lanes;
        double laneAlpha = Visibility(diff.LaneVisibilityChanges, beat) * (1 - settings.LaneTransparency / 100.0);
        if (judgementDepths.Length != lanes) judgementDepths = new double[lanes];
        HyperLane.JudgementLineMotion.Evaluate(diff.JudgementLineChanges, beat, judgementDepths);
        for (int i = 0; i < lanes; i++) judgementDepths[i] = projection.DepthAtScreenFraction(judgementDepths[i] / 100);
        for (int lane = 1; lane <= lanes; lane++)
        {
            double left = 1.15 - lane * laneWidth;
            WorldQuad(canvas, projection, left, left + laneWidth, -12, 52, .004,
                lane % 2 == 1 ? Color.Rgb(8, 20, 37) : Color.Rgb(11, 25, 44), laneAlpha);
        }
        for (int edge = 0; edge <= lanes; edge++)
        {
            double x = -1.15 + edge * laneWidth;
            bool outside = edge == 0 || edge == lanes;
            WorldQuad(canvas, projection, x - (outside ? .012 : .005), x + (outside ? .012 : .005), -12, 52, .014,
                outside ? Color.Rgb(85, 170, 215) : Color.Rgb(37, 77, 110), laneAlpha);
        }
        double rootScroll = MobileChart.ScrollPosition(diff, chart.Bpm, beat);
        double speed = settings.NoteSpeed * 10;
        // Positions are integrated once at load time, never quantized to render frames.
        foreach (double position in diff.MeasureScrollPositions)
        {
            double z = (position - rootScroll) * speed;
            if (z < -12) continue;
            if (z > 45) break;
            WorldQuad(canvas, projection, -1.15, 1.15, z - .012, z + .012, .022, Color.Rgb(90, 170, 230), .392 * laneAlpha);
        }
        for (int lane = 1; lane <= lanes; lane++)
        {
            double left = 1.15 - lane * laneWidth, depth = judgementDepths[lane - 1];
            if (held.Contains(lane)) beamRelease[lane] = seconds;
            double glow = held.Contains(lane) ? 1 : beamRelease.TryGetValue(lane, out double release) ?
                1 - Smooth(Math.Clamp((seconds - release) / .24, 0, 1)) : 0;
            if (glow > 0 && settings.KeyBeamLength > 0)
            {
                double length = 26 * settings.KeyBeamLength / 50.0;
                for (int section = 11; section >= 0; section--)
                {
                    double t = (section + .5) / 12;
                    double alpha = 130 / 255.0 * (1 - settings.KeyBeamOpacity / 100.0) * glow *
                        (Math.Exp(-3.6 * t) - Math.Exp(-3.6)) / (1 - Math.Exp(-3.6));
                    WorldQuad(canvas, projection, left + .008, left + laneWidth - .008,
                        depth + length * section / 12, depth + length * (section + 1) / 12, .006, Color.Rgb(55, 225, 255), alpha);
                }
            }
            WorldQuad(canvas, projection, left, left + laneWidth, depth - .028, depth + .028, .065, Color.Cyan, laneAlpha);
        }
        double cover = settings.LaneCover > 0 ? projection.DepthAtScreenFraction(1 - settings.LaneCover / 100.0) : 52;
        foreach (int layer in new[] { 1 }.Concat(diff.Layers.Keys.OrderBy(n => n)))
        {
            MobileDifficulty timing = layer == 1 ? diff : diff.Layers[layer];
            double layerBeat = MobileChart.BeatAtSeconds(timing, chart.Bpm, seconds - chart.OffsetFor(diff));
            double scroll = MobileChart.ScrollPosition(timing, chart.Bpm, layerBeat);
            double ns = MobileChart.NsMultiplierAt(timing, layerBeat);
            foreach (MobileNote note in diff.NotesByLayer.GetValueOrDefault(layer, []))
            {
                if (note.Layer != layer || note.Done || (long)note.Lane + note.LaneSpan - 1 < 1 || note.Lane > lanes) continue;
                int first = Math.Max(1, note.Lane), last = (int)Math.Min(lanes, (long)note.Lane + note.LaneSpan - 1);
                double depth = judgementDepths[first - 1];
                bool split = false;
                for (int l = first + 1; l <= last; l++) if (Math.Abs(judgementDepths[l - 1] - depth) > .0001) split = true;
                if (!split) NoteSegment(note, note.Lane, note.LaneSpan, depth);
                else for (int l = first; l <= last; l++) NoteSegment(note, l, 1, judgementDepths[l - 1]);
            }
            void NoteSegment(MobileNote note, int lane, int span, double offset)
            {
                double z = (note.ScrollStart - scroll) * speed * ns * note.Ss + offset;
                double end = (note.ScrollEnd - scroll) * speed * ns * note.Ss + offset;
                bool atLine = note.Kind == 'L' && (note.Started || seconds >= note.StartSeconds);
                if (atLine) z = offset;
                double left = 1.15 - ((double)lane + span - 1) * laneWidth;
                double right = 1.15 - ((double)lane - 1) * laneWidth;
                if (note.Kind is 'L' or 'G')
                {
                    double near = Math.Max(-12, Math.Min(z, end)), far = Math.Min(Math.Min(45, cover), Math.Max(z, end));
                    if (atLine) { if (note.Ss < 0) far = Math.Min(far, offset); else near = Math.Max(near, offset); }
                    if (far > near)
                    {
                        if (note.Kind == 'G')
                        {
                            Color color = Color.ParseColor(note.CustomColor ?? "#10b981");
                            for (int part = 0; part < 16; part++)
                            {
                                double a = near + (far - near) * part / 16, b = near + (far - near) * (part + 1) / 16;
                                double t = Math.Abs(end - z) < 1e-12 ? 0 : Math.Clamp(((a + b) / 2 - z) / (end - z), 0, 1);
                                double opacity = note.GuideOpacity * (note.GuideOpacityMode == "fade" ? 1 - t : note.GuideOpacityMode == "rise" ? t : 1);
                                WorldQuad(canvas, projection, left + .018, right - .018, a, b, .014, color, opacity);
                            }
                        }
                        else SpriteQuad(canvas, projection, "long", left + .009, right - .009, near - .08, Math.Min(cover, far + .08), .034, true);
                    }
                    if (note.Kind == 'G') return;
                    if (end >= -12 && end <= Math.Min(45, cover) && (!note.Started || (note.Ss < 0 ? end <= offset : end >= offset)))
                        Head(note.HoldEnd ? "holdEnd" : "longEnd", end);
                    if (note.Started) return;
                }
                if (z < -12 || z > Math.Min(45, cover)) return;
                Head(note.Kind switch { 'D' => "damage", 'L' => "longStart", 'H' or 'W' => "hold", 'M' => "large", _ => note.Lane % 2 == 0 ? "normalBlue" : "normalWhite" }, z);
                void Head(string sprite, double at)
                {
                    double thickness = .22 + .009 * Math.Max(0, at);
                    SpriteQuad(canvas, projection, sprite, left + .006, right - .006, at - thickness / 2 - .018,
                        Math.Min(cover, at + thickness / 2 + .018), .052, sprite == "large", note.CustomColor);
                }
            }
        }
        if (settings.LaneCover > 0)
        {
            if (gpuFrame is not null) gpuFrame.BuildingCover = true;
            WorldQuad(canvas, projection, -1.15, 1.15, cover, 52, .085, Color.Rgb(98, 66, 161), 170 / 255.0);
            WorldQuad(canvas, projection, -1.15, 1.15, cover, Math.Min(52, cover + .085), .088, Color.Rgb(171, 135, 225), 205 / 255.0);
            if (gpuFrame is not null) gpuFrame.BuildingCover = false;
        }
        if(gpuFrame is not null) session.HitEffects.Append(gpuFrame.HitEffects,session,projection,seconds);
        double hudOpacity = Visibility(diff.UiVisibilityChanges, beat);
        int hudSave = canvas.SaveLayerAlpha(new RectF(0, 0, 1920, 1080), (int)Math.Round(255 * hudOpacity));
        DrawNativeHud(canvas, seconds);
        canvas.RestoreToCount(hudSave);
        DrawZones(canvas, lanes);
        if(danPlaying)DrawMobileDanHud(canvas);
        PostInvalidateOnAnimation();
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);
    private static MobileFilterChange? FilterAt(List<MobileFilterChange> changes, double beat)
        => MobileFilters.At(changes, beat);
    private static double Visibility(List<MobileVisibilityChange> changes, double beat)
    {
        double from = 1, target = 1, start = 0, duration = 0;
        foreach (MobileVisibilityChange change in changes)
        {
            if (change.Beat > beat) break;
            from += (target - from) * (duration <= 0 ? 1 : Math.Clamp((change.Beat - start) / duration, 0, 1));
            target = change.Opacity; start = change.Beat; duration = change.Duration;
        }
        return from + (target - from) * (duration <= 0 ? 1 : Math.Clamp((beat - start) / duration, 0, 1));
    }

    private void WorldQuad(Canvas canvas, MobileProjection projection, double left, double right, double near, double far,
        double height, Color color, double opacity = 1)
    {
        if (right <= left || far <= near || opacity <= 0) return;
        if (gpuFrame is not null)
        {
            Color tint = Color.Argb((int)Math.Round(Math.Clamp(opacity, 0, 1) * color.A), color.R, color.G, color.B);
            gpuFrame.Quad(projection, left, right, near, far, height, tint, tint);
            return;
        }
        float[] points = projection.Polygon(left, height, far, right, height, far, right, height, near, left, height, near);
        if (points.Length < 6) return;
        using var path = new global::Android.Graphics.Path();
        path.MoveTo(points[0], points[1]);
        for (int i = 2; i < points.Length; i += 2) path.LineTo(points[i], points[i + 1]);
        path.Close();
        paint.SetStyle(Paint.Style.Fill);
        paint.Color = Color.Argb((int)Math.Round(Math.Clamp(opacity, 0, 1) * color.A), color.R, color.G, color.B);
        canvas.DrawPath(path, paint);
    }

    private void SpriteQuad(Canvas canvas, MobileProjection projection, string sprite, double left, double right, double near,
        double far, double height, bool strip = false, string? color = null)
    {
        if (far <= near) return;
        if (gpuFrame is not null && gpu.Sprite(gpuFrame, projection, sprite, left, right, near, far, height, strip,
            color is null ? Color.White : Color.ParseColor(color))) return;
        if (projection.Quad(left, right, height, near, far, out float[] quad) && skin?.DrawWorldQuad(canvas, sprite, quad, strip, color) == true) return;
        Color tint = color is null ? sprite switch { "hold" or "holdEnd" => Color.Gold, "large" => Color.Rgb(111, 168, 139),
            "normalBlue" => Color.Rgb(30, 144, 255), "damage" => Color.Rgb(255, 75, 90), "long" or "longStart" or "longEnd" => Color.Rgb(186, 133, 255), _ => Color.White } : Color.ParseColor(color);
        WorldQuad(canvas, projection, left, right, near, far, height, tint);
    }

    private void Fill(Canvas canvas, RectF rectangle, Color color)
    { paint.SetStyle(Paint.Style.Fill); paint.Color = color; canvas.DrawRect(rectangle, paint); }

    private void DrawNativeHud(Canvas canvas, double seconds)
    {
        if (session is null) return;
        if (hudPanel is not null) { paint.Color = Color.White; canvas.DrawBitmap(hudPanel, null, new RectF(-34, 38, 542, 362), paint); }
        Color difficulty = DifficultyColor(session.Difficulty.Name);
        if (jacket is not null) { paint.Color = Color.White; canvas.DrawBitmap(jacket, null, new RectF(78, 119, 234, 275), paint); }
        Stroke(canvas, new RectF(78, 119, 234, 275), difficulty, 3);
        UiText(canvas, session.Song.Chart.Title, 258, 141, 40);
        UiText(canvas, session.Song.Chart.Composer, 255, 192, 16);
        UiText(canvas, "chart " + session.Song.Chart.DesignerFor(session.Difficulty), 247, 223, 16);
        UiText(canvas, $"{session.Difficulty.Name} Lv.{session.Difficulty.DisplayLevel}", 114, 423, 40);
        Line(canvas, 0, 478, 473, 478, difficulty, 4);
        UiText(canvas, session.Score.ToString("D7"), 126, 505, 45, Color.White, Typeface.Monospace);
        Line(canvas, 0, 576, 473, 576, Color.Rgb(44, 102, 128), 2);
        int count = session.Counts.Sum();
        double accuracy = count == 0 ? 100 : (session.Counts[0] + .75 * session.Counts[1] + .5 * session.Counts[2] + .25 * session.Counts[3]) * 100 / count;
        UiText(canvas, $"ACCURACY  {accuracy:0.00}%", 108, 579, 20, Color.Cyan);
        string[] labels = ["PG", "GR", "GD", "BD", "MS"];
        for (int i = 0; i < 5; i++)
        {
            UiText(canvas, labels[i], 110, 615 + i * 42, 24);
            UiText(canvas, session.Counts[i].ToString(), 192, 615 + i * 42, 24);
            UiText(canvas, $"{(count == 0 ? 0 : session.Counts[i] * 100.0 / count):0.##}%", 277, 615 + i * 42, 24);
            Line(canvas, 0, 647 + i * 42, 359, 647 + i * 42, Color.Rgb(44, 102, 128), 2);
        }
        UiText(canvas, "Max combo", 49, 823, 24); UiText(canvas, session.MaxCombo.ToString(), 285, 823, 24);
        Line(canvas, 0, 857, 359, 857, Color.Rgb(44, 102, 128), 2);
        if(!danPlaying){
        Stroke(canvas, new RectF(1780, 299, 1865, 1034), Color.Rgb(170, 220, 255), 2);
        float gaugeHeight = 723 * (float)(session.Gauge / 100);
        Fill(canvas, new RectF(1790, 1027 - gaugeHeight, 1855, 1027), Color.Cyan);
        UiText(canvas, $"{session.Gauge:0}", 1814, 1044, 16);
        float gauge80=1027-723*.8f;
        Line(canvas, 1744, gauge80, 1903, gauge80, Color.Rgb(255, 190, 54), 4);
        UiText(canvas, "80", 1700, gauge80-13, 16, Color.Gold);
        }
        if (session.Combo > 0)
        {
            UiText(canvas, session.Combo.ToString("N0"), 960, 626, 45, Color.White, center: true);
            UiText(canvas, "COMBO", 960, 682, 16, Color.Cyan, center: true);
        }
        if (seconds < session.JudgeUntilSeconds && !DrawMobileJudgement(canvas,session.LastJudgement,new RectF(860,849,1060,879))) UiText(canvas, session.LastJudgement, 960, 834, 32, center: true);
        if (settings.AutoPlay && !DrawMobileJudgement(canvas,"AUTO",new RectF(28,978,138,1008))) UiText(canvas, "AUTO", 28, 1000, 18, Color.Rgb(165,222,174));
        UiText(canvas, "Ⅱ", 1820, 24, 36);
    }

    private void UiText(Canvas canvas, string text, float x, float top, float size, Color? color = null, Typeface? face = null, bool center = false)
    {
        paint.SetStyle(Paint.Style.Fill); paint.Color = color ?? Color.White; paint.TextSize = size;
        paint.SetTypeface(face ?? Typeface.Create("sans-serif", TypefaceStyle.Normal));
        paint.TextAlign = center ? Paint.Align.Center : Paint.Align.Left;
        canvas.DrawText(text, x, top - paint.Ascent(), paint);
        paint.TextAlign = Paint.Align.Left;
    }

    private void Stroke(Canvas canvas, RectF rectangle, Color color, float width)
    { paint.SetStyle(Paint.Style.Stroke); paint.Color = color; paint.StrokeWidth = width; canvas.DrawRect(rectangle, paint); paint.SetStyle(Paint.Style.Fill); }
    private void Line(Canvas canvas, float x0, float y0, float x1, float y1, Color color, float width)
    { paint.Color = color; paint.StrokeWidth = width; canvas.DrawLine(x0, y0, x1, y1, paint); }
    private static Color DifficultyColor(string name) => name.ToLowerInvariant() switch
    { "easy" => Color.Rgb(119, 220, 80), "normal" => Color.Rgb(79, 181, 239), "hard" => Color.Rgb(255, 185, 66),
        "expert" => Color.Rgb(255, 80, 92), "master" => Color.Rgb(151, 77, 248), "special" or "custom" or "" => Color.Rgb(245, 115, 220), _ => Color.Rgb(178, 211, 229) };
}
