import { IconButton } from "@mui/material";
import LightModeIcon from "@mui/icons-material/LightMode";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import { useTheme } from "@mui/material/styles";
import React from "react";

export default function ThemeToggle() {
  const theme = useTheme();
  const dark = theme.palette.mode === "dark";

  const toggle = () => {
    const next = dark ? "light" : "dark";


    localStorage.setItem("theme-mode", next);


    window.dispatchEvent(new Event("storage"));
  };

  return (
    <IconButton onClick={toggle} color="inherit">
      {dark ? <LightModeIcon /> : <DarkModeIcon />}
    </IconButton>
  );
}
