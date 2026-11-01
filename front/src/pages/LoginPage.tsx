import { useState } from "react";
import type { FormEvent } from "react";
import {
  Box,
  Button,
  TextField,
  Typography,
  Paper,
  Alert,
  Stack,
} from "@mui/material";
import AutoAwesomeOutlined from "@mui/icons-material/AutoAwesomeOutlined";
import ArrowForwardRounded from "@mui/icons-material/ArrowForwardRounded";
import DescriptionOutlined from "@mui/icons-material/DescriptionOutlined";
import ForumOutlined from "@mui/icons-material/ForumOutlined";
import InsightsOutlined from "@mui/icons-material/InsightsOutlined";
import { useAuth } from "../auth/AuthProvider";
import { useNavigate } from "react-router-dom";
import Brand from "../components/Brand";
import ThemeToggle from "../components/ThemeToggle";
export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await login(email, password);
      navigate("/chat");
    } catch {
      setError("Usuário ou senha inválidos");
    } finally {
      setLoading(false);
    }
  }
  return (
    <Box
      sx={{
        minHeight: "100dvh",
        display: "grid",
        gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" },
      }}
    >
      <Box
        sx={{
          display: { xs: "none", md: "flex" },
          p: { md: 5, lg: 8 },
          flexDirection: "column",
          justifyContent: "space-between",
          position: "relative",
          overflow: "hidden",
          bgcolor: "var(--portal-sidebar)",
          borderRight: "1px solid",
          borderColor: "divider",
          backgroundImage:
            "radial-gradient(ellipse at 20% 40%, color-mix(in srgb, var(--portal-primary) 13%, transparent), transparent 65%), radial-gradient(ellipse at 100% 80%, color-mix(in srgb, var(--portal-cyan) 9%, transparent), transparent 55%)",
        }}
      >
        <Brand />
        <Box className="portal-page" sx={{ my: 7, maxWidth: 480 }}>
          <Box
            sx={{
              display: "inline-flex",
              p: 1.5,
              border: "1px solid",
              borderColor: "divider",
              bgcolor: "background.paper",
              borderRadius: "16px",
              color: "primary.main",
              boxShadow: "var(--portal-shadow)",
              mb: 3,
            }}
          >
            <AutoAwesomeOutlined />
          </Box>
          <Typography
            variant="h4"
            fontSize={{ md: 38, lg: 48 }}
            lineHeight={1.15}
          >
            Seu conhecimento.
            <br />
            <Box component="span" color="primary.main">
              Novas possibilidades.
            </Box>
          </Typography>
          <Typography
            color="text.secondary"
            fontSize={15}
            lineHeight={1.8}
            mt={2.5}
          >
            Conecte ideias, explore documentos e transforme informação em ação
            com inteligência artificial.
          </Typography>
          <Stack spacing={2} mt={5}>
            {[
              { icon: <ForumOutlined />, label: "Conversas com mais contexto" },
              {
                icon: <DescriptionOutlined />,
                label: "Conhecimento em um só lugar",
              },
              { icon: <InsightsOutlined />, label: "Informações para ir além" },
            ].map((x) => (
              <Stack
                key={x.label}
                direction="row"
                alignItems="center"
                spacing={1.5}
              >
                <Box color="primary.main" display="flex">
                  {x.icon}
                </Box>
                <Typography fontSize={13}>{x.label}</Typography>
              </Stack>
            ))}
          </Stack>
        </Box>
        <Typography fontSize={11} color="text.secondary">
          IA-Portal · Conhecimento em ação
        </Typography>
      </Box>
      <Box
        sx={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          p: { xs: 3, md: 5 },
          position: "relative",
        }}
      >
        <Box sx={{ position: "absolute", top: 20, right: 20 }}>
          <ThemeToggle />
        </Box>
        <Paper
          className="portal-page"
          sx={{ width: "100%", maxWidth: 410, bgcolor: "transparent", py: 6 }}
        >
          <Box sx={{ display: { xs: "block", md: "none" }, mb: 5 }}>
            <Brand />
          </Box>
          <Typography
            variant="overline"
            fontWeight={700}
            color="primary.main"
            letterSpacing=".14em"
          >
            BEM-VINDO AO IA-PORTAL
          </Typography>
          <Typography variant="h4" fontSize={30} mt={1}>
            Acesse seu espaço
          </Typography>
          <Typography fontSize={14} color="text.secondary" mt={1} mb={3}>
            Entre com suas credenciais para continuar.
          </Typography>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}
          <Box component="form" onSubmit={handleSubmit}>
            <TextField
              fullWidth
              label="Email"
              type="email"
              autoComplete="username"
              margin="normal"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoFocus
              required
            />
            <TextField
              fullWidth
              label="Senha"
              type="password"
              autoComplete="current-password"
              margin="normal"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
            <Button
              fullWidth
              type="submit"
              variant="contained"
              size="large"
              endIcon={!loading && <ArrowForwardRounded />}
              sx={{ mt: 3, py: 1.6 }}
              disabled={loading}
            >
              {loading ? "Entrando..." : "Entrar"}
            </Button>
          </Box>
          <Typography
            fontSize={11}
            color="text.secondary"
            mt={4}
            textAlign="center"
          >
            Um espaço para conectar ideias e informações.
          </Typography>
        </Paper>
      </Box>
    </Box>
  );
}
