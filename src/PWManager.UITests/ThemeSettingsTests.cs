using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PWManager.Domain.Appearance;
using PWManager.Infra.Configuration;
using PWManager.Services;
using PWManager.ViewModels;
using PWManager.Views;
using AvaloniaApp = Avalonia.Application;

namespace PWManager.UITests;

public class ThemeSettingsTests
{
    private static T Find<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static ThemeSettingsViewModel OpenSettings(out ColorThemeStore store)
    {
        store = new ColorThemeStore(Path.Combine(Path.GetTempPath(), "pwmanager-ui-theme-" + Guid.NewGuid().ToString("N")));
        var viewModel = new ThemeSettingsViewModel(new ThemeService(), store);
        viewModel.Open();

        return viewModel;
    }

    private static Window OpenView(ThemeSettingsViewModel viewModel)
    {
        var window = new Window
        {
            Content = new ThemeSettingsView { DataContext = viewModel },
            Width = 760,
            Height = 700
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static Color BrushColor(string key)
    {
        Assert.True(AvaloniaApp.Current!.TryFindResource(key, out var resource));

        return Assert.IsType<SolidColorBrush>(resource).Color;
    }

    private static void RestoreDefaults()
    {
        new ThemeService().Apply(ColorScheme.Default);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ThemeSettingsView_RendersEveryColorPickerWithoutBindingLoops()
    {
        var viewModel = OpenSettings(out _);
        var window = OpenView(viewModel);

        try
        {
            var pickers = window.GetVisualDescendants().OfType<ColorPicker>().ToList();

            Assert.Equal(17, pickers.Count);
            Assert.Equal(pickers.Count, pickers.Select(picker => (string?)picker.Tag).Distinct().Count());
        }
        finally
        {
            window.Close();
            RestoreDefaults();
        }
    }

    [AvaloniaFact]
    public void BuiltInPresets_AreListedAndApplyLive()
    {
        var viewModel = OpenSettings(out _);
        var window = OpenView(viewModel);

        try
        {
            var expected = new[] { "Default" }
                .Concat(ColorSchemePresets.All.Select(preset => preset.Name));
            Assert.Equal(expected, viewModel.Profiles);

            var preset = ColorSchemePresets.MidnightOcean;
            viewModel.SelectedProfile = preset.Name;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(Color.Parse(preset.Colors.Accent), BrushColor("AccentBrush"));
            Assert.Equal(Color.Parse(preset.Colors.Canvas), BrushColor("CanvasBrush"));
            Assert.Equal(Color.Parse(preset.Colors.Title), BrushColor("TitleBrush"));
            Assert.False(viewModel.CanDeleteProfile);
        }
        finally
        {
            window.Close();
            RestoreDefaults();
        }
    }

    [AvaloniaFact]
    public void LivePreview_UpdatesTheSharedBrushesAndCancelRestoresThem()
    {
        var viewModel = OpenSettings(out _);
        var window = OpenView(viewModel);

        try
        {
            viewModel.TextColor = "#FACC15";
            viewModel.AccentColor = "#2563EB";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(Color.Parse("#FACC15"), BrushColor("TextBrush"));
            Assert.Equal(Color.Parse("#2563EB"), BrushColor("AccentBrush"));
            Assert.Equal(Color.Parse(ColorDerivation.Hover(RgbColor.Parse("#2563EB")).ToHex()), BrushColor("AccentHoverBrush"));

            viewModel.CancelCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(Color.Parse(ColorSchemeDefaults.Text), BrushColor("TextBrush"));
            Assert.Equal(Color.Parse(ColorSchemeDefaults.Accent), BrushColor("AccentBrush"));
            Assert.Equal(Color.Parse(ColorSchemeDefaults.AccentHover), BrushColor("AccentHoverBrush"));
        }
        finally
        {
            window.Close();
            RestoreDefaults();
        }
    }

    [AvaloniaFact]
    public void ColorPickerEdit_WritesBackToTheViewModel()
    {
        var viewModel = OpenSettings(out _);
        var window = OpenView(viewModel);

        try
        {
            var picker = window.GetVisualDescendants().OfType<ColorPicker>()
                .Single(control => Equals(control.Tag, "AccentColor"));

            picker.Color = Color.Parse("#2563EB");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("#2563EB", viewModel.AccentColor);
            Assert.Equal(Color.Parse("#2563EB"), BrushColor("AccentBrush"));
        }
        finally
        {
            window.Close();
            RestoreDefaults();
        }
    }

    [AvaloniaFact]
    public void OverlayPreview_KeepsTheAlphaChannel()
    {
        var viewModel = OpenSettings(out _);
        var window = OpenView(viewModel);

        try
        {
            viewModel.OverlayColor = "#E6090A0B";
            Dispatcher.UIThread.RunJobs();

            Assert.True(AvaloniaApp.Current!.TryFindResource("ModalOverlayBrush", out var resource));
            var brush = Assert.IsType<SolidColorBrush>(resource);

            Assert.Equal(Color.Parse("#E6090A0B"), brush.Color);
        }
        finally
        {
            window.Close();
            RestoreDefaults();
        }
    }

    [AvaloniaFact]
    public void AccentChange_UpdatesTheFluentPaletteForRuntimeControls()
    {
        var viewModel = OpenSettings(out _);
        var window = OpenView(viewModel);

        try
        {
            viewModel.AccentColor = "#2563EB";
            Dispatcher.UIThread.RunJobs();

            var fluentTheme = AvaloniaApp.Current!.Styles.OfType<FluentTheme>().First();
            var palettes = fluentTheme.Palettes.Values.ToList();

            Assert.NotEmpty(palettes);
            Assert.All(palettes, palette => Assert.Equal(Color.Parse("#2563EB"), palette.Accent));
        }
        finally
        {
            window.Close();
            RestoreDefaults();
        }
    }

    [AvaloniaFact]
    public void VaultPanel_AppearanceModal_OpensClosesAndKeepsFocusInside()
    {
        var viewModel = MemoryVault.Sample(3).CreateViewModel();
        viewModel.LoadEntriesAsync().GetAwaiter().GetResult();
        var window = new Window
        {
            Content = new VaultPanel
            {
                DataContext = viewModel
            },
            Width = 1180,
            Height = 780
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            viewModel.OpenThemeSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.IsThemeSettingsOpen);
            Assert.True(viewModel.HasModal);
            Assert.True(Find<Border>(window, "ThemeLayer").IsEffectivelyVisible);
            Assert.Equal(17, window.GetVisualDescendants().OfType<ColorPicker>().Count());

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.IsThemeSettingsOpen);
            Assert.False(Find<Border>(window, "ThemeLayer").IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
            RestoreDefaults();
        }
    }

    [AvaloniaFact]
    public void VaultPanel_AppearanceModal_KeepsTheDeleteButtonClearOfTheScrollBar()
    {
        var viewModel = MemoryVault.Sample(3).CreateViewModel();
        viewModel.LoadEntriesAsync().GetAwaiter().GetResult();
        var window = new Window
        {
            Content = new VaultPanel
            {
                DataContext = viewModel
            },
            Width = 1180,
            Height = 780
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            viewModel.OpenThemeSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var themeView = window.GetVisualDescendants().OfType<ThemeSettingsView>().Single();
            var delete = themeView.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Delete"));
            var scroll = delete.GetVisualAncestors().OfType<ScrollViewer>().First();
            var scrollBar = scroll.GetVisualDescendants().OfType<ScrollBar>()
                .Single(bar => bar.Name == "PART_VerticalScrollBar" && bar.IsEffectivelyVisible);
            var deleteTopLeft = delete.TranslatePoint(default, scroll);
            var scrollBarTopLeft = scrollBar.TranslatePoint(default, scroll);

            Assert.NotNull(deleteTopLeft);
            Assert.NotNull(scrollBarTopLeft);
            Assert.True(deleteTopLeft!.Value.X + delete.Bounds.Width <= scrollBarTopLeft!.Value.X,
                $"The Delete button ends at {deleteTopLeft.Value.X + delete.Bounds.Width} and overlaps the scroll bar starting at {scrollBarTopLeft.Value.X}.");
        }
        finally
        {
            window.Close();
            RestoreDefaults();
        }
    }
}
