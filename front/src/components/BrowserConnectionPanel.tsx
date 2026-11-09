import { useEffect, useState } from "react";
import { Alert, Box, Button, Stack, Typography } from "@mui/material";
import api from "../services/api";
import { aiError } from "./ConversationModelPicker";
type Status = { state: string; updatedAt: string; lastConnectedAt: string | null; attempts: number; qr: string | null; qrExpiresAt: string | null };
const labels: Record<string, string> = { account_in_use: "Conta já vinculada a outra conexão", disconnected: "Desconectado — ação necessária", starting: "Iniciando navegador", awaiting_qr: "Aguardando pareamento", authenticating: "Autenticando", connected: "Conectado", reconnecting: "Reconectando", pairing_expired: "Pareamento expirado" };
export default function BrowserConnectionPanel({ id, enabled }: { id: string; enabled: boolean }) {
  const [status, setStatus] = useState<Status | null>(null), [error, setError] = useState(""), [busy, setBusy] = useState(false);
  useEffect(() => {
    let disposed = false;
    async function refresh() { try { const { data } = await api.get<Status>(`api/channels/${id}/browser`); if (!disposed) { setStatus(data); setError(""); } } catch (e) { if (!disposed) { setStatus(null); setError(aiError(e)); } } }
    void refresh(); const timer = window.setInterval(() => void refresh(), 5000);
    return () => { disposed = true; clearInterval(timer); };
  }, [id]);
  async function operate(operation: string) {
    if (operation === "logout" && !window.confirm("Desvincular esta sessão do WhatsApp? Será necessário ler um novo QR Code.")) return;
    setBusy(true); setError(""); try { setStatus((await api.post<Status>(`api/channels/${id}/browser/${operation}`)).data); } catch (e) { setError(aiError(e)); } finally { setBusy(false); }
  }
  return <Box component="section" aria-label="Sessão WhatsApp no servidor" sx={{ border: "1px solid", borderColor: "divider", p: 2, borderRadius: 2 }}><Stack gap={2}>
    <Typography variant="h6">WhatsApp no servidor</Typography>
    <Typography role="status">{status ? labels[status.state] || "Estado desconhecido" : "Status indisponível"}</Typography>
    {error && <Alert severity="error">{error}</Alert>}
    {status?.qr && status.state === "awaiting_qr" && <>
      <Typography>Na conta desejada, abra WhatsApp → Aparelhos conectados → Conectar um aparelho e leia este QR Code. Ele autoriza acesso à conta; não o compartilhe.</Typography>
      <Box component="img" src={status.qr} alt="QR Code privado para vincular WhatsApp" sx={{ width: 300, maxWidth: "100%", alignSelf: "flex-start" }} />
      <Typography variant="caption">Validade deste código: {status.qrExpiresAt && new Date(status.qrExpiresAt).toLocaleTimeString("pt-BR")}</Typography>
    </>}
    {status?.lastConnectedAt && <Typography variant="body2">Última conexão: {new Date(status.lastConnectedAt).toLocaleString("pt-BR")}</Typography>}
    {status && <Typography variant="caption">Última atualização: {new Date(status.updatedAt).toLocaleString("pt-BR")} · tentativas de reconexão: {status.attempts}</Typography>}
    <Stack direction={{ xs: "column", sm: "row" }} gap={1}>
      <Button disabled={busy || !enabled} onClick={() => void operate("start")}>Conectar / reconectar</Button>
      <Button disabled={busy} onClick={() => void operate("stop")}>Pausar sessão</Button>
      <Button disabled={busy} color="warning" onClick={() => void operate("logout")}>Desvincular sessão</Button>
    </Stack>
    <Typography variant="body2">Integração experimental pelo WhatsApp Web. Texto individual está disponível; grupos, anexos e importação de histórico não estão implementados. Respostas automáticas começam desativadas.</Typography>
  </Stack></Box>;
}
