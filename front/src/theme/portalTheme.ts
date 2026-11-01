import { createTheme } from "@mui/material/styles";

// Provisional tokens from AGENTS.md; artwork is reviewed separately.
export const portalTokens = {
  light: {
    background: "#F5F8FC", sidebar: "#FFFFFF", surface: "#FFFFFF",
    text: "#14243D", muted: "#64748B", border: "#E4EAF2",
    primary: "#1769E0", cyan: "#06B6D4", violet: "#6847E8",
  },
  dark: {
    background: "#0B1220", sidebar: "#101B2E", surface: "#17243A",
    text: "#F1F5FC", muted: "#A5B4C8", border: "#293B55",
    primary: "#4D93FF", cyan: "#22D3EE", violet: "#9878FF",
  },
} as const;

export function createPortalTheme(mode: "light" | "dark") {
  const t = portalTokens[mode];
  return createTheme({
    palette: {
      mode,
      primary: { main: t.primary, contrastText: mode === "light" ? "#FFFFFF" : "#0B1220" },
      secondary: { main: t.cyan },
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
          },
          body: { backgroundColor: t.background, color: t.text },
          ":focus-visible": { outline: `3px solid ${t.primary}`, outlineOffset: 3 },
        },
      },
      MuiButtonBase: { styleOverrides: { root: { "&:focus-visible": { outline: `3px solid ${t.primary}`, outlineOffset: 3 } } } },
      MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { backgroundImage: "none" } } },
      MuiButton: { defaultProps: { disableElevation: true }, styleOverrides: { root: { borderRadius: 10 } } },
      MuiIconButton: { styleOverrides: { root: { borderRadius: 10 } } },
      MuiCard: { styleOverrides: { root: { border: `1px solid ${t.border}`, boxShadow: "none" } } },
    },
  });
}

export const portalTheme = createPortalTheme("light");
