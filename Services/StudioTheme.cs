using MudBlazor;

namespace Pixcii.Services;

public static class StudioTheme
{
    public static readonly MudTheme RetroDark = new()
    {
        PaletteDark = new PaletteDark
        {
            // Single restrained warm amber accent
            Primary = "#E5A030",
            PrimaryDarken = "#C98922",
            PrimaryLighten = "#F0B24A",
            PrimaryContrastText = "#14110B",

            Secondary = "#E5A030",
            SecondaryDarken = "#C98922",
            SecondaryLighten = "#F0B24A",
            SecondaryContrastText = "#14110B",

            Tertiary = "#E5A030",
            TertiaryContrastText = "#14110B",

            Info = "#96928B",
            Success = "#E5A030",
            Warning = "#E5A030",
            Error = "#C95A52",

            // Neutral charcoal surfaces & warm off-white text
            Background = "#141414",
            BackgroundGray = "#1B1B1B",
            Surface = "#222222",
            DrawerBackground = "#1B1B1B",
            DrawerText = "#EAE6DF",
            DrawerIcon = "#96928B",
            AppbarBackground = "#1B1B1B",
            AppbarText = "#EAE6DF",
            LinesDefault = "#2E2E2E",
            LinesInputs = "#3D3D3D",
            Divider = "#2E2E2E",
            TextPrimary = "#EAE6DF",
            TextSecondary = "#96928B",
            TextDisabled = "#5A5752",
            ActionDefault = "#96928B",
            ActionDisabled = "#5A5752"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "3px",
            DrawerWidthLeft = "240px",
            AppbarHeight = "48px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Segoe UI", "system-ui", "sans-serif"],
                FontSize = "0.75rem", // 12px
                LineHeight = "1.4",
                LetterSpacing = "0"
            },
            H6 = new H6Typography
            {
                FontFamily = ["Inter", "Segoe UI", "system-ui", "sans-serif"],
                FontSize = "0.875rem", // 14px
                FontWeight = "600",
                LineHeight = "1.35",
                LetterSpacing = "0"
            },
            Button = new ButtonTypography
            {
                FontFamily = ["Inter", "Segoe UI", "system-ui", "sans-serif"],
                TextTransform = "none",
                FontWeight = "500",
                FontSize = "0.75rem", // 12px
                LetterSpacing = "0"
            }
        }
    };
}
