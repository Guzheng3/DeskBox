using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace DeskBox.Views;

public sealed partial class ContentWidgetWindow
{
    private MenuFlyoutItem CreateWidgetRuleMenuItem()
    {
        var localization = App.Current.LocalizationService;
        bool hasActiveRule = SettingsService.Settings.DesktopOrganizationRules.Any(rule =>
            string.Equals(rule.TargetWidgetId, _config.Id, StringComparison.Ordinal) &&
            rule.IsEnabled &&
            HasRuleAssignments(rule));

        var item = new MenuFlyoutItem
        {
            Text = localization.T("Widget.RuleEditor.MenuLabel"),
            Icon = new FontIcon { Glyph = hasActiveRule ? "\uE73E" : "\uE8CB" }
        };
        item.Click += (_, _) => DispatcherQueue.TryEnqueue(async () =>
            await ShowWidgetRuleEditorAsync());
        return item;
    }

    private MenuFlyoutItem CreateDesktopOrganizationMenuItem()
    {
        var item = new MenuFlyoutItem
        {
            Text = App.Current.LocalizationService.T("Tray.OrganizeDesktop"),
            Icon = new FontIcon { Glyph = "\uE8FD" }
        };
        item.Click += (_, _) => DispatcherQueue.TryEnqueue(() =>
            App.Current.ShowDesktopOrganizationWindow());
        return item;
    }

    internal async Task ShowWidgetRuleEditorAsync()
    {
        if (RootElement.XamlRoot is null)
        {
            return;
        }

        var localization = App.Current.LocalizationService;
        var settings = SettingsService.Settings;
        DesktopOrganizationRule? registered = settings.DesktopOrganizationRules
            .FirstOrDefault(rule => string.Equals(rule.TargetWidgetId, _config.Id, StringComparison.Ordinal));

        // Edit a detached draft so canceling leaves persisted state untouched.
        var draft = new DesktopOrganizationRule
        {
            TargetWidgetId = _config.Id,
            IsEnabled = registered is null || registered.IsEnabled,
            CategoryIds = registered?.CategoryIds.ToList() ?? [],
            SubtypeIds = registered?.SubtypeIds.ToList() ?? [],
            Extensions = registered?.Extensions.ToList() ?? [],
            ExcludedExtensions = registered?.ExcludedExtensions.ToList() ?? [],
            RecentDaysWindow = registered?.RecentDaysWindow
        };

        var enableToggle = new ToggleSwitch
        {
            Header = localization.T("Widget.RuleEditor.EnableRule"),
            IsOn = draft.IsEnabled
        };

        var extensionInput = new TextBox
        {
            Header = localization.T("DesktopOrganization.Rule.Extensions"),
            PlaceholderText = localization.T("DesktopOrganization.Rule.ExtensionPlaceholder")
        };
        var extensionChips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        void RebuildExtensionChips()
        {
            extensionChips.Children.Clear();
            foreach (string extension in draft.Extensions)
            {
                var chip = new Button
                {
                    Content = $"{extension}  ×",
                    Tag = extension,
                    Padding = new Thickness(9, 4, 9, 4)
                };
                chip.Click += (_, _) =>
                {
                    draft.Extensions.RemoveAll(value =>
                        string.Equals(value, extension, StringComparison.OrdinalIgnoreCase));
                    RebuildExtensionChips();
                };
                extensionChips.Children.Add(chip);
            }
        }
        RebuildExtensionChips();

        void AddExtensionFromInput()
        {
            string extension = DesktopOrganizationClassifier.NormalizeExtension(extensionInput.Text);
            if (string.IsNullOrEmpty(extension))
            {
                return;
            }

            if (!draft.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                draft.Extensions.Add(extension);
            }

            extensionInput.Text = string.Empty;
            RebuildExtensionChips();
        }

        var addExtensionButton = new Button
        {
            Content = localization.T("DesktopOrganization.Common.Add"),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        addExtensionButton.Click += (_, _) => AddExtensionFromInput();
        extensionInput.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                AddExtensionFromInput();
            }
        };
        var extensionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        extensionRow.Children.Add(extensionInput);
        extensionRow.Children.Add(addExtensionButton);

