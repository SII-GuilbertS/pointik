using Pointik.Core.Effects;
using Pointik.Core.Geometry;
using Pointik.Core.Input;
using Pointik.Core.Keys;
using Pointik.Core.Platform;
using Pointik.Core.Settings;
using Pointik.Core.Zoom;

namespace Pointik.Core.Tests;

public class LayoutZoomAndHotkeyTests
{
    private static readonly ScreenRect Area = new(0, 0, 1920, 1040);
    private static readonly ScreenSize[] Sizes = [new(100, 40), new(200, 40)];

    [Fact]
    public void Bottom_right_stack_up_puts_newest_bubble_at_the_bottom_right()
    {
        var pos = new KeyboardPosition { Corner = ScreenCorner.BottomRight, OffsetX = 40, OffsetY = 60 };
        var r = BubbleStackLayout.Compute(Area, pos, Sizes, 8);

        var newest = r.Bubbles[1];
        Assert.Equal(1920 - 40, newest.Right);
        Assert.Equal(1040 - 60, newest.Bottom);
        Assert.True(r.Bubbles[0].Bottom <= newest.Top); // la plus ancienne au-dessus
        Assert.Equal(newest.Right, r.Bubbles[0].Right); // alignées à droite
    }

    [Fact]
    public void Stack_down_puts_newest_bubble_on_top()
    {
        var pos = new KeyboardPosition { Corner = ScreenCorner.TopLeft, OffsetX = 10, OffsetY = 10, Alignment = BubbleAlignment.Left, Stack = StackDirection.Down };
        var r = BubbleStackLayout.Compute(Area, pos, Sizes, 8);

        Assert.Equal(10, r.Bubbles[1].Top);
        Assert.Equal(10, r.Bubbles[1].Left);
        Assert.True(r.Bubbles[0].Top >= r.Bubbles[1].Bottom);
    }

    [Fact]
    public void Center_alignment_centers_bubbles_in_the_column()
    {
        var pos = new KeyboardPosition { Corner = ScreenCorner.BottomLeft, OffsetX = 0, OffsetY = 0, Alignment = BubbleAlignment.Center };
        var r = BubbleStackLayout.Compute(Area, pos, Sizes, 0);
        Assert.Equal(r.Bubbles[1].Center.X, r.Bubbles[0].Center.X, 3);
    }

    [Fact]
    public void Custom_position_is_relative_and_survives_resolution_changes()
    {
        var preview = new ScreenRect(800, 700, 300, 120);
        var (fx, fy) = BubbleStackLayout.ToCustomFraction(Area, preview, BubbleAlignment.Center, StackDirection.Up);
        var pos = new KeyboardPosition { UseCustomPosition = true, CustomX = fx, CustomY = fy, Alignment = BubbleAlignment.Center };

        // Même fraction sur un écran 4K : la colonne garde la même place relative.
        var big = new ScreenRect(0, 0, 3840, 2080);
        var r = BubbleStackLayout.Compute(big, pos, [new ScreenSize(300, 120)], 0);
        Assert.Equal(preview.Center.X / Area.Width, r.Column.Center.X / big.Width, 3);
        Assert.Equal(preview.Bottom / Area.Height, r.Column.Bottom / big.Height, 3);
    }

    [Fact]
    public void Column_stays_inside_the_screen()
    {
        var pos = new KeyboardPosition { Corner = ScreenCorner.BottomRight, OffsetX = -500, OffsetY = -500 };
        var r = BubbleStackLayout.Compute(Area, pos, Sizes, 8);
        Assert.True(r.Column.Right <= Area.Right && r.Column.Bottom <= Area.Bottom);
    }

    [Fact]
    public void Centered_zoom_keeps_cursor_in_the_middle_and_view_inside_screen()
    {
        var screen = new ScreenRect(0, 0, 2560, 1440);
        Assert.Equal(new ScreenPoint(360, 240), ZoomMath.Centered(new ScreenPoint(1000, 600), screen, 2));
        Assert.Equal(new ScreenPoint(0, 0), ZoomMath.Centered(new ScreenPoint(10, 10), screen, 2));
        Assert.Equal(new ScreenPoint(1280, 720), ZoomMath.Centered(new ScreenPoint(2550, 1430), screen, 2));
    }

