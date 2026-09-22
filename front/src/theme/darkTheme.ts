import { createTheme } from "@mui/material/styles";

export const darkTheme = createTheme({
  palette: {
    mode: "dark",
    primary: { main: "#e5ecff" },
    secondary: { main: "#ffca28" },
    background: {
      default: "#0b1020",
      paper: "#151a2b"
    }
  },
  typography: {
    fontFamily: `"Inter", "Roboto", "Segoe UI", sans-serif`,
  }
});
