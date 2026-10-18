using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.Json;

namespace GrowthBalanceTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new GrowthForm());
    }
}

internal sealed class GrowthForm : Form
{
    private readonly Dictionary<string, NumericUpDown> inputs = new();
    private readonly NumericUpDown graphHeight = new();
    private readonly Label saveStatus = new();
    private readonly Label summary = new();
    private readonly FlowLayoutPanel graphs = new();
    private readonly GrowthChart levels = new("Level reached", "Level", "Projected from food", "Expected");
    private readonly GrowthChart sizes = new("Size and speed accumulated", "Multiplier ×",
        "Player size", "Food size", "Speed");
    private readonly GrowthChart xp = new("XP growth", "XP", "XP per food", "Next-level XP gap");
    private readonly string settingsPath = Path.Combine(AppContext.BaseDirectory, "GrowthBalanceTool.settings.json");
    private bool loadingSettings;

    public GrowthForm()
    {
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Eat-Eat Cloud-Cloud · Growth Balance";
        Width = 1160;
        Height = 880;
        MinimumSize = new Size(850, 580);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = SystemColors.Window;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        header.Controls.Add(new Label
        {
            Text = "Round growth model",
            Font = new Font("Segoe UI", 16, FontStyle.Regular),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        saveStatus.Dock = DockStyle.Fill;
        saveStatus.TextAlign = ContentAlignment.MiddleRight;
        saveStatus.ForeColor = SystemColors.GrayText;
        header.Controls.Add(saveStatus, 1, 0);
        header.Controls.Add(new Label
        {
            Text = "Graph height",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        }, 2, 0);
        graphHeight.Minimum = 200;
        graphHeight.Maximum = 700;
        graphHeight.Increment = 20;
        graphHeight.Value = 280;
        graphHeight.Dock = DockStyle.Fill;
        graphHeight.TextAlign = HorizontalAlignment.Right;
        graphHeight.Margin = new Padding(4, 9, 0, 9);
        void ApplyGraphHeight(int height)
        {
            graphs.SuspendLayout();
            foreach (GrowthChart graph in graphs.Controls)
            {
                graph.Height = height;
                graph.Invalidate();
            }
            graphs.ResumeLayout(true);
        }
        graphHeight.ValueChanged += (_, _) => ApplyGraphHeight((int)graphHeight.Value);
        graphHeight.TextChanged += (_, _) =>
        {
            if (int.TryParse(graphHeight.Text, NumberStyles.Integer, CultureInfo.CurrentCulture,
                    out int height) && height >= graphHeight.Minimum && height <= graphHeight.Maximum)
                ApplyGraphHeight(height);
        };
        header.Controls.Add(graphHeight, 3, 0);
        var saveButton = new Button
        {
            Text = "Save",
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 8, 0, 8)
        };
        saveButton.Click += (_, _) => SaveSettings();
        header.Controls.Add(saveButton, 4, 0);
        layout.Controls.Add(header, 0, 0);

        var inputGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2 };
        for (int i = 0; i < 5; i++) inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        inputGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        inputGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.Controls.Add(inputGrid, 0, 1);

        AddInput(inputGrid, 0, "cycles", "Cycles", 10, 1, 30, 1, 0);
        AddInput(inputGrid, 1, "targetGain", "Expected levels gained / cycle", 1, 0, 10, .25m, 2);
        AddInput(inputGrid, 2, "foodPerCycle", "Food eaten / player / cycle", 12, 0, 1000, 1, 0);
        AddInput(inputGrid, 3, "baseFoodXp", "Base XP per food", 1, 0, 10000, .1m, 1);
        AddInput(inputGrid, 4, "baseGap", "Base XP gap", 10, 1, 100000, 1, 0);
        AddInput(inputGrid, 5, "gapRatio", "XP gap ratio / level", 1.5m, 1, 3, .01m, 2);
        AddInput(inputGrid, 6, "playerSizeRatio", "Player size ratio / level", 1.3m, 1, 3, .01m, 2);
        AddInput(inputGrid, 7, "foodSizeRatio", "Food size ratio / cycle", 1.5m, 1, 3, .01m, 2);
        AddInput(inputGrid, 8, "foodXpRatio", "Food XP ratio / cycle", 1.1m, 1, 3, .01m, 2);
        AddInput(inputGrid, 9, "speedRatio", "Speed ratio / cycle", 1.5m, 1, 3, .01m, 2);

        summary.Dock = DockStyle.Fill;
        summary.TextAlign = ContentAlignment.MiddleLeft;
        summary.AutoEllipsis = false;
        layout.Controls.Add(summary, 0, 2);

        graphs.Dock = DockStyle.Fill;
        graphs.AutoScroll = true;
        graphs.FlowDirection = FlowDirection.TopDown;
        graphs.WrapContents = false;
        graphs.BackColor = SystemColors.Window;
        graphs.Controls.AddRange([levels, sizes, xp]);
        graphs.Resize += (_, _) => SizeCharts();
        layout.Controls.Add(graphs, 0, 3);

        SizeCharts();
        LoadSettings();
        Recalculate();
    }

