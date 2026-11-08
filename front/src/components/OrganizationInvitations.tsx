import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import api from "../services/api";
import { aiError } from "./ConversationModelPicker";

type Invitation = { id: string; role: string; expiresAt: string; version: number; status: string };
const statuses: Record<string, string> = { pending: "Pendente", accepted: "Aceito", revoked: "Revogado", expired: "Expirado", invalidated: "Autorização do emissor alterada" };
export default function OrganizationInvitations() {
  const [items, setItems] = useState<Invitation[]>([]); const [role, setRole] = useState("Member");
  const [link, setLink] = useState(""); const [error, setError] = useState(""); const [notice, setNotice] = useState(""); const [busy, setBusy] = useState(false);
  const load = useCallback(async () => setItems((await api.get<Invitation[]>("api/organizations/current/invitations")).data), []);
  useEffect(() => { void load().catch(e => setError(aiError(e))); }, [load]);
  async function create() {
    setBusy(true); setError(""); setNotice(""); setLink("");
    try {
      const { data } = await api.post<{ token: string }>("api/organizations/current/invitations", { role });
      setLink(`${window.location.origin}/invite#invite=${data.token}`); await load();
    } catch (e) { setError(aiError(e)); } finally { setBusy(false); }
  }
  async function revoke(item: Invitation) {
    setBusy(true); setError(""); setLink("");
    try { await api.post(`api/organizations/current/invitations/${item.id}/revoke`, { version: item.version }); await load(); setNotice("Convite revogado."); }
    catch (e) { setError(aiError(e)); } finally { setBusy(false); }
  }
  return <Stack gap={2}>
    <Typography variant="h6" component="h3">Convites para contas existentes</Typography>
    <Typography variant="body2">O link vale por 24 horas e pode ser usado uma única vez por qualquer conta autenticada que o possua. Compartilhe somente com a pessoa desejada. Nenhuma mensagem é enviada pelo portal.</Typography>
    {error && <Alert severity="error">{error}</Alert>}{notice && <Alert severity="success">{notice}</Alert>}
    <Stack direction={{ xs: "column", sm: "row" }} gap={1}>
      <TextField select label="Papel do convite" value={role} onChange={e => setRole(e.target.value)} size="small" disabled={busy} sx={{ minWidth: 140 }}>
        <MenuItem value="Member">Member</MenuItem><MenuItem value="Viewer">Viewer</MenuItem>
      </TextField>
      <Button disabled={busy} onClick={() => void create()}>Gerar link de convite</Button>
    </Stack>
    {link && <>
      <Alert severity="warning">Este link só é exibido agora. Quem o possuir poderá aceitar o convite. Não o publique.</Alert>
      <TextField label="Link de convite" value={link} InputProps={{ readOnly: true }} size="small" />
      <Button onClick={() => {
        if (!navigator.clipboard) { setNotice("Selecione e copie o link no campo acima."); return; }
        void navigator.clipboard.writeText(link).then(() => setNotice("Link copiado.")).catch(() => setNotice("Selecione e copie o link no campo acima."));
      }}>Copiar link</Button>
    </>}
    {items.length === 0 && <Typography variant="body2">Nenhum convite registrado.</Typography>}
    {items.map(item => <Box key={item.id} sx={{ borderBottom: "1px solid", borderColor: "divider", pb: 1 }}>
      <Typography variant="body2">{item.role} · {statuses[item.status] || "Indisponível"} · {new Date(item.expiresAt).toLocaleString("pt-BR")}</Typography>
      {item.status === "pending" && <Button disabled={busy} aria-label={`Revogar convite ${item.id}`} onClick={() => void revoke(item)}>Revogar</Button>}
    </Box>)}
  </Stack>;
}
