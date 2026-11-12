import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import {
  Box,
  Button,
  TextField,
  Typography,
  Paper,
  Alert,
  Stack,
  IconButton,
  InputAdornment,
} from "@mui/material";
import AutoAwesomeOutlined from "@mui/icons-material/AutoAwesomeOutlined";
import ArrowForwardRounded from "@mui/icons-material/ArrowForwardRounded";
import DescriptionOutlined from "@mui/icons-material/DescriptionOutlined";
import ForumOutlined from "@mui/icons-material/ForumOutlined";
import InsightsOutlined from "@mui/icons-material/InsightsOutlined";
import { useAuth } from "../auth/AuthProvider";
import { useNavigate, useLocation } from "react-router-dom";
import axios from "axios";
import { Link } from "react-router-dom";
import Brand from "../components/Brand";
import ThemeToggle from "../components/ThemeToggle";
import LoginVisual from "../components/LoginVisual";
import VisibilityOutlined from "@mui/icons-material/VisibilityOutlined";
import VisibilityOffOutlined from "@mui/icons-material/VisibilityOffOutlined";
import "./login.css";
export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [signupEnabled, setSignupEnabled] = useState(false);
  useEffect(() => { axios.get((import.meta.env.VITE_API_URL || "/").replace(/\/?$/, "/") + "api/auth/signup/status").then(r => setSignupEnabled(r.data.enabled)).catch(() => {}); }, []);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await login(email, password);
      const from = (location.state as { from?: { pathname?: string; hash?: string } } | null)?.from;
      const hash = from?.pathname === "/invite" ? from.hash : location.hash;
      // Only the exact local invitation route may be resumed; no arbitrary return URL.
      navigate(/^#invite=[a-fA-F0-9]{64}$/.test(hash || "") ? `/invite${hash}` : "/", { replace: true });
    } catch {
      setError("Usuário ou senha inválidos");
    } finally {
      setLoading(false);
    }
  }
  return (
    <Box
      className="login-shell"
      sx={{
        minHeight: "100dvh",
        display: "grid",
        gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" },
      }}
    >
      <Box
        className="login-story"
        sx={{
          display: { xs: "none", md: "flex" },
          p: { md: 4, lg: 5 },
          flexDirection: "column",
          justifyContent: "space-between",
          position: "relative",
          overflow: "hidden",
          borderRight: "1px solid",
          borderColor: "divider",
        }}
      >
        <Brand />
        <LoginVisual />
        <Box className="portal-page login-story-card" sx={{ my: 3, maxWidth: 520, p: { md: 3, lg: 3.5 } }}>
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
              mb: 2,
            }}
          >
            <AutoAwesomeOutlined />
          </Box>
          <Typography
            variant="h4"
            fontSize={{ md: 32, lg: 40 }}
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
          <Stack spacing={1} mt={3}>
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
                className="login-feature"
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
        className="login-form-panel"
        sx={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          p: { xs: 2, sm: 4, md: 5 },
          position: "relative",
        }}
      >
        <Box sx={{ position: "absolute", top: 20, right: 20 }}>
          <ThemeToggle />
        </Box>
        <Paper
          className="portal-page login-form-card"
          sx={{ width: "100%", maxWidth: 460, px: { xs: 3, sm: 4 }, py: { xs: 4, sm: 4.5 }, my: { xs: 7, md: 4 } }}
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
          <Alert severity="info" sx={{ mb: 2 }}>
            O acesso a esta demonstração é gratuito. O chat está sujeito aos
            limites de uso dos provedores de IA. Se houver indisponibilidade,
            tente novamente mais tarde.
          </Alert>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}
          {signupEnabled && <Button component={Link} to="/signup" sx={{ mb: 2 }}>Criar conta no IA-Portal</Button>}
          <Box component="form" onSubmit={handleSubmit}>
            <TextField
              fullWidth
              label="E-mail"
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
              type={showPassword ? "text" : "password"}
              InputProps={{ endAdornment: <InputAdornment position="end"><IconButton aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"} onClick={() => setShowPassword(value => !value)} edge="end">{showPassword ? <VisibilityOffOutlined /> : <VisibilityOutlined />}</IconButton></InputAdornment> }}
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
