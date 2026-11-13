import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Box, Button, TextField, Typography, Paper, Alert, IconButton, InputAdornment } from "@mui/material";
import ArrowForwardRounded from "@mui/icons-material/ArrowForwardRounded";
import VisibilityOutlined from "@mui/icons-material/VisibilityOutlined";
import VisibilityOffOutlined from "@mui/icons-material/VisibilityOffOutlined";
import { Link, useNavigate, useLocation } from "react-router-dom";
import axios from "axios";
import { useAuth } from "../auth/AuthProvider";
import Brand from "../components/Brand";
import ThemeToggle from "../components/ThemeToggle";
import LoginVisual from "../components/LoginVisual";
import { portalTokens } from "../theme/portalTheme";
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
    <Box component="main" className="login-shell">
      <Box className="login-story" sx={{
        "--login-art-background": portalTokens.dark.background,
        "--login-art-text": portalTokens.dark.text,
        "--login-art-muted": portalTokens.dark.muted,
        "--login-art-accent": portalTokens.dark.cyan,
      }}>
        <Box component="img" src="/brand/ia-portal-logo-dark.svg" alt="IA-Portal" width={190} height={50} sx={{ objectFit: "contain" }} />
        <LoginVisual />
        <Box className="login-story-copy">
          <Typography component="p" className="login-story-title">
            Seu conhecimento.<br />
            <Box component="span">Novas possibilidades.</Box>
          </Typography>
          <Typography className="login-story-description">
            Conecte ideias e documentos com inteligência artificial.
          </Typography>
        </Box>
      </Box>
      <Box className="login-form-panel">
        <Box className="login-toolbar">
          <Box className="login-mobile-brand"><Brand /></Box>
          <ThemeToggle />
        </Box>
        <Paper component="section" className="login-form-card" aria-labelledby="login-title">
          <Typography variant="overline" fontWeight={700} color="primary.main" letterSpacing=".14em" sx={{ fontSize: 11, lineHeight: 1.5 }}>
            BEM-VINDO AO IA-PORTAL
          </Typography>
          <Typography component="h1" id="login-title" variant="h4" fontSize={28} mt={1}>
            Acesse seu espaço
          </Typography>
          <Typography fontSize={14} color="text.secondary" mt={1} mb={3}>
            Entre com suas credenciais para continuar.
          </Typography>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Box component="form" className="login-fields" onSubmit={handleSubmit} aria-busy={loading}>
            <TextField fullWidth label="E-mail" type="email" autoComplete="username" size="small"
              value={email} onChange={(e) => setEmail(e.target.value)} autoFocus required />
            <TextField fullWidth label="Senha" type={showPassword ? "text" : "password"} size="small"
              InputProps={{ endAdornment: <InputAdornment position="end"><IconButton aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"} onClick={() => setShowPassword(value => !value)} edge="end">{showPassword ? <VisibilityOffOutlined /> : <VisibilityOutlined />}</IconButton></InputAdornment> }}
              autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
            <Button fullWidth type="submit" variant="contained" size="large" endIcon={!loading && <ArrowForwardRounded />}
              sx={{ minHeight: 46, mt: 0.5 }} disabled={loading}>
              {loading ? "Entrando..." : "Entrar"}
            </Button>
          </Box>
          {signupEnabled && <Typography className="login-signup" color="text.secondary" fontSize={13}>
            <Link to="/signup">Criar conta no IA-Portal</Link>
          </Typography>}
          <Box component="details" className="login-demo-notice">
            <Box component="summary">Demonstração gratuita · limites de IA</Box>
            <Typography component="p" fontSize={13} mt={1}>
              O acesso a esta demonstração é gratuito. O chat está sujeito aos
              limites de uso dos provedores de IA. Se houver indisponibilidade,
              tente novamente mais tarde.
            </Typography>
          </Box>
        </Paper>
      </Box>
    </Box>
  );
}
