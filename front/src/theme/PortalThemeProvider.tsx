import { createContext, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useState, ReactNode } from "react";
import { CssBaseline, ThemeProvider } from "@mui/material";
import { createPortalTheme } from "./portalTheme";

type Mode = "light" | "dark";
const preferenceKey = "theme-mode";
const ThemeContext = createContext<{ mode: Mode; toggle: () => void } | null>(null);

function readPreference(): Mode {
  try { return localStorage.getItem(preferenceKey) === "dark" ? "dark" : "light"; }
  catch { return "light"; }
}

export function usePortalTheme() {
  const context = useContext(ThemeContext);
  if (!context) throw new Error("PortalThemeProvider não encontrado.");
  return context;
}

export function PortalThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setMode] = useState<Mode>(readPreference);
  const toggle = useCallback(() => setMode(current => current === "light" ? "dark" : "light"), []);

  useLayoutEffect(() => {
    document.documentElement.dataset.theme = mode;
    document.documentElement.style.colorScheme = mode;
    document.body.dataset.theme = mode;
    try { localStorage.setItem(preferenceKey, mode); } catch { /* Theme remains usable without storage. */ }
  }, [mode]);

  useEffect(() => {
    const synchronize = (event: StorageEvent) => {
      if (event.key === preferenceKey || event.key === null) setMode(readPreference());
    };
    window.addEventListener("storage", synchronize);
    return () => window.removeEventListener("storage", synchronize);
  }, []);

  const theme = useMemo(() => createPortalTheme(mode), [mode]);
  const value = useMemo(() => ({ mode, toggle }), [mode, toggle]);
  return <ThemeContext.Provider value={value}>
    <ThemeProvider theme={theme}><CssBaseline />{children}</ThemeProvider>
  </ThemeContext.Provider>;
}
