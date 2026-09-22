import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  IconButton,
  Button,
  Tooltip,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import UnfoldMoreIcon from "@mui/icons-material/UnfoldMore";
import LogoutIcon from "@mui/icons-material/Close";
import UnfoldLessIcon from "@mui/icons-material/UnfoldLess";
import ThemeToggle from "../components/ThemeToggle";
import { useChatUI } from "../context/ChatUIContext";
import React from "react";
import { useTheme } from "@mui/material/styles";
import { useAuth } from "../auth/AuthProvider";

export default function HeaderBar({ open, setOpen }: any) {
  const { hasAnyCollapsed, toggleExpandAll } = useChatUI();
  const { logout } = useAuth();
  const theme = useTheme();
  const isDark = theme.palette.mode === "dark";
  return (
    <AppBar position="fixed" color="default" elevation={2} sx={{ zIndex: 1201 }}>
      <Toolbar sx={{ justifyContent: "space-between" }}>
        <Box display="flex" alignItems="center">
          <IconButton onClick={() => setOpen(!open)} sx={{ mr: 2 }}>
            <MenuIcon />
          </IconButton>


          <Typography variant="h6" color="primary">
            Portal de Conhecimento IA
          </Typography>
        </Box>

        <Box display="flex" alignItems="center" gap={2}>
          <Tooltip
            title={
              hasAnyCollapsed
                ? "Expandir todas as respostas da IA"
                : "Recolher todas as respostas da IA"
            }
          >
            <Button
              size="small"
              variant="outlined"
              startIcon={
                hasAnyCollapsed ? <UnfoldMoreIcon /> : <UnfoldLessIcon />
              }
              onClick={toggleExpandAll}
            >
              {hasAnyCollapsed ? "Expandir tudo" : "Recolher tudo"}
            </Button>
          </Tooltip>

          <Button
            color="inherit"
            startIcon={<LogoutIcon />}
            onClick={logout}
          >
            Sair
          </Button>


          <ThemeToggle />
        </Box>
      </Toolbar>
    </AppBar>
  );
}
