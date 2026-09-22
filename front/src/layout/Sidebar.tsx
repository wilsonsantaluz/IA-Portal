import React from "react";
import {
  Box,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Tooltip,
  styled,
} from "@mui/material";

import ChatIcon from "@mui/icons-material/Chat";
import DashboardIcon from "@mui/icons-material/Dashboard";
import LibraryBooksIcon from "@mui/icons-material/LibraryBooks";
import MenuIcon from "@mui/icons-material/Menu";
import { useTheme } from "@mui/material/styles";

type SidebarProps = {
  open: boolean;
  setOpen: (v: boolean) => void;
};

const EXPANDED_WIDTH = 240;
const COLLAPSED_WIDTH = 64;
const HOVER_WIDTH = 220;

const SidebarContainer = styled(Box, {
  shouldForwardProp: (prop) => prop !== "open",
})<{ open: boolean }>(({ open, theme }) => {
  const isDark = theme.palette.mode === "dark";

  return {
    position: "fixed",
    left: 0,
    top: 0,
    height: "100vh",
    width: open ? EXPANDED_WIDTH : COLLAPSED_WIDTH,

    /* 🔑 AQUI ESTÁ A CORREÇÃO */
    backgroundColor: isDark
      ? theme.palette.background.paper
      : "#0a2240",

    color: isDark
      ? theme.palette.text.primary
      : "#ffffff",

    boxShadow: theme.shadows[8],

    transition: "width 0.25s ease",
    overflow: "hidden",
    zIndex: 1200,
    display: "flex",
    flexDirection: "column",

    ...(open
      ? {}
      : {
          "&:hover": {
            width: HOVER_WIDTH,
          },
        }),
  };
});

const ItemText = styled(ListItemText, {
  shouldForwardProp: (prop) => prop !== "open",
})<{ open: boolean }>(({ open }) => ({
  opacity: open ? 1 : 0,
  transition: "opacity 0.2s ease",
  whiteSpace: "nowrap",
  marginLeft: -10,
}));

const SidebarRoot = styled(Box)({
  "&:hover .item-text": {
    opacity: 1,
  },
});

export default function Sidebar({ open, setOpen }: SidebarProps) {
  return (
    <SidebarContainer open={open}>
      <SidebarRoot sx={{ mt: 1 }}>
        <List>
          <ListItemButton sx={{ py: 2 }} onClick={() => setOpen(!open)}>
            <ListItemIcon sx={{ color: "white" }}>
              <MenuIcon />
            </ListItemIcon>
            <ItemText open={open} className="item-text" primary="Menu" />
          </ListItemButton>

          <Tooltip title="Chat IA" placement="right" disableHoverListener={open}>
            <ListItemButton component="a" href="/chat" sx={{ py: 2 }}>
              <ListItemIcon sx={{ color: "white" }}>
                <ChatIcon />
              </ListItemIcon>
              <ItemText open={open} className="item-text" primary="Chat IA" />
            </ListItemButton>
          </Tooltip>

          <Tooltip title="Dashboard" placement="right" disableHoverListener={open}>
            <ListItemButton component="a" href="/dashboard" sx={{ py: 2 }}>
              <ListItemIcon sx={{ color: "white" }}>
                <DashboardIcon />
              </ListItemIcon>
              <ItemText open={open} className="item-text" primary="Dashboard" />
            </ListItemButton>
          </Tooltip>

          <Tooltip
            title="Base de Conhecimento"
            placement="right"
            disableHoverListener={open}
          >
            <ListItemButton component="a" href="/kb" sx={{ py: 2 }}>
              <ListItemIcon sx={{ color: "white" }}>
                <LibraryBooksIcon />
              </ListItemIcon>
              <ItemText
                open={open}
                className="item-text"
                primary="Base Conhecimento"
              />
            </ListItemButton>
          </Tooltip>
        </List>
      </SidebarRoot>
    </SidebarContainer>
  );
}