    [Fact]
    public void Zoom_on_a_secondary_screen_with_negative_coordinates_stays_on_that_screen()
    {
        var left = new ScreenRect(-1920, 0, 1920, 1080);
        var o = ZoomMath.Centered(new ScreenPoint(-1900, 500), left, 4);
        Assert.Equal(-1920, o.X);
        Assert.True(left.Contains(o));
    }

    [Fact]
    public void Edge_push_moves_only_near_the_border()
    {
        var screen = new ScreenRect(0, 0, 2000, 1000);
        var origin = new ScreenPoint(500, 250); // vue de 1000×500 à ×2
        Assert.Equal(origin, ZoomMath.EdgePush(origin, new ScreenPoint(1000, 500), screen, 2, 0.15));
        var moved = ZoomMath.EdgePush(origin, new ScreenPoint(1480, 500), screen, 2, 0.15);
        Assert.True(moved.X > origin.X);
        Assert.Equal(origin.Y, moved.Y);
    }

    [Fact]
    public void Zoom_controller_animates_in_and_always_returns_to_1x()
    {
        var service = new FakeZoomService();
        var settings = new ZoomSettings { Level = 3, TransitionMs = 200 };
        var zoom = new ZoomController(service, () => settings);
        var screen = new ScreenInfo("1", "Écran 1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), 1, true);
        var cursor = new ScreenPoint(960, 540);

        Assert.True(zoom.Start(cursor, screen, TimeSpan.Zero));
        zoom.Update(cursor, TimeSpan.FromMilliseconds(100));
        Assert.InRange(zoom.Scale, 1.01, 2.99);
        zoom.Update(cursor, TimeSpan.FromMilliseconds(250));
        Assert.Equal(ZoomPhase.Active, zoom.Phase);
        Assert.Equal(3, service.Scale, 3);

        zoom.Stop(TimeSpan.FromMilliseconds(300));
        zoom.Update(cursor, TimeSpan.FromMilliseconds(600));
        Assert.Equal(ZoomPhase.Off, zoom.Phase);
        Assert.Equal(1, service.Scale);
        Assert.True(service.ResetCalls > 0);
    }

    [Fact]
    public void Zoom_failure_resets_to_1x_and_disables_the_feature()
    {
        var service = new FakeZoomService { FailTransform = true };
        var zoom = new ZoomController(service, () => new ZoomSettings());
        string? message = null;
        zoom.Failed += m => message = m;
        var screen = new ScreenInfo("1", "", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), 1, true);

        Assert.False(zoom.Start(new ScreenPoint(10, 10), screen, TimeSpan.Zero));
        Assert.True(zoom.Disabled);
        Assert.Equal(ZoomPhase.Off, zoom.Phase);
        Assert.True(service.ResetCalls > 0);
        Assert.NotNull(message);
    }

    [Fact]
    public void Wheel_adjusts_the_level_within_bounds()
    {
        var settings = new ZoomSettings { Level = 7.9, WheelStep = 0.25 };
        var zoom = new ZoomController(new FakeZoomService(), () => settings);
        zoom.AdjustLevel(120, TimeSpan.Zero);
        Assert.Equal(ZoomSettings.MaxLevel, settings.Level);
        for (var i = 0; i < 100; i++) zoom.AdjustLevel(-120, TimeSpan.Zero);
        Assert.Equal(ZoomSettings.MinLevel, settings.Level);
    }

