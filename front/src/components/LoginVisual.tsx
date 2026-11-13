import { Box } from "@mui/material";
import DescriptionOutlined from "@mui/icons-material/DescriptionOutlined";
import HubOutlined from "@mui/icons-material/HubOutlined";

// Original artwork stays unobscured and vivid in both themes.
export default function LoginVisual() {
  return <Box className="login-scene" aria-hidden="true">
    <Box component="img" className="login-illustration" src="/images/login-neon-portal.png" alt="" />
    <Box className="login-float login-float-document">
      <DescriptionOutlined /><span>Documentos<span className="login-float-caption">Informação conectada</span></span>
    </Box>
    <Box className="login-float login-float-knowledge">
      <HubOutlined /><span>Conhecimento<span className="login-float-caption">Novas ideias</span></span>
    </Box>
  </Box>;
}
