import { Box } from "@mui/material";

// Original artwork stays unobscured and vivid in both themes.
export default function LoginVisual() {
  return <Box className="login-scene" aria-hidden="true">
    <Box component="img" className="login-illustration" src="/images/login-neon-portal.png" alt="" />
  </Box>;
}