    [Fact]
    public void Hotkey_conflicts_are_detected()
    {
        var h = new HotkeySettings();
        Assert.Empty(HotkeyConflicts.Find(h));

        h.Spot = h.Zoom;
        var conflicts = HotkeyConflicts.Find(h);
        Assert.Contains(HotkeyAction.ToggleSpot, conflicts.Keys);
        Assert.Contains(HotkeyAction.ToggleZoom, conflicts.Keys);

        Assert.NotNull(HotkeyConflicts.Validate(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.Up)));
        Assert.NotNull(HotkeyConflicts.Validate(new Hotkey(KeyModifiers.Shift, KeyId.A)));
        Assert.Null(HotkeyConflicts.Validate(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.Z)));
    }

    [Fact]
    public void Ripples_fade_out_and_press_effect_shrinks_the_ring()
    {
        var fx = new ClickEffectsEngine();
        var d = TimeSpan.FromMilliseconds(500);
        fx.AddRipple(new ScreenPoint(0, 0), MouseButton.Left, TimeSpan.Zero);

        var mid = Assert.Single(fx.GetRipples(TimeSpan.FromMilliseconds(250), d, 5, 40));
        Assert.InRange(mid.Opacity, 0.01, 0.99);
        Assert.InRange(mid.Radius, 5, 40);
        Assert.Empty(fx.GetRipples(TimeSpan.FromMilliseconds(500), d, 5, 40));

        fx.SetButtonState(true, TimeSpan.Zero);
        Assert.Equal(1 - ClickEffectsEngine.PressShrink, fx.GetRingScale(TimeSpan.FromSeconds(1)), 3);
        fx.SetButtonState(false, TimeSpan.FromSeconds(1));
        Assert.Equal(1, fx.GetRingScale(TimeSpan.FromSeconds(2)), 3);
    }

    private sealed class FakeZoomService : IZoomService
    {
        public double Scale { get; private set; } = 1;
        public int ResetCalls { get; private set; }
        public bool FailTransform { get; init; }
        public bool OverlaysAreMagnified => true;

        public bool TryInitialize(out string? error)
        {
            error = null;
            return true;
        }

        public bool TrySetTransform(double scale, ScreenPoint viewOrigin, out string? error)
        {
            error = FailTransform ? "refusé" : null;
            if (!FailTransform) Scale = scale;
            return !FailTransform;
        }

        public void Reset()
        {
            ResetCalls++;
            Scale = 1;
        }

        public void Dispose() { }
    }
}

public class IdleFadeTests
{
    [Fact]
    public void Ring_stays_visible_then_fades_after_the_idle_delay()
    {
        var delay = TimeSpan.FromSeconds(3);
        var start = TimeSpan.FromSeconds(10);
        Assert.Equal(1, Effects.IdleFade.Visibility(start + TimeSpan.FromSeconds(2.9), start, delay));
        var mid = Effects.IdleFade.Visibility(start + delay + Effects.IdleFade.FadeDuration / 2, start, delay);
        Assert.InRange(mid, 0.01, 0.99);
        Assert.True(Effects.IdleFade.IsFading(start + delay + TimeSpan.FromMilliseconds(50), start, delay));
        Assert.Equal(0, Effects.IdleFade.Visibility(start + TimeSpan.FromSeconds(60), start, delay));
        Assert.False(Effects.IdleFade.IsFading(start + TimeSpan.FromSeconds(60), start, delay));
    }
}

public class HotkeyAlternativesTests
{
    [Fact]
    public void Each_action_has_valid_and_distinct_fallbacks()
    {
        var defaults = new HotkeySettings();
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var usable = HotkeyAlternatives.UsableCandidates(action, defaults).ToList();
            Assert.True(usable.Count >= 3);
            Assert.DoesNotContain(defaults.Get(action), usable);
            Assert.All(usable, h => Assert.Null(HotkeyConflicts.Validate(h)));
            Assert.Equal(usable.Count, usable.Distinct().Count());
        }
    }

    [Fact]
    public void Fallbacks_never_collide_with_other_actions()
    {
        var hotkeys = new HotkeySettings { Spot = new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.H) };
        var first = HotkeyAlternatives.UsableCandidates(HotkeyAction.ToggleRingAndSpot, hotkeys).First();
        Assert.Equal(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Shift, KeyId.M), first);
        Assert.DoesNotContain(hotkeys.Spot, HotkeyAlternatives.UsableCandidates(HotkeyAction.ToggleRingAndSpot, hotkeys));
        Assert.True(HotkeyAlternatives.IsDefault(HotkeyAction.ToggleZoom, new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.Z)));
    }
}

public class DrawingBoardTests
{
    private static readonly Graphics.ArgbColor Red = Graphics.ArgbColor.Parse("#E52B2B");