        var categoryChecks = new StackPanel { Spacing = 4 };
        foreach (string categoryId in DesktopOrganizationCategoryIds.DefaultOrder)
        {
            var checkBox = new CheckBox
            {
                Content = CreateRuleOptionContent(
                    localization.T($"DesktopOrganization.Category.{categoryId}"),
                    DesktopOrganizationClassifier.GetCategoryExtensions(categoryId),
                    categoryId == DesktopOrganizationCategoryIds.Other
                        ? localization.T("DesktopOrganization.Rule.OtherExtensions")
                        : null),
                Tag = categoryId,
                IsChecked = draft.CategoryIds.Contains(categoryId, StringComparer.Ordinal)
            };
            checkBox.Click += (_, _) =>
            {
                if (checkBox.IsChecked == true)
                {
                    if (!draft.CategoryIds.Contains(categoryId, StringComparer.Ordinal))
                    {
                        draft.CategoryIds.Add(categoryId);
                    }
                }
                else
                {
                    draft.CategoryIds.RemoveAll(value =>
                        string.Equals(value, categoryId, StringComparison.Ordinal));
                }
            };
            categoryChecks.Children.Add(checkBox);
        }

        var subtypeChecks = new StackPanel { Spacing = 4 };
        foreach (string subtypeId in new[]
                 {
                     DesktopOrganizationSubtypeIds.Pdf,
                     DesktopOrganizationSubtypeIds.Word,
                     DesktopOrganizationSubtypeIds.Excel,
                     DesktopOrganizationSubtypeIds.PowerPoint,
                     DesktopOrganizationSubtypeIds.Text,
                     DesktopOrganizationSubtypeIds.Audio,
                     DesktopOrganizationSubtypeIds.Video
                 })
        {
            var checkBox = new CheckBox
            {
                Content = CreateRuleOptionContent(
                    localization.T($"DesktopOrganization.Subtype.{subtypeId}"),
                    DesktopOrganizationClassifier.GetSubtypeExtensions(subtypeId)),
                Tag = subtypeId,
                IsChecked = draft.SubtypeIds.Contains(subtypeId, StringComparer.Ordinal)
            };
            checkBox.Click += (_, _) =>
            {
                if (checkBox.IsChecked == true)
                {
                    if (!draft.SubtypeIds.Contains(subtypeId, StringComparer.Ordinal))
                    {
                        draft.SubtypeIds.Add(subtypeId);
                    }
                }
                else
                {
                    draft.SubtypeIds.RemoveAll(value =>
                        string.Equals(value, subtypeId, StringComparison.Ordinal));
                }
            };
            subtypeChecks.Children.Add(checkBox);
        }

        var excludedInput = new TextBox
        {
            Header = localization.T("DesktopOrganization.Rule.Exclusions"),
            PlaceholderText = localization.T("DesktopOrganization.Rule.ExclusionPlaceholder")
        };
        var excludedChips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        void RebuildExcludedChips()
        {
            excludedChips.Children.Clear();
            foreach (string extension in draft.ExcludedExtensions)
            {
                var chip = new Button
                {
                    Content = $"{extension}  ×",
                    Tag = extension,
                    Padding = new Thickness(9, 4, 9, 4)
                };
                chip.Click += (_, _) =>
                {
                    draft.ExcludedExtensions.RemoveAll(value =>
                        string.Equals(value, extension, StringComparison.OrdinalIgnoreCase));
                    RebuildExcludedChips();
                };
                excludedChips.Children.Add(chip);
            }
        }
        RebuildExcludedChips();

        void AddExcludedFromInput()
        {
            string extension = DesktopOrganizationClassifier.NormalizeExtension(excludedInput.Text);
            if (string.IsNullOrEmpty(extension))
            {
                return;
            }

            if (!draft.ExcludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                draft.ExcludedExtensions.Add(extension);
            }

            excludedInput.Text = string.Empty;
            RebuildExcludedChips();
        }

