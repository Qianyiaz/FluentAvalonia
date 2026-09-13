using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using FluentAvalonia.UI.Media;

namespace FluentAvalonia.Styling;

/// <summary>
///     Theme manager for FluentAvalonia, managing various components of the Fluentv2 theme
///     like AccentColor, styles, and platform settings
/// </summary>
public partial class FluentAvaloniaTheme : Styles, IResourceProvider
{
    /// <summary>
    ///     High Contrast Theme
    /// </summary>
    public static readonly ThemeVariant HighContrastTheme = new("HighContrast", ThemeVariant.Light);

    private ResourceDictionary _accentColorsDictionary;
    private Color? _customAccentColor;

    private bool _hasLoaded;
    private IPlatformSettings _platformSettings;

    /// <summary>
    ///     Create new instance of <see cref="FluentAvaloniaTheme" />.
    /// </summary>
    public FluentAvaloniaTheme()
    {
        MergedDictionaries = [];
        MergedDictionaries.CollectionChanged += MergedDictionariesCollectionChanged;
        Init();
    }

    /// <summary>
    ///     Gets or sets whether the system font should be used on Windows. Value only applies at startup
    /// </summary>
    /// <remarks>
    ///     On Windows 10, this is "Segoe UI", and Windows 11, this is "Segoe UI Variable Text".
    /// </remarks>
    public bool UseSystemFontOnWindows { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to use the current system theme (light or dark mode).
    /// </summary>
    /// <remarks>
    ///     This property is respected on Windows, MacOS, and Linux. However, on linux,
    ///     the detection is different depending on the user's desktop environment. On KDE,
    ///     Cinnamon, LXDE and LXQt, it requires the user's theme (color scheme in the
    ///     case of KDE) name to contain "dark". On GNOME or Xfce, it requires 'color-scheme'
    ///     to be set to either 'prefer-light', 'prefer-dark', or 'gtk-theme' to contain 'dark'.
    ///     Also note, that high contrast theme will only resolve here on Windows.
    /// </remarks>
    public bool PreferSystemTheme
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                // Only call this if PreferSystemTheme is true to invalidate the current theme.
                if (value) ResolveThemeAndInitializeSystemResources();
            }
        }
    }

    /// <summary>
    ///     Gets or sets whether to use the current user's accent color as the resource SystemAccentColor
    /// </summary>
    /// <remarks>
    ///     On Linux, accent color detection is only supported on KDE (from current scheme,
    ///     from wallpaper and custom), LXQt (from selection color) and LXDE (from custom selection
    ///     color).
    /// </remarks>
    public bool PreferUserAccentColor
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                // Unlike PreferSystemTheme, we call this everytime as LoadCustomAccentColor handles
                // switching between a system and custom color (and back)
                LoadCustomAccentColor();
            }
        }
    } = true;

    /// <summary>
    ///     Gets or sets a <see cref="Color" /> to use as the SystemAccentColor for the app. Note this takes precedence over
    ///     the
    ///     <see cref="PreferUserAccentColor" /> property and must be set to null to restore the system color, if desired
    /// </summary>
    /// <remarks>
    ///     The 6 variants (3 light/3 dark) are pregenerated from the given color. FluentAvalonia makes no checks to ensure the
    ///     legibility and
    ///     accessibility of the chosen color and places that responsibility upon you. For more control over the accent color
    ///     variants, directly
    ///     override SystemAccentColor or the variants in the Application level resource dictionary.
    /// </remarks>
    public Color? CustomAccentColor
    {
        get => _customAccentColor;
        set
        {
            if (_customAccentColor != value)
            {
                _customAccentColor = value;
                if (_hasLoaded) LoadCustomAccentColor();
            }
        }
    }

    public AvaloniaList<IResourceDictionary> MergedDictionaries { get; }

    bool IResourceNode.HasResources => true;

    bool IResourceNode.TryGetResource(object key, ThemeVariant theme, out object value) =>
        TryGetResource(key, theme, out value);

    /// <inheritdoc />
    public new bool TryGetResource(object key, ThemeVariant theme, out object value)
    {
        // Github build failing with this not being set, even tho it passes locally
        value = null;

        // We also search the app level resources so resources can be overridden.
        // Do not search App level styles though as we'll have to iterate over them
        // to skip the FluentAvaloniaTheme instance or we'll stack overflow
        if (Application.Current?.Resources.TryGetResource(key, theme, out value) == true)
            return true;

        if (base.TryGetResource(key, theme, out value) == true)
            return true;

        value = null;
        return false;
    }

    private void Init()
    {
        AvaloniaXamlLoader.Load(this);

        // First load our base and theme resources

        // When initializing, UseSystemTheme overrides any setting of RequestedTheme, this must be
        // explicitly disabled to enable setting the theme manually
        ResolveThemeAndInitializeSystemResources();

        if (OperatingSystem.IsWindows())
            // Load this in all cases since with ThemeDictionaries, we always have a ref to the 
            // HighContrast dictionary
            TryLoadHighContrastThemeColors();

        _hasLoaded = true;
    }

    private void ResolveThemeAndInitializeSystemResources()
    {
        ThemeVariant theme = null;

        // PlatformSettings on the Application should be immutable so we can store them here
        if (_platformSettings == null)
        {
            _platformSettings = Application.Current.PlatformSettings;
            _platformSettings.ColorValuesChanged += OnPlatformColorValuesChanged;
        }

        if (OperatingSystem.IsWindows())
        {
            theme = ResolveWindowsSystemSettings(_platformSettings);
        }
        else if (OperatingSystem.IsLinux())
        {
            theme = ResolveLinuxSystemSettings(_platformSettings);
        }
        else if (OperatingSystem.IsMacOS())
        {
            theme = ResolveMacOSSystemSettings(_platformSettings);
        }
        else
        {
            // WASM & Mobile

            // Don't read from PlatformSettings if PreferSystemTheme = false, Issue #497
            if (PreferSystemTheme)
                theme = GetThemeFromIPlatformSettings(_platformSettings);

            // MacOS logic is also used for WASM/Mobile since it just pulls from 
            // IPlatformSettings Color Values
            TryLoadMacOSAccentColor(_platformSettings);

            AddOrUpdateSystemResource("ContentControlThemeFontFamily", FontFamily.Default);
        }

        // The Resolve...Settings will return null if PreferSystemTheme is false
        if (theme != null) Application.Current.RequestedThemeVariant = theme;
    }

    private void OnPlatformColorValuesChanged(object sender, PlatformColorValues e)
    {
        if (OperatingSystem.IsWindows()) TryLoadHighContrastThemeColors();

        if (PreferSystemTheme)
        {
            ThemeVariant theme;
            if (e.ContrastPreference == ColorContrastPreference.High)
                theme = HighContrastTheme;
            else
                theme = e.ThemeVariant == PlatformThemeVariant.Light ? ThemeVariant.Light : ThemeVariant.Dark;

            Application.Current.RequestedThemeVariant = theme;
        }

        if (!CustomAccentColor.HasValue && PreferUserAccentColor)
        {
            if (OperatingSystem.IsWindows())
                TryLoadWindowsAccentColor();
            else if (OperatingSystem.IsMacOS())
                TryLoadMacOSAccentColor(_platformSettings);
            else if (OperatingSystem.IsLinux()) TryLoadLinuxAccentColor();
        }
    }

    private ThemeVariant ResolveMacOSSystemSettings(IPlatformSettings platformSettings)
    {
        ThemeVariant theme = null;
        if (PreferSystemTheme) theme = GetThemeFromIPlatformSettings(platformSettings);

        if (CustomAccentColor != null)
            LoadCustomAccentColor();
        else if (PreferUserAccentColor)
            TryLoadMacOSAccentColor(platformSettings);
        else
            LoadDefaultAccentColor();

        AddOrUpdateSystemResource("ContentControlThemeFontFamily", FontFamily.Default);

        return theme;
    }

    private ThemeVariant ResolveLinuxSystemSettings(IPlatformSettings platformSettings)
    {
        ThemeVariant theme = null;
        if (PreferSystemTheme)
        {
            // See TryLoadLinuxAccentColor() for note on what Avalonia IPlatformSettings supports
            // on Linux. We'll try the existing logic first before attempting IPlatformSettings
            var resolvedTheme = LinuxThemeResolver.TryLoadSystemTheme();
            if (resolvedTheme != null)
                theme = resolvedTheme;
            else
                theme = GetThemeFromIPlatformSettings(platformSettings);
        }

        if (CustomAccentColor != null)
            LoadCustomAccentColor();
        else if (PreferUserAccentColor)
            TryLoadLinuxAccentColor();
        else
            LoadDefaultAccentColor();

        AddOrUpdateSystemResource("ContentControlThemeFontFamily", FontFamily.Default);

        return theme;
    }

    private static ThemeVariant GetThemeFromIPlatformSettings(IPlatformSettings platformSettings)
    {
        var platformColors = platformSettings.GetColorValues();
        var isSystemInHighContrast = platformColors.ContrastPreference == ColorContrastPreference.High;
        if (!isSystemInHighContrast)
            return platformColors.ThemeVariant == PlatformThemeVariant.Light ? ThemeVariant.Light : ThemeVariant.Dark;

        return HighContrastTheme;
    }

    private void LoadCustomAccentColor()
    {
        if (!_customAccentColor.HasValue)
        {
            if (PreferUserAccentColor)
            {
                if (OperatingSystem.IsWindows())
                    TryLoadWindowsAccentColor();
                else if (OperatingSystem.IsLinux())
                    TryLoadLinuxAccentColor();
                else // Mac & WASM/Mobile
                    TryLoadMacOSAccentColor(_platformSettings);
            }
            else
            {
                LoadDefaultAccentColor();
            }

            return;
        }

        Color2 col = _customAccentColor.Value;

        UpdateAccentColors(_customAccentColor.Value,
            col.LightenPercent(0.15f),
            col.LightenPercent(0.30f),
            col.LightenPercent(0.45f),
            col.LightenPercent(-0.15f),
            col.LightenPercent(-0.30f),
            col.LightenPercent(-0.45f));
    }

    private void TryLoadMacOSAccentColor(IPlatformSettings platformSettings)
    {
        try
        {
            // Replaced old logic with PlatformSettings from Avalonia
            Color2 aColor = platformSettings.GetColorValues().AccentColor1;

            UpdateAccentColors(aColor,
                aColor.LightenPercent(0.15f),
                aColor.LightenPercent(0.30f),
                aColor.LightenPercent(0.45f),
                aColor.LightenPercent(-0.15f),
                aColor.LightenPercent(-0.30f),
                aColor.LightenPercent(-0.45f));
        }
        catch
        {
            LoadDefaultAccentColor();
        }
    }

    private void TryLoadLinuxAccentColor()
    {
        // Per GH#9913:
        // Only works if distro implements newest (~2021) standard of FreeDesktop. GTK and others specific settings are ignored.
        // Accent colors are not supported, and frame theme isn't changeable from the app (not sure if it's possible, if anybody wants to help - please do).
        // No high contrast support.
        // So we'll keep the existing logic here

        var aColor = LinuxThemeResolver.TryLoadAccentColor();
        if (aColor != null)
        {
            var col = aColor.Value;

            UpdateAccentColors(col,
                col.LightenPercent(0.15f),
                col.LightenPercent(0.30f),
                col.LightenPercent(0.45f),
                col.LightenPercent(-0.15f),
                col.LightenPercent(-0.30f),
                col.LightenPercent(-0.45f));
        }
        else
        {
            LoadDefaultAccentColor();
        }
    }

    private void LoadDefaultAccentColor()
    {
        UpdateAccentColors(Colors.SlateBlue,
            Color.Parse("#7F69FF"),
            Color.Parse("#9B8AFF"),
            Color.Parse("#B9ADFF"),
            Color.Parse("#43339C"),
            Color.Parse("#33238C"),
            Color.Parse("#1D115C"));
    }

    private void AddOrUpdateSystemResource(object key, object value)
    {
        Resources[key] = value;
    }

    private void UpdateAccentColors(Color accent,
        Color light1, Color light2, Color light3,
        Color dark1, Color dark2, Color dark3)
    {
        if (_accentColorsDictionary != null)
            Resources.MergedDictionaries.Remove(_accentColorsDictionary);

        _accentColorsDictionary = new ResourceDictionary
        {
            { "SystemAccentColor", accent },
            { "SystemAccentColorLight1", light1 },
            { "SystemAccentColorLight2", light2 },
            { "SystemAccentColorLight3", light3 },
            { "SystemAccentColorDark1", dark1 },
            { "SystemAccentColorDark2", dark2 },
            { "SystemAccentColorDark3", dark3 }
        };

        Resources.MergedDictionaries.Add(_accentColorsDictionary);
    }

    private void MergedDictionariesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (IResourceDictionary item in e.OldItems)
                Resources.MergedDictionaries.Remove(item);

        if (e.NewItems != null)
            foreach (IResourceDictionary item in e.NewItems)
                Resources.MergedDictionaries.Add(item);
    }
}