    [Fact]
    public void Numbers_count_up_and_restart_after_erase_or_clear()
    {
        var board = new Drawing.DrawingBoard();
        Assert.Equal(1, board.PlaceNumber(Red, 6, new ScreenPoint(100, 100)).Number);
        Assert.Equal(2, board.PlaceNumber(Red, 6, new ScreenPoint(200, 100)).Number);
        var third = board.PlaceNumber(Red, 6, new ScreenPoint(300, 100));
        Assert.Equal(3, third.Number);

        // Gommer le dernier : le suivant reprend son numéro.
        board.BeginErase(new ScreenPoint(300, 100));
        Assert.Equal(1, board.EndErase());
        Assert.Equal(3, board.NextNumber);

        // Annuler la gomme le fait revenir ; tout effacer repart de 1.
        Assert.True(board.Undo());
        Assert.Equal(4, board.NextNumber);
        board.Clear();
        Assert.Equal(1, board.NextNumber);
    }

    [Fact]
    public void Number_hit_covers_the_whole_disc()
    {
        var board = new Drawing.DrawingBoard();
        var n = board.PlaceNumber(Red, 6, new ScreenPoint(100, 100));
        Assert.Equal(Drawing.DrawingBoard.NumberRadiusFor(6), n.NumberRadius);
        Assert.True(Drawing.DrawingBoard.Hits(n, new ScreenPoint(100, 100), 0));
        Assert.True(Drawing.DrawingBoard.Hits(n, new ScreenPoint(100 + n.NumberRadius, 100), 0));
        Assert.False(Drawing.DrawingBoard.Hits(n, new ScreenPoint(100 + n.NumberRadius + 5, 100), 2));
    }

    [Fact]
    public void Filled_zone_is_erased_from_inside_and_line_snaps_to_45_degrees()
    {
        var board = new Drawing.DrawingBoard();
        board.Begin(Drawing.DrawingTool.FilledRectangle, Red, 6, new ScreenPoint(0, 0));
        Assert.True(board.End(new ScreenPoint(200, 100), constrain: false));
        Assert.True(Drawing.DrawingBoard.Hits(board.Shapes[0], new ScreenPoint(100, 50), 0)); // au centre

        board.Begin(Drawing.DrawingTool.Line, Red, 6, new ScreenPoint(0, 300));
        Assert.True(board.End(new ScreenPoint(100, 290), constrain: true));
        var line = board.Shapes[1];
        Assert.Equal(300, line.End.Y, 6); // ramenée à l'horizontale
        Assert.True(Drawing.DrawingBoard.Hits(line, new ScreenPoint(50, 302), 2));
        Assert.False(Drawing.DrawingBoard.Hits(line, new ScreenPoint(50, 320), 2));

        // Un simple clic avec l'outil Ligne ne laisse rien.
        board.Begin(Drawing.DrawingTool.Line, Red, 6, new ScreenPoint(500, 500));
        Assert.False(board.End(new ScreenPoint(502, 501), constrain: false));
    }

    [Fact]
    public void Rectangle_is_normalized_whatever_the_drag_direction()
    {
        var board = new Drawing.DrawingBoard();
        board.Begin(Drawing.DrawingTool.Rectangle, Red, 5, new ScreenPoint(200, 150));
        board.Extend(new ScreenPoint(120, 90), constrain: false);
        Assert.True(board.End(new ScreenPoint(100, 50), constrain: false));

        var shape = Assert.Single(board.Shapes);
        Assert.Equal(new ScreenRect(100, 50, 100, 100), shape.Bounds);
        Assert.Null(board.Current);
    }

    [Fact]
    public void Shift_makes_squares_and_circles_in_the_drag_direction()
    {
        Assert.Equal(new ScreenPoint(60, 60), Drawing.DrawingBoard.Constrain(new ScreenPoint(0, 0), new ScreenPoint(60, 20)));
        Assert.Equal(new ScreenPoint(-40, 40), Drawing.DrawingBoard.Constrain(new ScreenPoint(0, 0), new ScreenPoint(-40, 10)));

        var board = new Drawing.DrawingBoard();
        board.Begin(Drawing.DrawingTool.Ellipse, Red, 5, new ScreenPoint(10, 10));
        board.End(new ScreenPoint(110, 40), constrain: true);
        var r = board.Shapes[0].Bounds;
        Assert.Equal(r.Width, r.Height);
    }

