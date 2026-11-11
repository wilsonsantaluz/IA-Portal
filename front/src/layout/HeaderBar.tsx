import { useEffect, useState } from "react";
import api from "../services/api";
import { switchOrganization } from "../auth/switchOrganization";
import {
  Box,
  Toolbar,
  IconButton,
  Typography,
  Tooltip,
  Avatar,
  Divider,
  Select,
  MenuItem,
  Alert,
} from "@mui/material";
import MenuOutlined from "@mui/icons-material/MenuOutlined";
import LogoutOutlined from "@mui/icons-material/LogoutOutlined";
import UnfoldLess from "@mui/icons-material/UnfoldLess";
import UnfoldMore from "@mui/icons-material/UnfoldMore";
import ThemeToggle from "../components/ThemeToggle";
import { useChatUI } from "../context/ChatUIContext";
import { useAuth } from "../auth/AuthProvider";
import { useLocation } from "react-router-dom";
import { alpha } from "@mui/material/styles";

type Props = {
  onMenu: () => void;
  menuExpanded: boolean;
  navigationId: string;
};
export default function HeaderBar({
  onMenu,
  menuExpanded,
  navigationId,
}: Props) {
  const { hasAnyCollapsed, toggleExpandAll } = useChatUI();
  const { user, logout } = useAuth();
  const { pathname } = useLocation();
  const [organizations, setOrganizations] = useState<{ id: number; name: string }[]>([]);
  const [organizationError, setOrganizationError] = useState(false);
  useEffect(() => {
    let active = true;
    const load = () => { void api.get("/api/organizations").then(({ data }) => { if (active) setOrganizations(data); }).catch(() => { if (active) setOrganizationError(true); }); };
    load(); window.addEventListener("organizations-changed", load);
    return () => { active = false; window.removeEventListener("organizations-changed", load); };
  }, []);
  const title =
    (
      {
        "/chat": "Chat IA",
        "/dashboard": "Dashboard",
        "/kb": "Base de conhecimento",
        "/users": "Usuários",
        "/groups": "Grupos e permissões",
        "/settings/ai": "Configurações · Inteligência Artificial",
        "/data-connectors": "Data Connectors",
        "/usage": "Consumo de Inteligência Artificial",
        "/channels": "IA-Portal Service",
        "/organizations": "Organizações",
        "/invite": "Convite de organização",
        "/audit": "Log de ações",
      } as Record<string, string>
    )[pathname] || "IA-Portal";
  return (
    <Box
      component="header"
      sx={(theme) => ({
        bgcolor: alpha(theme.palette.background.paper, 0.88),
        backdropFilter: "blur(16px)",
        borderBottom: "1px solid",
        borderColor: "divider",
        flexShrink: 0,
        zIndex: 1,
      })}
    >
      <Toolbar
        sx={{
          minHeight: "72px !important",
          gap: { xs: 0.5, sm: 1.5 },
          px: { xs: 1.5, sm: 3 },
        }}
      >
        <IconButton
          aria-label="Alternar menu de navegação"
          aria-expanded={menuExpanded}
          aria-controls={navigationId}
          onClick={onMenu}
        >
          <MenuOutlined />
        </IconButton>
        <Box flex={1} minWidth={0}>
          <Typography
            fontSize={10}
            letterSpacing=".05em"
            color="text.secondary"
            sx={{ display: { xs: "none", sm: "block" } }}
          >
            IA-PORTAL / ESPAÇO DE TRABALHO
          </Typography>
          <Typography
            component="h1"
            noWrap
            fontWeight={650}
            fontSize={{ xs: 14, md: 15 }}
          >
            {title}
          </Typography>
        </Box>
        {pathname === "/chat" && (
          <Tooltip
            title={
              hasAnyCollapsed ? "Expandir respostas" : "Recolher respostas"
            }
          >
            <IconButton
              aria-label={
                hasAnyCollapsed ? "Expandir respostas" : "Recolher respostas"
              }
              onClick={toggleExpandAll}
            >
              {hasAnyCollapsed ? (
                <UnfoldMore fontSize="small" />
              ) : (
                <UnfoldLess fontSize="small" />
              )}
            </IconButton>
          </Tooltip>
        )}
        {organizationError && <Alert severity="error">Erro ao carregar organizações</Alert>}
        {organizations.length > 0 && <Select size="small" value={user?.organizationId || organizations[0].id}
          inputProps={{ "aria-label": "Organização ativa" }} sx={{ maxWidth: { xs: 130, md: 250 } }}
          onChange={e => { void switchOrganization(Number(e.target.value)).catch(() => setOrganizationError(true)); }}>
          {organizations.map(o => <MenuItem key={o.id} value={o.id}>{o.name}</MenuItem>)}
        </Select>}
        <ThemeToggle />
        <Divider
          orientation="vertical"
          flexItem
          sx={{ my: 2, display: { xs: "none", sm: "block" } }}
        />
        <Avatar
          sx={{
            display: { xs: "none", sm: "flex" },
            width: 34,
            height: 34,
            fontSize: 13,
            background:
              "linear-gradient(135deg, var(--portal-primary), var(--portal-violet))",
            color: "#fff",
            boxShadow:
              "0 3px 10px color-mix(in srgb, var(--portal-primary) 22%, transparent)",
          }}
        >
          {user?.userName?.slice(0, 1).toUpperCase()}
        </Avatar>
        <Box sx={{ display: { xs: "none", lg: "block" }, maxWidth: 150 }}>
          <Typography fontSize={12} fontWeight={600} noWrap>
            {user?.userName}
          </Typography>
          <Typography fontSize={10} color="text.secondary" noWrap>
            {user?.email}
          </Typography>
        </Box>
        <Tooltip title="Sair">
          <IconButton aria-label="Sair" onClick={logout}>
            <LogoutOutlined fontSize="small" />
          </IconButton>
        </Tooltip>
      </Toolbar>
    </Box>
  );
}
