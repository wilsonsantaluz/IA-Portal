import { Box } from "@mui/material";
import { usePortalTheme } from "../theme/PortalThemeProvider";

export default function Brand({ compact = false }: { compact?: boolean }) {
  const { mode } = usePortalTheme();
  const source = compact
    ? "/brand/ia-portal-mark.svg"
    : mode === "dark"
      ? "/brand/ia-portal-logo-dark.svg"
      : "/brand/ia-portal-logo.svg";

  return (
    <Box
      component="img"
      src={source}
      alt="IA-Portal"
      width={compact ? 40 : 190}
      height={compact ? 34 : 50}
      sx={{ display: "block", objectFit: "contain", flexShrink: 0, maxWidth: "100%" }}
    />
  );
}
