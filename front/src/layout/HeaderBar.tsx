import { Box, Toolbar, IconButton, Typography, Tooltip, Avatar, Divider } from "@mui/material";
import MenuOutlined from "@mui/icons-material/MenuOutlined";
import LogoutOutlined from "@mui/icons-material/LogoutOutlined";
import UnfoldLess from "@mui/icons-material/UnfoldLess";
import UnfoldMore from "@mui/icons-material/UnfoldMore";
import ThemeToggle from "../components/ThemeToggle";
import { useChatUI } from "../context/ChatUIContext";
import { useAuth } from "../auth/AuthProvider";
import { useLocation } from "react-router-dom";

type Props = { onMenu: () => void; menuExpanded: boolean; navigationId: string };
export default function HeaderBar({ onMenu, menuExpanded, navigationId }: Props) {
  const { hasAnyCollapsed, toggleExpandAll } = useChatUI();
  const { user, logout } = useAuth();
  const { pathname } = useLocation();
  const title = ({ "/chat": "Chat IA", "/dashboard": "Dashboard", "/kb": "Base de conhecimento" } as Record<string, string>)[pathname] || "IA-Portal";
  return <Box component="header" sx={{ bgcolor: "background.paper", borderBottom: "1px solid", borderColor: "divider", flexShrink: 0 }}>
    <Toolbar sx={{ minHeight: "72px !important", gap: { xs: .5, sm: 1.5 }, px: { xs: 1.5, sm: 3 } }}>
      <IconButton aria-label="Alternar menu de navegação" aria-expanded={menuExpanded} aria-controls={navigationId} onClick={onMenu}><MenuOutlined /></IconButton>
      <Box flex={1} minWidth={0}>
        <Typography fontSize={10} letterSpacing=".05em" color="text.secondary" sx={{ display: { xs: "none", sm: "block" } }}>IA-PORTAL / ESPAÇO DE TRABALHO</Typography>
        <Typography component="h1" noWrap fontWeight={650} fontSize={{ xs: 14, md: 15 }}>{title}</Typography>
      </Box>
      {pathname === "/chat" && <Tooltip title={hasAnyCollapsed ? "Expandir respostas" : "Recolher respostas"}>
        <IconButton aria-label={hasAnyCollapsed ? "Expandir respostas" : "Recolher respostas"} onClick={toggleExpandAll}>
          {hasAnyCollapsed ? <UnfoldMore fontSize="small" /> : <UnfoldLess fontSize="small" />}
        </IconButton>
      </Tooltip>}
      <ThemeToggle />
      <Divider orientation="vertical" flexItem sx={{ my: 2, display: { xs: "none", sm: "block" } }} />
      <Avatar sx={{ display: { xs: "none", sm: "flex" }, width: 32, height: 32, fontSize: 13, bgcolor: "primary.main", color: "primary.contrastText" }}>{user?.userName?.slice(0, 1).toUpperCase()}</Avatar>
      <Typography fontSize={12} sx={{ display: { xs: "none", lg: "block" }, maxWidth: 130 }} noWrap>{user?.userName}</Typography>
      <Tooltip title="Sair"><IconButton aria-label="Sair" onClick={logout}><LogoutOutlined fontSize="small" /></IconButton></Tooltip>
    </Toolbar>
  </Box>;
}
