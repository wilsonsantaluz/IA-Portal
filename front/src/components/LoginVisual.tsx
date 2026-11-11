import { Box } from "@mui/material";
import DescriptionOutlined from "@mui/icons-material/DescriptionOutlined";
import HubOutlined from "@mui/icons-material/HubOutlined";

// Decorative floating cards over the owner-provided background; no usage metrics.
export default function LoginVisual() {
  return <Box className="login-scene" aria-hidden="true">
    <Box className="login-float login-float-document"><DescriptionOutlined /><span>Documentos<Box className="login-document-lines"><i /><i /><i /></Box></span></Box>
    <Box className="login-float login-float-knowledge"><HubOutlined /><span>Conhecimento<br /><small>Ideias conectadas</small></span></Box>
  </Box>;
}
