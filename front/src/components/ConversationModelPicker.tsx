import { useEffect, useState } from "react";
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField } from "@mui/material";
import api from "../services/api";
import type { Session } from "../hooks/useChatLogic";
export type ProviderOption = { id: string; models: string[]; defaultModel: string; streaming: boolean; apiBilling: string };
export const aiError = (error: unknown) => (error as { response?: { data?: { error?: string } } }).response?.data?.error || "Não foi possível concluir a operação.";
export default function ConversationModelPicker({ session, disabled, refresh }: { session: Session; disabled: boolean; refresh: () => Promise<unknown> }) {
  const [providers, setProviders] = useState<ProviderOption[]>([]);
  const [choice, setChoice] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => { let active = true; api.get<ProviderOption[]>("api/ai/providers").then(r => { if (active) setProviders(r.data); }).catch(e => { if (active) setError(aiError(e)); }); return () => { active = false; }; }, [session.id]);
  const current = JSON.stringify({ provider: session.provider, model: session.model });
  const options = providers.flatMap(p => p.models.map(model => ({ value: JSON.stringify({ provider: p.id, model }), label: `${p.id} · ${model}` })));
  async function change() {
    setBusy(true); setError("");
    try { await api.patch(`api/chatBot/sessions/${session.id}/model`, { ...JSON.parse(choice), version: session.version }); setChoice(""); await refresh(); }
    catch (e) { setError(aiError(e)); await refresh(); }
    finally { setBusy(false); }
  }
  return <Stack gap={1} sx={{ mt: 1, maxWidth: 460 }}>
    <TextField select size="small" label="Modelo da conversa" value={current} disabled={disabled || busy} onChange={e => setChoice(e.target.value)}>
      {!options.some(o => o.value === current) && <MenuItem value={current} disabled>{session.provider} · {session.model} (indisponível)</MenuItem>}
      {options.map(o => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
    </TextField>
    {error && <Alert severity="error" onClose={() => setError("")}>{error}</Alert>}
    <Dialog open={!!choice} onClose={() => !busy && setChoice("")} aria-labelledby="change-model-title">
      <DialogTitle id="change-model-title">Trocar modelo da conversa?</DialogTitle>
      <DialogContent>As próximas respostas usarão {choice && `${JSON.parse(choice).provider} · ${JSON.parse(choice).model}`}. O histórico será preservado.</DialogContent>
      <DialogActions><Button disabled={busy} onClick={() => setChoice("")}>Cancelar</Button><Button variant="contained" disabled={busy} onClick={() => void change()}>Confirmar troca</Button></DialogActions>
    </Dialog>
  </Stack>;
}
