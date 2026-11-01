import { IconButton, Tooltip } from "@mui/material";
import LightModeIcon from "@mui/icons-material/LightMode";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import { usePortalTheme } from "../theme/PortalThemeProvider";
export default function ThemeToggle() {
  const { mode, toggle } = usePortalTheme();
  const label = mode === "dark" ? "Ativar tema claro" : "Ativar tema escuro";
  return <Tooltip title={label}><IconButton aria-label={label} onClick={toggle} color="inherit">
    {mode === "dark" ? <LightModeIcon fontSize="small" /> : <DarkModeIcon fontSize="small" />}
  </IconButton></Tooltip>;
}
