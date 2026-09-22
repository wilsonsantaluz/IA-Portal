import { createTheme } from "@mui/material/styles";

export const theme = createTheme({
  palette: {
    mode: "light",
    primary: {
      main: "#4C6FFF",
    },
    secondary: {
      main: "#7B61FF",
    },
    background: {
      default: "#f5f6fa",
      paper: "#ffffff",
    },
    text: {
      primary: "#1d1d1d",
    },
  },
  typography: {
    fontFamily: "Inter, Roboto, sans-serif",
  },
  shape: {
    borderRadius: 12,
  },
});