    private void AddInput(TableLayoutPanel grid, int index, string key, string caption,
        decimal initial, decimal minimum, decimal maximum, decimal increment, int places)
    {
        var box = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(4, 4, 10, 7)
        };
        box.RowStyles.Add(new RowStyle(SizeType.Percent, 57));
        box.RowStyles.Add(new RowStyle(SizeType.Percent, 43));
        var label = new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };
        var input = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            DecimalPlaces = places,
            Value = initial,
            ThousandsSeparator = true
        };
        input.ValueChanged += (_, _) => Recalculate();
        inputs.Add(key, input);
        box.Controls.Add(label, 0, 0);
        box.Controls.Add(input, 0, 1);
        grid.Controls.Add(box, index % 5, index / 5);
    }

    private double Value(string key) => (double)inputs[key].Value;

    private void SaveSettings()
    {
        ValidateChildren();
        var saved = new SavedSettings
        {
            GraphHeight = (int)graphHeight.Value,
            Inputs = inputs.ToDictionary(pair => pair.Key, pair => pair.Value.Value)
        };
        try
        {
            string temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(saved,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, settingsPath, true);
            saveStatus.Text = "Saved";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            saveStatus.Text = "Save failed";
            MessageBox.Show(this, $"Could not save settings to:\n{settingsPath}\n\n{error.Message}",
                "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadSettings()
    {
        if (!File.Exists(settingsPath)) return;
        try
        {
            var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(settingsPath));
            if (saved?.Version != 1 || saved.Inputs == null)
                throw new JsonException("Unsupported or incomplete settings file");

            loadingSettings = true;
            foreach (var (key, value) in saved.Inputs)
            {
                if (inputs.TryGetValue(key, out var input))
                    input.Value = Math.Clamp(value, input.Minimum, input.Maximum);
            }
            if (saved.GraphHeight >= graphHeight.Minimum && saved.GraphHeight <= graphHeight.Maximum)
                graphHeight.Value = saved.GraphHeight;
            saveStatus.Text = "Loaded saved values";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            saveStatus.Text = "Save file unreadable";
        }
        finally
        {
            loadingSettings = false;
        }
    }

    private void Recalculate()
    {
        if (loadingSettings || inputs.Count != 10) return;
        var settings = new GrowthSettings(
            (int)Value("cycles"), Value("targetGain"), (int)Value("foodPerCycle"),
            Value("baseFoodXp"), Value("baseGap"), Value("gapRatio"),
            Value("playerSizeRatio"), Value("foodSizeRatio"), Value("foodXpRatio"),
            Value("speedRatio"));
        var data = GrowthMath.Calculate(settings);
        var last = data[^1];

        summary.Text = $"After cycle {settings.Cycles}: projected level {last.Level} vs expected {last.ExpectedLevel}   ·   " +
                       $"player size {Format(last.PlayerSize)}× vs food {Format(last.FoodSize)}×   ·   " +
                       $"earned {Format(last.TotalXp)} XP vs {Format(last.TargetXp)} XP needed\n" +
                       $"Cumulative speed boost: {Format(Math.Pow(settings.SpeedRatio, settings.Cycles - 1))}×. " +
                       "Food per player is an estimate; player-eating XP, respawns, and old food are excluded.";

        levels.SetData(data.Select(d => (double)d.Level).ToArray(), data.Select(d => (double)d.ExpectedLevel).ToArray());
        sizes.SetData(data.Select(d => d.PlayerSize).ToArray(), data.Select(d => d.FoodSize).ToArray(),
            data.Select(d => Math.Pow(settings.SpeedRatio, d.Cycle - 1)).ToArray());
        xp.SetData(data.Select(d => d.FoodXp).ToArray(), data.Select(d => d.NextGap).ToArray());
    }

    private void SizeCharts()
    {
        int width = Math.Max(400, graphs.ClientSize.Width - 23);
        foreach (Control graph in graphs.Controls) graph.Width = width;
    }

    private static string Format(double number) =>
        number.ToString(number >= 10000 ? "0.##E+0" : "#,##0.##", CultureInfo.CurrentCulture);
}

internal sealed class SavedSettings
{
    public SavedSettings() { }

    public int Version { get; set; } = 1;
    public int GraphHeight { get; set; }
    public Dictionary<string, decimal> Inputs { get; set; } = new();
}

internal sealed record GrowthSettings(int Cycles, double TargetGain, int FoodPerCycle,
    double BaseFoodXp, double BaseGap, double GapRatio, double PlayerSizeRatio,
    double FoodSizeRatio, double FoodXpRatio, double SpeedRatio);

internal sealed record GrowthPoint(int Cycle, int Level, int ExpectedLevel, double PlayerSize,
    double FoodSize, double FoodXp, double NextGap, double TotalXp, double TargetXp);

internal static class GrowthMath
{
    private static double GapAt(int level, GrowthSettings s) =>
        Math.Ceiling(s.BaseGap * Math.Pow(s.GapRatio, level - 1));

    public static GrowthPoint[] Calculate(GrowthSettings s)
    {
        var points = new GrowthPoint[s.Cycles];
        int level = 1;
        double carriedXp = 0, totalXp = 0;
        for (int cycle = 1; cycle <= s.Cycles; cycle++)
        {
            double foodXp = s.BaseFoodXp * Math.Pow(s.FoodXpRatio, cycle - 1);
            double gained = s.FoodPerCycle * foodXp;
            carriedXp += gained;
            totalXp += gained;
            if (s.GapRatio == 1)
            {
                double gap = GapAt(level, s);
                int levelsGained = (int)Math.Min(int.MaxValue - level, Math.Floor(carriedXp / gap));
                carriedXp -= levelsGained * gap;
                level += levelsGained;
            }
            else
            {
                while (carriedXp >= GapAt(level, s))
                {
                    carriedXp -= GapAt(level, s);
                    level++;
                }
            }

            int expected = 1 + (int)Math.Floor(s.TargetGain * cycle + .5);
            double targetXp = 0;
            for (int n = 1; n < expected; n++) targetXp += GapAt(n, s);
            points[cycle - 1] = new GrowthPoint(cycle, level, expected,
                Math.Pow(s.PlayerSizeRatio, level - 1),
                Math.Pow(s.FoodSizeRatio, cycle - 1), foodXp,
                GapAt(level, s), totalXp, targetXp);
        }
        return points;
    }
}

internal sealed class GrowthChart : Control
{
    private static readonly Color FirstColor = Color.FromArgb(35, 110, 190);
    private static readonly Color SecondColor = Color.FromArgb(190, 100, 45);
    private static readonly Color ThirdColor = Color.FromArgb(115, 70, 170);
    private readonly string title, unit, firstName, secondName;
    private readonly string? thirdName;
    private double[] first = [], second = [], third = [];
    private bool capped;

    public GrowthChart(string title, string unit, string firstName, string secondName,
        string? thirdName = null)
    {
        this.title = title;
        this.unit = unit;
        this.firstName = firstName;
        this.secondName = secondName;
        this.thirdName = thirdName;
        Height = 280;
        Margin = new Padding(0, 0, 0, 14);
        BackColor = SystemColors.Window;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    public void SetData(double[] first, double[] second, double[]? third = null)
    {
        capped = first.Concat(second).Concat(third ?? []).Any(value => !double.IsFinite(value) || value > 1e15);
        this.first = first.Select(PlotValue).ToArray();
        this.second = second.Select(PlotValue).ToArray();
        this.third = third?.Select(PlotValue).ToArray() ?? [];
        Invalidate();
    }

    private static double PlotValue(double value) =>
        double.IsFinite(value) ? Math.Min(1e15, value) : 1e15;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (first.Length == 0) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var titleFont = new Font("Segoe UI", 11, FontStyle.Bold);
        using var axisFont = new Font("Segoe UI", 9f);
        using var textBrush = new SolidBrush(SystemColors.ControlText);
        using var subtleBrush = new SolidBrush(SystemColors.GrayText);
        using var borderPen = new Pen(SystemColors.ControlDark, 1);
        using var gridPen = new Pen(SystemColors.ControlLight, 1);
        using var firstPen = new Pen(FirstColor, 2.5f);
        using var secondPen = new Pen(SecondColor, 2.5f) { DashStyle = DashStyle.Dash };
        using var thirdPen = new Pen(ThirdColor, 2.5f) { DashStyle = DashStyle.Dot };

        TextRenderer.DrawText(g, title, titleFont, new Point(4, 3), SystemColors.ControlText,
            TextFormatFlags.NoPadding);
        float legendY = 37;
        g.DrawLine(firstPen, 6, legendY + 6, 27, legendY + 6);
        g.DrawString(firstName, axisFont, textBrush, 32, legendY);
        float secondX = Math.Max(185, 48 + g.MeasureString(firstName, axisFont).Width);
        g.DrawLine(secondPen, secondX, legendY + 6, secondX + 21, legendY + 6);
        g.DrawString(secondName, axisFont, textBrush, secondX + 26, legendY);
        if (thirdName != null)
        {
            float thirdX = Math.Max(secondX + 125,
                secondX + 42 + g.MeasureString(secondName, axisFont).Width);
            g.DrawLine(thirdPen, thirdX, legendY + 6, thirdX + 21, legendY + 6);
            g.DrawString(thirdName, axisFont, textBrush, thirdX + 26, legendY);
        }

        var plot = new RectangleF(84, 70, Math.Max(40, Width - 104), Height - 135);
        double highest = Math.Max(1, first.Concat(second).Concat(third).Max()) * 1.1;
        for (int i = 0; i <= 4; i++)
        {
            float y = plot.Bottom - plot.Height * i / 4f;
            if (i > 0 && i < 4) g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            string tick = Format(highest * i / 4);
            var size = g.MeasureString(tick, axisFont);
            g.DrawString(tick, axisFont, subtleBrush, plot.Left - size.Width - 7, y - size.Height / 2);
        }
        g.DrawRectangle(borderPen, plot.X, plot.Y, plot.Width, plot.Height);
        if (capped)
        {
            const string note = "Plot capped at 1E+15";
            var noteSize = g.MeasureString(note, axisFont);
            g.DrawString(note, axisFont, subtleBrush, plot.Right - noteSize.Width - 4, plot.Top + 3);
        }

        float X(int i) => plot.Left + 5 + (first.Length == 1 ? .5f : (float)i / (first.Length - 1)) * (plot.Width - 10);
        float Y(double value) => plot.Bottom - 5 - (float)(value / highest) * (plot.Height - 10);
        DrawSeries(g, secondPen, SecondColor, second, X, Y);
        if (third.Length > 0)
            DrawSeries(g, thirdPen, ThirdColor, third, X, Y, squareMarkers: true);
        DrawSeries(g, firstPen, FirstColor, first, X, Y);

        int tickCount = Math.Min(first.Length, Width < 500 ? 4 : 7);
        var used = new HashSet<int>();
        for (int i = 0; i < tickCount; i++)
        {
            int cycle = 1 + (int)Math.Round(i * (first.Length - 1d) / Math.Max(1, tickCount - 1));
            if (!used.Add(cycle)) continue;
            string label = cycle.ToString(CultureInfo.CurrentCulture);
            var size = g.MeasureString(label, axisFont);
            g.DrawString(label, axisFont, subtleBrush, X(cycle - 1) - size.Width / 2, plot.Bottom + 6);
        }
        string xLabel = "Cycle";
        var xSize = g.MeasureString(xLabel, axisFont);
        g.DrawString(xLabel, axisFont, textBrush, plot.Left + (plot.Width - xSize.Width) / 2, Height - 23);
        var transform = g.Save();
        g.TranslateTransform(3, plot.Top + plot.Height / 2);
        g.RotateTransform(-90);
        var unitSize = g.MeasureString(unit, axisFont);
        g.DrawString(unit, axisFont, textBrush, -unitSize.Width / 2, 0);
        g.Restore(transform);
    }

    private static void DrawSeries(Graphics g, Pen pen, Color color, double[] values,
        Func<int, float> x, Func<double, float> y, bool squareMarkers = false)
    {
        if (values.Length > 1)
            g.DrawLines(pen, values.Select((value, index) => new PointF(x(index), y(value))).ToArray());
        using var brush = new SolidBrush(color);
        for (int i = 0; i < values.Length; i++)
        {
            if (squareMarkers)
                g.FillRectangle(brush, x(i) - 3, y(values[i]) - 3, 6, 6);
            else
                g.FillEllipse(brush, x(i) - 3, y(values[i]) - 3, 6, 6);
        }
    }

    private static string Format(double value) =>
        value.ToString(value >= 10000 ? "0.#E+0" : "#,##0.#", CultureInfo.CurrentCulture);
}