    [Fact]
    public void Freehand_skips_points_that_are_too_close()
    {
        var board = new Drawing.DrawingBoard();
        board.Begin(Drawing.DrawingTool.Freehand, Red, 5, new ScreenPoint(0, 0));
        board.Extend(new ScreenPoint(0.5, 0), false);
        board.Extend(new ScreenPoint(5, 0), false);
        board.Extend(new ScreenPoint(5.2, 0.2), false);
        board.End(new ScreenPoint(10, 0), false);
        Assert.Equal(3, board.Shapes[0].Points.Count);
    }

    [Fact]
    public void Tiny_shapes_are_ignored_and_undo_and_clear_work()
    {
        var board = new Drawing.DrawingBoard();
        board.Begin(Drawing.DrawingTool.Rectangle, Red, 5, new ScreenPoint(10, 10));
        Assert.False(board.End(new ScreenPoint(11, 11), false));
        Assert.True(board.IsEmpty);

        for (var i = 0; i < 3; i++)
        {
            board.Begin(Drawing.DrawingTool.Ellipse, Red, 5, new ScreenPoint(0, 0));
            board.End(new ScreenPoint(50 + i, 50), false);
        }
        Assert.True(board.Undo());
        Assert.Equal(2, board.Shapes.Count);
        Assert.True(board.Clear());
        Assert.True(board.IsEmpty);
        Assert.False(board.Clear());
    }

    private static Drawing.DrawingBoard BoardWithRectangles(int count)
    {
        var board = new Drawing.DrawingBoard();
        for (var i = 0; i < count; i++)
        {
            board.Begin(Drawing.DrawingTool.Rectangle, Red, 4, new ScreenPoint(i * 100, 0));
            board.End(new ScreenPoint(i * 100 + 50, 50), false);
        }
        return board;
    }

    [Fact]
    public void Redo_restores_undone_shapes_and_a_new_shape_clears_the_redo_history()
    {
        var board = BoardWithRectangles(3);
        var last = board.Shapes[2];
        Assert.True(board.Undo());
        Assert.True(board.CanRedo);
        Assert.True(board.Redo());
        Assert.Same(last, board.Shapes[2]);
        Assert.False(board.Redo());

        board.Undo();
        board.Begin(Drawing.DrawingTool.Ellipse, Red, 4, new ScreenPoint(0, 0));
        board.End(new ScreenPoint(40, 40), false);
        Assert.False(board.CanRedo);
    }

    [Fact]
    public void Clear_can_be_undone_and_redone()
    {
        var board = BoardWithRectangles(3);
        var before = board.Shapes.ToList();
        board.Clear();
        Assert.True(board.Undo());
        Assert.Equal(before, board.Shapes);
        Assert.True(board.Redo());
        Assert.Empty(board.Shapes);
    }

    [Fact]
    public void Eraser_removes_touched_shapes_in_one_undoable_step_and_restores_their_order()
    {
        var board = BoardWithRectangles(4);
        var before = board.Shapes.ToList();

        // Clic dans le vide à l'intérieur d'un cadre : rien n'est effacé (seul le contour compte).
        board.BeginErase(new ScreenPoint(25, 25));
        Assert.Equal(0, board.EndErase());

        // Glissé rapide le long du bord haut des cadres 1 et 2 (sans point intermédiaire) : les deux sont effacés.
        board.BeginErase(new ScreenPoint(110, 0));
        board.EraseTo(new ScreenPoint(240, 0));
        Assert.Equal(2, board.EndErase());
        Assert.Equal(new[] { before[0], before[3] }, board.Shapes);

        Assert.True(board.Undo());
        Assert.Equal(before, board.Shapes);
        Assert.True(board.Redo());
        Assert.Equal(2, board.Shapes.Count);
    }

