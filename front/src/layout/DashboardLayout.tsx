import React, { useEffect, useState } from "react";
import { Box, CssBaseline, ThemeProvider } from "@mui/material";
import { Outlet } from "react-router-dom";

import Sidebar from "./Sidebar";
import HeaderBar from "./HeaderBar";

import { ChatUIProvider } from "../context/ChatUIContext";
import { lightTheme } from "../theme/lightTheme";
import { darkTheme } from "../theme/darkTheme";

type ThemeMode = "light" | "dark";

export default function DashboardLayout({ children }: any) {
  const [sidebarOpen, setSidebarOpen] = useState(false);

  /**
   * 🔑 Fonte da verdade do tema
   * Usa localStorage (compatível com teu toggle atual)
   */
  const [mode, setMode] = useState<ThemeMode>("dark");

  /**
   * Carrega tema salvo
   */
  useEffect(() => {
    const savedTheme = localStorage.getItem("theme-mode") as ThemeMode | null;
    if (savedTheme === "light" || savedTheme === "dark") {
      setMode(savedTheme);
    }
  }, []);

  /**
   * Mantém sincronizado
   */
  useEffect(() => {
    const syncTheme = () => {
      const saved = localStorage.getItem("theme-mode") as ThemeMode | null;
      if (saved === "light" || saved === "dark") {
        setMode(saved);
      }
    };
 
  
    syncTheme();
  
    window.addEventListener("storage", syncTheme);
    return () => window.removeEventListener("storage", syncTheme);
  }, []);
  useEffect(() => {
    document.body.dataset.theme = mode;
  }, [mode]);
  /**
   * Tema efetivo
   */
  const theme = mode === "dark" ? darkTheme : lightTheme;

  return (
    <ThemeProvider theme={theme}>
      <ChatUIProvider>
        <CssBaseline />

        <Box sx={{ display: "flex", height: "100vh", overflow: "hidden" }}>
          {/* Sidebar */}
          <Sidebar open={sidebarOpen} setOpen={setSidebarOpen} />

          {/* Main */}
          <Box
            component="main"
            sx={{
              flex: 1,
              display: "flex",
              flexDirection: "column",
              overflow: "hidden",
              ml: sidebarOpen ? "240px" : "64px",
              transition: "margin-left .25s ease",
            }}
          >
            {/* Header */}
            <HeaderBar
              open={sidebarOpen}
              setOpen={setSidebarOpen}
              // 👇 opcional, se quiser ligar direto no toggle depois
              // themeMode={mode}
              // setThemeMode={setMode}
            />

            {/* Conteúdo */}
            <Box
              sx={{
                flex: 1,
                overflow: "hidden",
                pt: "64px",
              }}
            >
              <Outlet />
              {children}
            </Box>
          </Box>
        </Box>
      </ChatUIProvider>
    </ThemeProvider>
  );
}
