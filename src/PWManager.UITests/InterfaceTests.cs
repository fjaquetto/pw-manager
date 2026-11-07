using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PWManager.ViewModels;
using PWManager.Views;

namespace PWManager.UITests;

public class InterfaceTests
{
    private static T Find<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static MainViewModel Vault(Window window) => (MainViewModel)((VaultPanel)window.Content!).DataContext!;

    private static Window Open(int width = 1180, int height = 780, int count = 7)
    {
        var viewModel = MemoryVault.Sample(count).CreateViewModel();
        UiTestPump.WaitFor(viewModel.LoadEntriesAsync());
        var window = new Window
        {
            Content = new VaultPanel
            {
                DataContext = viewModel
            },
            Width = width,
            Height = height
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [AvaloniaTheory]
    [InlineData(760, 480, true)]
    [InlineData(900, 600, true)]
    [InlineData(1180, 780, false)]
    public void Layout_AdaptsWithoutHorizontalOverflow(int width, int height, bool compact)
    {
        var window = Open(width, height);

        try
        {
            var viewModel = Vault(window);

            Assert.Equal(compact, viewModel.IsCompact);
            Assert.True(Find<EntryListView>(window, "ListRegion").IsEffectivelyVisible);
            Assert.Equal(!compact, Find<Border>(window, "PaneRegion").IsEffectivelyVisible);
            Assert.True(Find<TextBox>(window, "SearchBox").Bounds.Width > 80);
            var placeholder = Find<TextBox>(window, "SearchBox").GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Placeholder");

            Assert.Equal(1d, placeholder.Opacity);
            viewModel.SelectedEntry = viewModel.Entries[1];
            Dispatcher.UIThread.RunJobs();

            Assert.True(Find<Border>(window, "PaneRegion").IsEffectivelyVisible);
            Assert.True(Find<TextBox>(window, "DetailPassword").Bounds.Width > 120);
            var pane = Find<Border>(window, "PaneRegion");

            Assert.True(pane.Bounds.Width <= window.Bounds.Width);
            Capture(window, $"vault-{width}x{height}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PrimaryActionLabels_StayVerticallyCentered()
    {
        var window = Open();

        try
        {
            var viewModel = Vault(window);
            viewModel.SelectedEntry = viewModel.Entries[0];
            Dispatcher.UIThread.RunJobs();

            var buttons = new[]
            {
                window.GetVisualDescendants().OfType<Button>()
                    .Single(button => (button.Content as string)?.StartsWith('+') == true),
                window.GetVisualDescendants().OfType<Button>()
                    .Single(button => Equals(button.Content, "Edit entry"))
            };

            foreach (var button in buttons)
            {
                var label = button.GetVisualDescendants().OfType<TextBlock>().First();
                var origin = label.TranslatePoint(default, button)!.Value;

                Assert.Equal(button.Bounds.Height / 2, origin.Y + label.Bounds.Height / 2, 1.5);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selection_FilterRefresh_PreservesSelectedEntryAndMask()
    {
        var window = Open();

        try
        {
            var viewModel = Vault(window);
            var list = Find<ListBox>(window, "EntriesList");
            list.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(viewModel.SelectedEntry);
            var id = viewModel.SelectedEntry.Id;
            viewModel.SearchGlobal = "no-match";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(id, viewModel.SelectedEntry!.Id);
            Assert.True(viewModel.SavedEntryHidden);
            viewModel.ClearFiltersCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(id, ((PasswordEntryViewModel)list.SelectedItem!).Id);
            Assert.Equal('•', Find<TextBox>(window, "DetailPassword").PasswordChar);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardSearch_New_SaveAndEscape_WorkThroughBindings()
    {
        var window = Open();

        try
        {
            var viewModel = Vault(window);
            var search = Find<TextBox>(window, "SearchBox");
            search.Focus();
            Dispatcher.UIThread.RunJobs();

            Assert.True(search.IsFocused);
            window.KeyTextInput("github");

            Assert.Single(viewModel.Entries);
            window.KeyPress(Key.N, RawInputModifiers.Control, PhysicalKey.N, null);
            window.KeyRelease(Key.N, RawInputModifiers.Control, PhysicalKey.N, null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.IsEditorOpen);
            Assert.True(Find<TextBox>(window, "EditorSite").IsFocused);
            viewModel.Editor.Site = "A new entry";
            viewModel.Editor.Login = "login";
            viewModel.Editor.Password = "draft";
            window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, null);
            window.KeyRelease(Key.S, RawInputModifiers.Control, PhysicalKey.S, null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.IsEditorOpen);
            Assert.Equal("A new entry", viewModel.SelectedEntry!.Site);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editor_GeneratorAndDiscard_KeepFocusWithinDialogAndRestoreMasks()
    {
        var window = Open();
        var viewModel = Vault(window);

        try
        {
            viewModel.NewEntryCommand.Execute(null);
            viewModel.Editor.Site = "Example";
            viewModel.Editor.Login = "alex@example.com";
            viewModel.Editor.Password = "draft";
            viewModel.Editor.IsPasswordVisible = true;
            viewModel.OpenEditorGeneratorCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(Find<TextBox>(window, "GeneratedPassword").IsFocused);
            Assert.False(Find<TextBox>(window, "SearchBox").IsEffectivelyEnabled);
            for (var i = 0; i < 20; i++)
            {
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
                var focused = Assert.IsAssignableFrom<Control>(window.FocusManager!.GetFocusedElement());

                Assert.Contains(Find<Border>(window, "GeneratorLayer"), focused.GetVisualAncestors());
            }

            Assert.Equal('•', Find<TextBox>(window, "EditorPassword").PasswordChar);
            var numeric = window.GetVisualDescendants().OfType<NumericUpDown>().Single();
            numeric.Value = 32;

            Assert.Equal(32, viewModel.Generator.Length);
            Assert.Equal(32, viewModel.Generator.GeneratedPassword.Length);
            Capture(window, "generator");
            viewModel.UseGeneratedPasswordCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(Find<TextBox>(window, "EditorPassword").IsFocused);
            Assert.Equal(viewModel.Generator.GeneratedPassword, viewModel.Editor.Password);
            Capture(window, "editor");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.IsDiscardPending);
            Assert.True(Find<Button>(window, "KeepEditing").IsFocused);
            viewModel.DiscardChangesCommand.Execute(null);

            Assert.False(viewModel.IsEditorOpen);
        }
        finally
        {
            if (viewModel.IsDiscardPending)
            {
                viewModel.DiscardChangesCommand.Execute(null);
            }

            if (viewModel.IsEditorOpen)
            {
                viewModel.CancelEditingCommand.Execute(null);
                viewModel.DiscardChangesCommand.Execute(null);
            }

            window.Close();
        }
    }

    [AvaloniaFact]
    public void DeleteConfirmation_StartsOnCancel_AndEscapeDoesNotDelete()
    {
        var window = Open();

        try
        {
            var viewModel = Vault(window);
            viewModel.SelectedEntry = viewModel.Entries[0];
            viewModel.RequestDeleteCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(Find<Button>(window, "CancelDelete").IsFocused);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.IsDeletePending);
            Assert.Equal(7, viewModel.EntryCount);
            Assert.True(Find<Button>(window, "DetailDelete").IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ThousandEntries_AreVirtualized()
    {
        var window = Open(count: 1000);

        try
        {
            var list = Find<ListBox>(window, "EntriesList");

            Assert.Equal(1000, list.ItemCount);
            Assert.InRange(list.GetVisualDescendants().OfType<ListBoxItem>().Count(), 1, 40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ListCopyButton_HasWorkingCompiledCommandBinding()
    {
        var vault = MemoryVault.Sample();
        var viewModel = vault.CreateViewModel();
        UiTestPump.WaitFor(viewModel.LoadEntriesAsync());
        var window = new Window
        {
            Content = new VaultPanel
            {
                DataContext = viewModel
            }
        };
        window.Show();

        try
        {
            Dispatcher.UIThread.RunJobs();
            var button = Find<ListBox>(window, "EntriesList").GetVisualDescendants().OfType<Button>().First();
            var command = Assert.IsAssignableFrom<CommunityToolkit.Mvvm.Input.IAsyncRelayCommand>(button.Command);

            Assert.IsType<PasswordEntryViewModel>(button.CommandParameter);
            await command.ExecuteAsync(button.CommandParameter);

            Assert.Equal("Example-password-42!", vault.CopiedText);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SmallWindow_DialogAndEditorActionsRemainReachable()
    {
        var window = Open(760, 480);
        var viewModel = Vault(window);

        try
        {
            viewModel.OpenGeneratorCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            var use = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Use in new entry"));
            var position = use.TranslatePoint(default, window)!.Value;

            Assert.True(position.Y >= 0 && position.Y + use.Bounds.Height <= window.Bounds.Height);
            Capture(window, "generator-compact");
            viewModel.CloseGeneratorCommand.Execute(null);
            viewModel.NewEntryCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Capture(window, "editor-compact");
            var save = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save entry"));
            var savePosition = save.TranslatePoint(default, window)!.Value;

            Assert.True(savePosition.Y + save.Bounds.Height <= window.Bounds.Height);
            viewModel.CancelEditingCommand.Execute(null);
            viewModel.FiltersExpanded = true;
            Dispatcher.UIThread.RunJobs();
            Capture(window, "filters-compact");

            Assert.True(Find<ListBox>(window, "EntriesList").Bounds.Height >= 76);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EmptyVault_HasActionableState()
    {
        var window = Open(count: 0);

        try
        {
            var viewModel = Vault(window);

            Assert.True(viewModel.IsVaultEmpty);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "A fresh start");
            Capture(window, "empty-vault");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Render_AtDifferentPixelDensities(double scale)
    {
        var window = Open();

        try
        {
            var viewModel = Vault(window);
            viewModel.SelectedEntry = viewModel.Entries[1];
            window.SetRenderScaling(scale);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(scale, window.RenderScaling);
            using var bitmap = Render(window, scale);

            Assert.Equal((int)(window.Bounds.Width * scale), bitmap.PixelSize.Width);
            Assert.Equal((int)(window.Bounds.Height * scale), bitmap.PixelSize.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void LongContent_RemainsInsideThePane()
    {
        var window = Open(760, 480);

        try
        {
            var viewModel = Vault(window);
            viewModel.SelectedEntry = viewModel.Entries[0];
            viewModel.SelectedEntry.Site = new string('W', 180);
            viewModel.SelectedEntry.Login = new string('w', 180) + "@example.com";
            Dispatcher.UIThread.RunJobs();

            Assert.True(Find<TextBox>(window, "DetailPassword").Bounds.Width > 120);
            Capture(window, "long-content");
        }
        finally
        {
            window.Close();
        }
    }

    private static RenderTargetBitmap Render(Window window, double scale = 1)
    {
        Dispatcher.UIThread.RunJobs();
        var pixelSize = new PixelSize((int)(window.Bounds.Width * scale), (int)(window.Bounds.Height * scale));
        var resolution = new Vector(96 * scale, 96 * scale);
        var bitmap = new RenderTargetBitmap(pixelSize, resolution);
        bitmap.Render(window);

        return bitmap;
    }

    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("PWMANAGER_SCREENSHOTS");

        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using var bitmap = Render(window);
        bitmap.Save(Path.Combine(directory, name + ".png"));
    }
}
