import { alpha, createTheme } from "@mui/material/styles";

// Provisional tokens from AGENTS.md; artwork is reviewed separately.
export const portalTokens = {
  light: {
    background: "#F5F8FC",
    sidebar: "#FFFFFF",
    surface: "#FFFFFF",
    text: "#14243D",
    muted: "#64748B",
    border: "#E4EAF2",
    primary: "#2563EB",
    cyan: "#06B6D4",
    violet: "#7C3AED",
  },
  dark: {
    background: "#0B1220",
    sidebar: "#0D1728",
    surface: "#132238",
    text: "#F1F5FC",
    muted: "#A5B4C8",
    border: "#293B55",
    primary: "#4D93FF",
    cyan: "#22D3EE",
    violet: "#9878FF",
  },
} as const;

export function createPortalTheme(mode: "light" | "dark") {
  const t = portalTokens[mode];
  return createTheme({
    palette: {
      mode,
      primary: {
        main: t.primary,
        contrastText: mode === "light" ? "#FFFFFF" : "#0B1220",
      },
      secondary: { main: t.cyan },
      info: { main: t.cyan },
      success: { main: mode === "light" ? "#059669" : "#34D399" },
      background: { default: t.background, paper: t.surface },
      text: { primary: t.text, secondary: t.muted },
      divider: t.border,
    },
    typography: {
      fontFamily: 'Inter, "Segoe UI", system-ui, sans-serif',
      h4: { fontWeight: 700, letterSpacing: "-.035em" },
      h5: { fontWeight: 700, letterSpacing: "-.025em" },
      h6: { fontWeight: 600 },
      button: { textTransform: "none", fontWeight: 600 },
    },
    shape: { borderRadius: 12 },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          ":root": {
            "--portal-sidebar": t.sidebar,
            "--portal-surface": t.surface,
            "--portal-background": t.background,
            "--portal-text": t.text,
            "--portal-muted": t.muted,
            "--portal-border": t.border,
            "--portal-primary": t.primary,
            "--portal-cyan": t.cyan,
            "--portal-violet": t.violet,
            "--portal-shadow":
              mode === "light"
                ? "0 4px 6px -4px rgba(20,36,61,.05), 0 16px 40px -20px rgba(20,36,61,.16)"
                : "0 4px 6px -4px rgba(0,0,0,.2), 0 16px 40px -20px rgba(0,0,0,.55)",
            "--portal-shadow-hover":
              mode === "light"
                ? "0 18px 40px -16px rgba(37,99,235,.25)"
                : "0 18px 40px -16px rgba(0,0,0,.7)",
          },
          body: { backgroundColor: t.background, color: t.text },
          ":focus-visible": {
            outline: `3px solid ${t.primary}`,
            outlineOffset: 3,
          },
        },
      },
      MuiButtonBase: {
        styleOverrides: {
          root: {
            "&:focus-visible": {
              outline: `3px solid ${t.primary}`,
              outlineOffset: 3,
            },
          },
        },
      },
      MuiPaper: {
        defaultProps: { elevation: 0 },
        styleOverrides: { root: { backgroundImage: "none" } },
      },
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: {
            borderRadius: 10,
            padding: "9px 18px",
            transition: "transform .2s, box-shadow .2s",
            "&:hover": { transform: "translateY(-1px)" },
          },
          containedPrimary: {
            backgroundImage: `linear-gradient(135deg, ${t.primary}, ${mode === "light" ? "#3B82F6" : "#60A5FA"})`,
            boxShadow: `0 5px 14px ${alpha(t.primary, 0.22)}`,
          },
        },
      },
      MuiIconButton: { styleOverrides: { root: { borderRadius: 10 } } },
      MuiCard: {
        styleOverrides: {
          root: {
            border: `1px solid ${t.border}`,
            boxShadow: "var(--portal-shadow)",
            borderRadius: 16,
          },
        },
      },
      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: 10,
            backgroundColor: alpha(t.background, 0.5),
            "& fieldset": { borderColor: t.border },
          },
        },
      },
      MuiTooltip: {
        styleOverrides: { tooltip: { borderRadius: 8, fontSize: 12 } },
      },
    },
  });
}

export const portalTheme = createPortalTheme("light");
