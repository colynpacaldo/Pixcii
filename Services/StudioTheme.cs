using MudBlazor;

namespace Pixcii.Services;

public static class StudioTheme
{
    public static readonly MudTheme RetroDark = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#22D3EE",
            PrimaryContrastText = "#091017",
            Secondary = "#F59E0B",
            SecondaryContrastText = "#140D02",
            Tertiary = "#A78BFA",
            Success = "#10B981",
            Info = "#38BDF8",
            Warning = "#FBBF24",
            Error = "#F43F5E",
            Background = "#0B0D12",
            BackgroundGray = "#11141D",
            Surface = "#141824",
            DrawerBackground = "#10131C",
            DrawerText = "#CBD5E1",
            DrawerIcon = "#94A3B8",
            AppbarBackground = "#0E1118",
            AppbarText = "#F1F5F9",
            LinesDefault = "#232838",
            Divider = "#232838",
            TextPrimary = "#F1F5F9",
            TextSecondary = "#94A3B8",
            TextDisabled = "#475569",
            ActionDefault = "#94A3B8",
            ActionDisabled = "#475569"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
            DrawerWidthLeft = "240px",
            AppbarHeight = "52px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Segoe UI", "system-ui", "sans-serif"],
                FontSize = "0.875rem"
            },
            Button = new ButtonTypography
            {
                TextTransform = "none",
                FontWeight = "600",
                FontSize = "0.8125rem"
            }
        }
    };
}
