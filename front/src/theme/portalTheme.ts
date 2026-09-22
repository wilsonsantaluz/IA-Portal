import { createTheme } from "@mui/material/styles";

export const portalTheme = createTheme({
  palette: {
    mode: "light",
    primary: {
      main: "#012155", // Azul padrão do header
      light: "#c2cf00",
      dark: "#c2cf00",
    },
    secondary: {
      main: "#012155", // Roxo
    },
    background: {
      default: "#F6F7FB",
      paper: "#FFFFFF",
    },
    success: { main: "#4CAF50" },
    warning: { main: "#FFB020" },
    error: { main: "#FF4C51" },
  },

  typography: {
    fontFamily: "Inter, Roboto, sans-serif",
    h1: { fontWeight: 700 },
    h2: { fontWeight: 700 },
    h3: { fontWeight: 700 },
  },

  shape: {
    borderRadius: 6,
  },

  shadows: Array(25).fill("0px 4px 20px rgba(0,0,0,0.05)") as any,

  components: {
    MuiCard: {
      styleOverrides: {
        root: {
          
          padding: "20px",
          background: "linear-gradient(145deg, #ffffff, #f4f5fa)",
          boxShadow: "0 4px 20px rgba(0,0,0,0.05)",
          transition: "all .3s ease",
        },
      },
    },

    MuiPaper: {
      styleOverrides: {
        root: {
          borderRadius: 6,
        },
      },
    },
  },
  
});