        var addExcludedButton = new Button
        {
            Content = localization.T("DesktopOrganization.Common.Add"),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        addExcludedButton.Click += (_, _) => AddExcludedFromInput();
        excludedInput.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                AddExcludedFromInput();
            }
        };
        var excludedRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        excludedRow.Children.Add(excludedInput);
        excludedRow.Children.Add(addExcludedButton);

        var dateLimitToggle = new ToggleSwitch
        {
            Header = localization.T("Widget.RuleEditor.DateHeader"),
            IsOn = draft.RecentDaysWindow is > 0
        };
        var daysInput = new NumberBox
        {
            Header = localization.T("Widget.RuleEditor.DateDaysLabel"),
            Value = draft.RecentDaysWindow ?? 7,
            Minimum = 1,
            Maximum = SettingsService.MaxRecentDaysWindow,
            SmallChange = 1,
            LargeChange = 7,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            MinWidth = 160,
            IsEnabled = draft.RecentDaysWindow is > 0
        };
        dateLimitToggle.Toggled += (_, _) => daysInput.IsEnabled = dateLimitToggle.IsOn;
        var dateNote = new TextBlock
        {
            Text = localization.T("Widget.RuleEditor.DateNote"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };

        var panel = new StackPanel { Spacing = 18, MinWidth = 420, MaxWidth = 480 };
        panel.Children.Add(enableToggle);
        panel.Children.Add(extensionRow);
        panel.Children.Add(extensionChips);
        panel.Children.Add(CreateDialogSectionHeader(
            localization.T("Widget.RuleEditor.CategoriesHeader")));
        panel.Children.Add(categoryChecks);
        panel.Children.Add(CreateDialogSectionHeader(
            localization.T("Widget.RuleEditor.SubtypesHeader")));
        panel.Children.Add(subtypeChecks);
        panel.Children.Add(excludedRow);
        panel.Children.Add(excludedChips);
        panel.Children.Add(dateLimitToggle);
        panel.Children.Add(daysInput);
        panel.Children.Add(dateNote);

        var dialog = new ContentDialog
        {
            XamlRoot = RootElement.XamlRoot,
            Title = localization.Format("Widget.RuleEditor.Title", _config.Name),
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            PrimaryButtonText = localization.T("Common.Save"),
            CloseButtonText = localization.T("Common.Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        try
        {
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            draft.RecentDaysWindow = dateLimitToggle.IsOn && !double.IsNaN(daysInput.Value)
                ? (int)Math.Clamp(daysInput.Value, 1, SettingsService.MaxRecentDaysWindow)
                : null;
            ApplyWidgetRuleDraft(draft, registered);
            await SettingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopOrganization] Widget rule editor failed: {ex}");
        }
    }

    private void ApplyWidgetRuleDraft(
        DesktopOrganizationRule draft,
        DesktopOrganizationRule? registered)
    {
        var settings = SettingsService.Settings;
        DesktopOrganizationRule rule = registered ?? draft;

        if (registered is null)
        {
            if (!HasRuleAssignments(draft))
            {
                return;
            }

            settings.DesktopOrganizationRules.Add(rule);
        }
        else
        {
            rule.CategoryIds = draft.CategoryIds;
            rule.SubtypeIds = draft.SubtypeIds;
            rule.ExcludedExtensions = draft.ExcludedExtensions;
            rule.RecentDaysWindow = draft.RecentDaysWindow;
        }

        // Extensions are exclusive across rules: reassign ownership silently.
        rule.Extensions.Clear();
        var resolver = new DesktopOrganizationRuleResolver();
        foreach (string extension in draft.Extensions)
        {
            resolver.AssignExtensionExclusively(
                settings.DesktopOrganizationRules,
                _config.Id,
                extension);
        }

        // Categories and subtypes follow the same exclusive ownership model.
        foreach (DesktopOrganizationRule other in settings.DesktopOrganizationRules)
        {
            if (ReferenceEquals(other, rule) ||
                string.Equals(other.TargetWidgetId, _config.Id, StringComparison.Ordinal))
            {
                continue;
            }

            other.CategoryIds.RemoveAll(value => rule.CategoryIds.Contains(value, StringComparer.Ordinal));
            other.SubtypeIds.RemoveAll(value => rule.SubtypeIds.Contains(value, StringComparer.Ordinal));
        }

        rule.IsEnabled = HasRuleAssignments(rule) && draft.IsEnabled;
    }

    private static bool HasRuleAssignments(DesktopOrganizationRule rule) =>
        rule.CategoryIds.Count > 0 ||
        rule.SubtypeIds.Count > 0 ||
        rule.Extensions.Count > 0 ||
        rule.RecentDaysWindow is > 0;

    private static TextBlock CreateDialogSectionHeader(string text) => new()
    {
        Text = text,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
    };

    private static FrameworkElement CreateRuleOptionContent(
        string title,
        IReadOnlyList<string> extensions,
        string? emptyDescription = null)
    {
        var content = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(0, 3, 0, 3)
        };
        content.Children.Add(new TextBlock
        {
            Text = title,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = extensions.Count > 0
                ? string.Join("  ·  ", extensions)
                : emptyDescription ?? string.Empty,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420
        });
        return content;
    }
}
