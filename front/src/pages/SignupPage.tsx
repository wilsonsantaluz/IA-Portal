import { useEffect, useState, type FormEvent } from "react";
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from "@mui/material";
import { Link, useLocation } from "react-router-dom";
import axios from "axios";
import Brand from "../components/Brand";
import ThemeToggle from "../components/ThemeToggle";

const signupApi = axios.create({ baseURL: (import.meta.env.VITE_API_URL || "/").replace(/\/?$/, "/") });
export default function SignupPage() {
  const location = useLocation();
  const [token, setToken] = useState("");
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    const match = /^#confirm=([A-Fa-f0-9]{64})$/.exec(location.hash);
    if (match) { setToken(match[1]); setMessage(""); setError(""); }
    window.history.replaceState(window.history.state, "", window.location.pathname);
  }, [location.hash]);
  useEffect(() => {
    signupApi.get("api/auth/signup/status").then(r => setEnabled(r.data.enabled)).catch(() => setEnabled(false));
  }, []);
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError("");
    try {
      const { data } = token
        ? await signupApi.post("api/auth/signup/confirm", { token })
        : await signupApi.post("api/auth/signup", { name, email, password });
      setMessage(data.message); setPassword("");
    } catch (e) {
      setError(axios.isAxiosError(e) ? e.response?.data?.error || (e.response?.status === 429 ? "Muitas tentativas. Aguarde alguns minutos." : "Não foi possível concluir. Verifique os dados e tente novamente.") : "Falha ao conectar.");
    } finally { setBusy(false); }
  }
  return <Box sx={{ minHeight: "100dvh", display: "grid", placeItems: "center", p: 3 }}>
    <Box sx={{ position: "absolute", right: 20, top: 20 }}><ThemeToggle /></Box>
    <Paper sx={{ p: { xs: 3, md: 5 }, width: "100%", maxWidth: 480 }}>
      <Brand />
      <Typography variant="h5" mt={3}>{token ? "Confirme seu e-mail" : "Crie sua conta no IA-Portal"}</Typography>
      <Typography color="text.secondary" mt={1} mb={3}>Acesse o chat do IA-Portal e a base demonstrativa Atlas. Você poderá criar e editar documentos compartilhados, sem excluir ou desativar conteúdos nem administrar a instalação.</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {message ? <Alert severity="success">{message}</Alert> : enabled === null ? <Typography>Carregando...</Typography> : !enabled ? <Alert severity="info">Cadastro público indisponível nesta instalação.</Alert> :
        <Stack component="form" onSubmit={submit} spacing={2}>
          {!token && <>
            <TextField label="Nome" autoComplete="name" required value={name} inputProps={{ maxLength: 150 }} onChange={e => setName(e.target.value)} />
            <TextField label="E-mail" type="email" autoComplete="email" required value={email} inputProps={{ maxLength: 150 }} onChange={e => setEmail(e.target.value)} />
            <TextField label="Senha" type="password" autoComplete="new-password" required value={password} inputProps={{ minLength: 12, maxLength: 72 }} helperText="Use pelo menos 12 caracteres (máximo de 72 bytes)." onChange={e => setPassword(e.target.value)} />
          </>}
          <Button type="submit" variant="contained" disabled={busy}>{busy ? "Aguarde..." : token ? "Confirmar cadastro" : "Receber confirmação por e-mail"}</Button>
        </Stack>}
      <Button component={Link} to="/login" sx={{ mt: 3 }}>Voltar para o login</Button>
    </Paper>
  </Box>;
}