    [Fact]
    public void Hit_test_follows_the_drawn_line()
    {
        var board = new Drawing.DrawingBoard();
        board.Begin(Drawing.DrawingTool.Ellipse, Red, 4, new ScreenPoint(0, 0));
        board.End(new ScreenPoint(200, 100), false);
        board.Begin(Drawing.DrawingTool.Freehand, Red, 4, new ScreenPoint(0, 300));
        board.Extend(new ScreenPoint(100, 300), false);
        board.End(new ScreenPoint(100, 400), false);
        var (ellipse, line) = (board.Shapes[0], board.Shapes[1]);

        Assert.True(Drawing.DrawingBoard.Hits(ellipse, new ScreenPoint(200, 50), 5)); // sur le bord droit
        Assert.True(Drawing.DrawingBoard.Hits(ellipse, new ScreenPoint(100, 3), 5));  // près du haut
        Assert.False(Drawing.DrawingBoard.Hits(ellipse, new ScreenPoint(100, 50), 5)); // au centre
        Assert.True(Drawing.DrawingBoard.Hits(line, new ScreenPoint(104, 350), 5));
        Assert.False(Drawing.DrawingBoard.Hits(line, new ScreenPoint(50, 350), 5));
    }

    [Fact]
    public void Highlighter_is_wide_translucent_and_straight_with_shift()
    {
        var board = new Drawing.DrawingBoard();
        board.Begin(Drawing.DrawingTool.Highlighter, Red, 6, new ScreenPoint(0, 0));
        board.Extend(new ScreenPoint(30, 4), false);
        board.Extend(new ScreenPoint(60, 6), false);
        board.End(new ScreenPoint(200, 9), constrain: true);

        var h = Assert.Single(board.Shapes);
        Assert.Equal(Drawing.DrawingBoard.HighlighterWidth(6), h.Thickness);
        Assert.True(h.Thickness > 6);
        Assert.InRange(h.Color.A, 90, 115);
        Assert.Equal(2, h.Points.Count);
        Assert.Equal(0, h.Points[1].Y, 6);
    }

    [Fact]
    public void Text_is_typed_edited_measured_and_empty_text_is_ignored()
    {
        var board = new Drawing.DrawingBoard();
        board.BeginText(Red, 6, new ScreenPoint(10, 20));
        Assert.True(board.IsEditingText);
        board.TypeText("Bonjour\r\nà tous\t!");
        board.TypeText("\r");      // Entrée relayée comme caractère : ignorée
        board.TypeText("\u0008");  // Retour arrière relayé comme caractère : ignoré
        board.TypeText("😀");
        board.DeleteLastChar();     // l'émoji (deux unités UTF-16) part d'un coup
        board.TypeText("\n\n  ");
        board.RestyleText(Red, 12);

        Assert.True(board.EndText(a => (a.Text.Length * 10.0, 30)));
        var text = Assert.Single(board.Shapes);
        Assert.Equal("Bonjour\nà tous !", text.Text);
        Assert.Equal(Drawing.DrawingBoard.FontSizeFor(12), text.FontSize);
        Assert.Equal(new ScreenRect(10, 20, 160, 30), text.Bounds);
        Assert.True(Drawing.DrawingBoard.Hits(text, new ScreenPoint(100, 40), 0));
        Assert.False(board.IsEditingText);

        board.BeginText(Red, 6, new ScreenPoint(0, 0));
        board.TypeText("   ");
        Assert.False(board.EndText(_ => (1, 1)));
        Assert.Single(board.Shapes);
    }

    [Fact]
    public void Drawing_hotkey_has_a_conflict_free_default()
    {
        var h = new HotkeySettings();
        Assert.Equal(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.D), h.Get(HotkeyAction.ToggleDrawing));
        Assert.Empty(HotkeyConflicts.Find(h));
    }
}

