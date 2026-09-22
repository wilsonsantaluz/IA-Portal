import { createTheme } from "@mui/material/styles";

export const lightTheme = createTheme({
  palette: {
    mode: "light",
    primary: { main: "#012155" },
    secondary: { main: "#BDC905" },
    background: {
      default: "#f7f9fc",
      paper: "#ffffff"
    }
  },
  typography: {
    fontFamily: `"Inter", "Roboto", "Segoe UI", sans-serif`,
  }
});
