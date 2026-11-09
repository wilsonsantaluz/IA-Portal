import { useEffect, useState } from "react";
import { Alert, Box, Button, Chip, Link, Stack, Typography } from "@mui/material";
import api from "../services/api";
import { aiError } from "./ConversationModelPicker";

type Status = { appId: string | null; webhookUrl: string | null; webhookPath: string; appSecretConfigured: boolean; verifyTokenConfigured: boolean; accessTokenConfigured: boolean; publicCallbackConfigured: boolean; receiveConfigurationReady: boolean; liveSendingAvailable: boolean; authorizedNumbers: { phoneNumberId: string; accountId: string }[] };
export default function WhatsAppSetupCard() {
  const [status, setStatus] = useState<Status | null>(null), [error, setError] = useState(""), [busy, setBusy] = useState(false);
  async function load() { setBusy(true); setError(""); try { setStatus((await api.get<Status>("api/channels/whatsapp/setup")).data); } catch (e) { setError(aiError(e)); } finally { setBusy(false); } }
  useEffect(() => { void load(); }, []);
  return <Box component="section" aria-labelledby="whatsapp-setup-title" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, p: 2 }}>
    <Stack gap={2}>
      <Typography id="whatsapp-setup-title" variant="h6" component="h3">Conectar WhatsApp oficial</Typography>
      <Typography variant="body2">Comece pelo número de teste da Meta. Um link de simulador não conecta uma conta real do WhatsApp.</Typography>
      <Box component="ol" sx={{ pl: 3, m: 0, "& li": { mb: 1 } }}>
        <li><Typography variant="body2">Cadastre-se no Meta for Developers, crie um aplicativo com WhatsApp e abra a configuração da API.</Typography></li>
        <li><Typography variant="body2">Anote o WhatsApp Business Account ID (WABA) e o Phone Number ID exibidos pela Meta. O ID do número é diferente do número de telefone.</Typography></li>
        <li><Typography variant="body2">Configure o access token, o App Secret e o token de verificação no backend. Eles não são exibidos nem armazenados neste navegador.</Typography></li>
        <li><Typography variant="body2">Cadastre na Meta um callback HTTPS público e autorize o número para esta organização no servidor. Localhost não pode receber os webhooks da Meta.</Typography></li>
      </Box>
      <Stack direction={{ xs: "column", sm: "row" }} gap={1}>
        <Button component={Link} href="https://developers.facebook.com/async/registration/" target="_blank" rel="noopener noreferrer">Cadastrar desenvolvedor na Meta</Button>
        <Button component={Link} href="https://developers.facebook.com/apps" target="_blank" rel="noopener noreferrer">Abrir painel de aplicativos</Button>
        <Button disabled={busy} onClick={() => void load()}>Verificar configuração local</Button>
      </Stack>
      {error && <Alert severity="error">{error}</Alert>}
      {!status && !error && <Typography variant="body2">Verificando configuração do backend...</Typography>}
      {status && <>
        <Stack direction="row" flexWrap="wrap" gap={1}>
          <Chip label={`App Secret: ${status.appSecretConfigured ? "configurado" : "pendente"}`} size="small" />
          <Chip label={`Verificação: ${status.verifyTokenConfigured ? "configurada" : "pendente"}`} size="small" />
          <Chip label={`Access token: ${status.accessTokenConfigured ? "configurado" : "pendente"}`} size="small" />
        </Stack>
        {status.appId && <Typography variant="body2">App ID: {status.appId}</Typography>}
        <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>Callback: {status.webhookUrl || `${status.webhookPath} · endereço HTTPS público ainda não configurado`}</Typography>
        {status.authorizedNumbers.length === 0 ? <Typography variant="body2">Nenhum número autorizado no servidor para esta organização.</Typography> : status.authorizedNumbers.map(n => <Typography key={n.phoneNumberId} variant="body2" sx={{ overflowWrap: "anywhere" }}>Phone Number ID: {n.phoneNumberId} · WABA ID: {n.accountId}</Typography>)}
        <Alert severity={status.receiveConfigurationReady ? "info" : "warning"}>{status.receiveConfigurationReady ? "Configuração de recebimento preenchida. A verificação no painel Meta e a entrega de uma mensagem real ainda precisam ser validadas." : "Recebimento real pendente: conclua os dados no painel Meta e a configuração privada do servidor."}</Alert>
        <Typography variant="body2">Nesta versão, o simulador e o webhook de recebimento estão implementados. Envio real, Embedded Signup e Coexistence ainda não estão disponíveis; o envio automático real permanece bloqueado.</Typography>
      </>}
    </Stack>
  </Box>;
}
