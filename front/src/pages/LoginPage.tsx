import { useState } from "react";
import {
  Box,
  Button,
  TextField,
  Typography,
  Paper,
  Alert
} from "@mui/material";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { useAuth } from "../auth/AuthProvider";
import { useNavigate } from "react-router-dom";
import React from "react";

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
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
        minHeight: "80vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
     
      }}
    >
      <Paper
        elevation={10}
        sx={{
          width: 380,
          p: 4,
          borderRadius: 2
        }}
      >
        <Box
          sx={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            mb: 3
          }}
        >
          <Box
            sx={{
              backgroundColor: "#1976d2",
              color: "#fff",
              borderRadius: "50%",
              width: 48,
              height: 48,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              mb: 1
            }}
          >
            <LockOutlinedIcon />
          </Box>

          <Typography variant="h5" fontWeight={600}>
            Portal de Conhecimento IA
          </Typography>

          <Typography variant="body2" color="text.secondary">
            Acesso ao sistema
          </Typography>
        </Box>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <Box component="form" onSubmit={handleSubmit}>
          <TextField
            fullWidth
            label="Email"
            margin="normal"
            value={email}
            onChange={e => setEmail(e.target.value)}
            autoFocus
          />

          <TextField
            fullWidth
            label="Senha"
            type="password"
            margin="normal"
            value={password}
            onChange={e => setPassword(e.target.value)}
          />

          <Button
            fullWidth
            type="submit"
            variant="contained"
            size="large"
            sx={{ mt: 3 }}
            disabled={loading}
          >
            {loading ? "Entrando..." : "Entrar"}
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}
