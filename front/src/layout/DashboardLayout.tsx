import { useEffect, useState } from "react";
import { Box, Drawer, useMediaQuery, useTheme } from "@mui/material";
import { Outlet } from "react-router-dom";
import Sidebar from "./Sidebar";
import HeaderBar from "./HeaderBar";
import { ChatUIProvider } from "../context/ChatUIContext";

export default function DashboardLayout() {
  const desktop = useMediaQuery(useTheme().breakpoints.up("md"));
  const [expanded, setExpanded] = useState(true);
  const [mobileOpen, setMobileOpen] = useState(false);
  useEffect(() => {
    if (desktop) setMobileOpen(false);
  }, [desktop]);
  return (
    <ChatUIProvider>
      <Box sx={{ display: "flex", height: "100dvh", overflow: "hidden" }}>
        <a className="skip-link" href="#main-content">
          Ir para o conteúdo
        </a>
        {desktop ? (
          <Box
            component="aside"
            id="desktop-navigation"
            sx={{
              width: expanded ? 248 : 80,
              transition: "width .25s ease",
              flexShrink: 0,
              borderRight: "1px solid",
              borderColor: "divider",
              zIndex: 2,
            }}
          >
            <Sidebar open={expanded} onNavigate={() => {}} />
          </Box>
        ) : (
          <Drawer
            ModalProps={{ keepMounted: true }}
            open={mobileOpen}
            onClose={() => setMobileOpen(false)}
            PaperProps={{ id: "mobile-navigation", sx: { width: 272 } }}
          >
            <Sidebar open onNavigate={() => setMobileOpen(false)} />
          </Drawer>
        )}
        <Box
          sx={{
            display: "flex",
            flexDirection: "column",
            flex: 1,
            minWidth: 0,
          }}
        >
          <HeaderBar
            onMenu={() =>
              desktop ? setExpanded((v) => !v) : setMobileOpen((v) => !v)
            }
            menuExpanded={desktop ? expanded : mobileOpen}
            navigationId={desktop ? "desktop-navigation" : "mobile-navigation"}
          />
          <Box
            id="main-content"
            component="main"
            tabIndex={-1}
            sx={{
              flex: 1,
              minHeight: 0,
              overflow: "auto",
              background:
                "radial-gradient(ellipse at 95% 0%, color-mix(in srgb, var(--portal-cyan) 5%, transparent), transparent 45%), var(--portal-background)",
            }}
          >
            <Outlet />
          </Box>
        </Box>
      </Box>
    </ChatUIProvider>
  );
}
