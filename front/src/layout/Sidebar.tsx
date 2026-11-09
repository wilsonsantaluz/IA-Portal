import StorageOutlined from "@mui/icons-material/StorageOutlined";
import {
  Box,
  Divider,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Tooltip,
  Typography,
} from "@mui/material";
import ChatOutlined from "@mui/icons-material/ChatOutlined";
import InsightsOutlined from "@mui/icons-material/InsightsOutlined";
import LibraryBooksOutlined from "@mui/icons-material/LibraryBooksOutlined";
import { alpha } from "@mui/material/styles";
import { Link, useLocation } from "react-router-dom";
import Brand from "../components/Brand";
import { useAuth } from "../auth/AuthProvider";
import PeopleOutline from "@mui/icons-material/PeopleOutline";
import AdminPanelSettingsOutlined from "@mui/icons-material/AdminPanelSettingsOutlined";
import SettingsOutlined from "@mui/icons-material/SettingsOutlined";
import DataUsageOutlined from "@mui/icons-material/DataUsageOutlined";
import type { ReactNode } from "react";

const destinations: { to: string; label: string; icon: ReactNode; permission: string; alternatePermission?: string }[] = [
  { to: "/organizations", label: "Organizações", icon: <PeopleOutline />, permission: "" },
  {
    to: "/chat",
    label: "Chat IA",
    icon: <ChatOutlined />,
    permission: "chat.access",
  },
  {
    to: "/dashboard",
    label: "Dashboard",
    icon: <InsightsOutlined />,
    permission: "dashboard.view",
  },
  {
    to: "/kb",
    label: "Base de conhecimento",
    icon: <LibraryBooksOutlined />,
    permission: "kb.view",
  },
  { to: "/channels", label: "Canais", icon: <ChatOutlined />, permission: "channels.manage" },
  { to: "/data-connectors", label: "Data Connectors", icon: <StorageOutlined />, permission: "connectors.manage" },
  { to: "/usage", label: "Consumo de IA", icon: <DataUsageOutlined />, permission: "chat.access", alternatePermission: "usage.admin" },
  {
    to: "/users",
    label: "Usuários",
    icon: <PeopleOutline />,
    permission: "access.manage",
  },
  {
    to: "/groups",
    label: "Grupos e permissões",
    icon: <AdminPanelSettingsOutlined />,
    permission: "access.manage",
  },
  { to: "/settings/ai", label: "Inteligência Artificial", icon: <SettingsOutlined />, permission: "chat.access", alternatePermission: "ai.manage" },
];

export default function Sidebar({
  open,
  onNavigate,
}: {
  open: boolean;
  onNavigate: () => void;
}) {
  const { pathname } = useLocation();
  const { hasPermission } = useAuth();
  return (
    <Box
      component="nav"
      aria-label="Navegação principal"
      sx={{
        height: "100%",
        display: "flex",
        flexDirection: "column",
        bgcolor: "var(--portal-sidebar)",
        color: "text.primary",
        overflow: "auto",
      }}
    >
      <Box
        component={Link}
        to="/"
        aria-label="IA-Portal: início"
        onClick={onNavigate}
        sx={{
          height: 72,
          px: open ? 3 : 2.5,
          display: "flex",
          alignItems: "center",
          textDecoration: "none",
          flexShrink: 0,
        }}
      >
        <Brand compact={!open} />
      </Box>
      <Divider sx={{ mx: 2 }} />
      <Box sx={{ px: 1.5, mt: 3 }}>
        {open && (
          <Typography
            sx={{
              px: 1.5,
              pb: 1.2,
              fontSize: 10,
              fontWeight: 700,
              letterSpacing: ".12em",
              color: "text.secondary",
            }}
          >
            ESPAÇO DE TRABALHO
          </Typography>
        )}
        <List component="div" disablePadding>
          {destinations
            .filter(item => import.meta.env.VITE_MINIMAL_HOSTING !== "true" || item.to !== "/data-connectors")
            .filter((item) => item.permission === "" || hasPermission(item.permission) || !!item.alternatePermission && hasPermission(item.alternatePermission))
            .map((item) => (
              <Tooltip
                key={item.to}
                title={open ? "" : item.label}
                placement="right"
              >
                <ListItemButton
                  component={Link}
                  to={item.to}
                  aria-label={item.label}
                  aria-current={pathname === item.to ? "page" : undefined}
                  onClick={onNavigate}
                  selected={pathname === item.to}
                  sx={(theme) => ({
                    position: "relative",
                    borderRadius: "10px",
                    mb: 0.7,
                    minHeight: 46,
                    px: 1.6,
                    transition: "background .2s, box-shadow .2s",
                    color: theme.palette.text.primary,
                    "&.Mui-selected": {
                      color: theme.palette.primary.main,
                      background: `linear-gradient(100deg, ${alpha(theme.palette.primary.main, 0.14)}, ${alpha(theme.palette.secondary.main, 0.06)})`,
                      boxShadow: `inset 3px 0 0 ${theme.palette.primary.main}`,
                    },
                    "&.Mui-selected .MuiListItemIcon-root": {
                      color: theme.palette.primary.main,
                    },
                  })}
                >
                  <ListItemIcon
                    sx={{ minWidth: open ? 37 : 0, color: "text.secondary" }}
                  >
                    {item.icon}
                  </ListItemIcon>
                  {open && (
                    <ListItemText
                      primary={item.label}
                      primaryTypographyProps={{
                        fontSize: 13,
                        fontWeight: pathname === item.to ? 650 : 500,
                      }}
                    />
                  )}
                </ListItemButton>
              </Tooltip>
            ))}
        </List>
      </Box>
      {open && (
        <Box sx={{ mx: 2.5, mt: "auto", pt: 5, pb: 3 }}>
          <Box
            sx={{
              border: "1px solid",
              borderColor: "divider",
              borderRadius: "14px",
              p: 2,
              background:
                "linear-gradient(135deg, color-mix(in srgb, var(--portal-primary) 6%, transparent), color-mix(in srgb, var(--portal-cyan) 4%, transparent))",
            }}
          >
            <Typography fontSize={12} fontWeight={650}>
              Conhecimento em ação.
            </Typography>
            <Typography fontSize={11} color="text.secondary" mt={0.5}>
              Um espaço para conectar ideias e informações.
            </Typography>
          </Box>
        </Box>
      )}
    </Box>
  );
}
