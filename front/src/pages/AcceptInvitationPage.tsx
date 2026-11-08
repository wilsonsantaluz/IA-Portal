import { useEffect, useState } from "react";
import { Alert, Box, Button, CircularProgress, Stack, Typography } from "@mui/material";
import { Link } from "react-router-dom";
import api from "../services/api";
import { switchOrganization } from "../auth/switchOrganization";
import { aiError } from "../components/ConversationModelPicker";
import { useAuth } from "../auth/AuthProvider";

type Preview = { name: string; role: string; expiresAt: string; alreadyMember: boolean };
export default function AcceptInvitationPage() {
  const { user } = useAuth();
  const [token] = useState(() => /^#invite=([a-fA-F0-9]{64})$/.exec(window.location.hash)?.[1] || "");
  const [preview, setPreview] = useState<Preview | null>(null);
  const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  useEffect(() => {
    // Remove the capability from the visible address/history entry; never use localStorage.
    window.history.replaceState(window.history.state, "", window.location.pathname);
    if (!token) { setError("Convite inválido ou indisponível. Abra o link original recebido."); return; }
    let active = true;
    void api.post<Preview>("api/organization-invitations/preview", { token }).then(({ data }) => { if (active) setPreview(data); })
      .catch(e => { if (active) setError(aiError(e)); });
    return () => { active = false; };
  }, [token]);
  async function accept() {
    setBusy(true); setError("");
    try {
      const { data } = await api.post<{ organizationId: number }>("api/organization-invitations/accept", { token });
      await switchOrganization(data.organizationId, "/organizations");
    } catch (e) { setError(aiError(e)); setBusy(false); }
  }
  return <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 720, mx: "auto" }}>
    <Stack gap={2}>
      <Typography variant="h5" component="h2">Convite de organização</Typography>
      <Typography variant="body2">Conta: <strong>{user?.email}</strong></Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {!preview && !error && <CircularProgress aria-label="Verificando convite" />}
      {preview && <>
        <Typography>Você foi convidado para <strong>{preview.name}</strong> com o papel <strong>{preview.role}</strong>.</Typography>
        <Typography variant="body2">Válido até {new Date(preview.expiresAt).toLocaleString("pt-BR")}.</Typography>
        <Alert severity="info">{preview.alreadyMember ? "Você já participa desta organização. Seu papel atual será preservado." : "Sua conta será associada à organização somente após sua confirmação. Isso não compartilha suas conversas ou documentos de outras organizações."}</Alert>
        <Button variant="contained" disabled={busy || !!error} onClick={() => void accept()}>Aceitar convite e entrar na organização</Button>
      </>}
      <Button component={Link} to="/organizations" disabled={busy}>Voltar sem aceitar</Button>
    </Stack>
  </Box>;
}