public class ScrollIndicatorTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Quick_notches_in_the_same_direction_stack_chevrons_up_to_three()
    {
        var s = new Effects.ScrollIndicator();
        for (var i = 0; i < 5; i++) s.AddNotch(new ScreenPoint(10, 10), -120, Ms(i * 60));
        var f = s.GetFrame(Ms(260))!.Value;
        Assert.Equal(-1, f.Direction);
        Assert.Equal(Effects.ScrollIndicator.MaxChevrons, f.Chevrons);
    }

    [Fact]
    public void Changing_direction_restarts_and_the_indicator_fades_out()
    {
        var s = new Effects.ScrollIndicator();
        s.AddNotch(new ScreenPoint(0, 0), -120, Ms(0));
        s.AddNotch(new ScreenPoint(0, 0), -120, Ms(50));
        s.AddNotch(new ScreenPoint(0, 0), 120, Ms(100));
        var f = s.GetFrame(Ms(110))!.Value;
        Assert.Equal(1, f.Direction);
        Assert.Equal(1, f.Chevrons);
        Assert.Equal(1, f.Opacity, 3);

        Assert.InRange(s.GetFrame(Ms(100) + Effects.ScrollIndicator.Duration * 0.8)!.Value.Opacity, 0.01, 0.99);
        Assert.Null(s.GetFrame(Ms(100) + Effects.ScrollIndicator.Duration));
        Assert.False(s.IsActive(Ms(2000)));
    }
}

public class ArrowTests
{
    private static readonly Graphics.ArgbColor Red = Graphics.ArgbColor.Parse("#E52B2B");

    [Fact]
    public void Arrow_head_points_at_the_tip_and_is_symmetric()
    {
        var a = Drawing.ArrowShape.Compute(new ScreenPoint(0, 0), new ScreenPoint(100, 0), 6);
        Assert.Equal(new ScreenPoint(100, 0), a.Tip);
        Assert.True(a.WingLeft.X < 100 && a.WingRight.X < 100);
        Assert.Equal(-a.WingLeft.Y, a.WingRight.Y, 6);
        Assert.True(a.ShaftEnd.X > a.WingLeft.X && a.ShaftEnd.X < 100); // le trait s'arrête dans la pointe
    }

    [Fact]
    public void Head_never_exceeds_half_of_a_short_arrow()
    {
        var a = Drawing.ArrowShape.Compute(new ScreenPoint(0, 0), new ScreenPoint(20, 0), 12);
        Assert.True(a.WingLeft.X >= 10 - 1e-9);
    }

    [Fact]
    public void Shift_snaps_arrows_to_45_degrees_and_tiny_arrows_are_ignored()
    {
        var snapped = Drawing.DrawingBoard.SnapAngle(new ScreenPoint(0, 0), new ScreenPoint(100, 12));
        Assert.Equal(0, snapped.Y, 6);
        Assert.Equal(Math.Sqrt(100 * 100 + 12 * 12), snapped.X, 6);
        var diagonal = Drawing.DrawingBoard.SnapAngle(new ScreenPoint(0, 0), new ScreenPoint(50, -45));
        Assert.Equal(-diagonal.X, diagonal.Y, 6);

        var board = new Drawing.DrawingBoard();
        board.Begin(Drawing.DrawingTool.Arrow, Red, 6, new ScreenPoint(10, 10));
        Assert.False(board.End(new ScreenPoint(13, 12), false));
        board.Begin(Drawing.DrawingTool.Arrow, Red, 6, new ScreenPoint(10, 10));
        Assert.True(board.End(new ScreenPoint(200, 30), true));
        Assert.Equal(10, board.Shapes[0].End.Y, 6);
    }
}

public class ScreenshotFilesTests
{
    [Fact]
    public void Screenshot_names_are_dated_and_never_overwrite_an_existing_file()
    {
        var folder = Path.Combine("C:", "Images", "Pointik");
        var now = new DateTime(2026, 11, 1, 21, 3, 5);
        var first = Drawing.ScreenshotFiles.NextPath(folder, now, _ => false);
        Assert.Equal(Path.Combine(folder, "Pointik 2026-11-01 21-03-05.png"), first);

        var taken = new HashSet<string> { first, Path.Combine(folder, "Pointik 2026-11-01 21-03-05 (2).png") };
        Assert.Equal(Path.Combine(folder, "Pointik 2026-11-01 21-03-05 (3).png"),
            Drawing.ScreenshotFiles.NextPath(folder, now, taken.Contains));
    }
}
