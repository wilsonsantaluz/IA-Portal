import { useEffect, useState } from "react";
import { Alert, Box, Button, Checkbox, FormControlLabel, MenuItem, Stack, TextField, Typography } from "@mui/material";
import api from "../services/api";
import { aiError } from "../components/ConversationModelPicker";
import WhatsAppSetupCard from "../components/WhatsAppSetupCard";

type Connection = { id: string; displayName: string; provider: string; status: string; isEnabled: boolean; autoReply: boolean; agentEnabled: boolean; agentId: string; retentionDays: number; monthlyLimit: number; version: number };
type Conversation = { id: string; humanHandoff: boolean; version: number; updatedAt: string };
type Message = { id: string; direction: string; text: string; state: string; sourcesJson: string; createdAt: string };
const states: Record<string, string> = { simulated: "Simulado", disabled: "Desativado", pending: "Pendente", processed: "Processado", queued: "Na fila", held: "Pausado", limit: "Limite atingido", failed: "Falha", "retained-tombstone": "Conteúdo removido", "receive-only; live onboarding not verified": "Somente recebimento; onboarding oficial não verificado" };
function sourceTitles(value: string) {
  try { const parsed: unknown = JSON.parse(value); return Array.isArray(parsed) ? parsed.slice(0, 8).map(s => typeof s?.Title === "string" ? s.Title : typeof s?.title === "string" ? s.title : "Documento autorizado").join(", ") : ""; }
  catch { return "Referências indisponíveis"; }
}
export default function ChannelsPage() {
  const [items, setItems] = useState<Connection[]>([]), [selected, setSelected] = useState("");
  const [name, setName] = useState(""), [account, setAccount] = useState(""), [phone, setPhone] = useState("");
  const [provider, setProvider] = useState("simulator"), [knowledge, setKnowledge] = useState<{ id: number; title: string; keyName: string }[]>([]), [ids, setIds] = useState<number[]>([]);
  const [conversations, setConversations] = useState<Conversation[]>([]), [conversation, setConversation] = useState<Conversation | null>(null), [messages, setMessages] = useState<Message[]>([]);
  const [question, setQuestion] = useState(""), [error, setError] = useState(""), [notice, setNotice] = useState(""), [busy, setBusy] = useState(false);
  const [usage, setUsage] = useState<{ inbound: number; outbound: number; monthlyLimit: number } | null>(null);
  const [messageOffset, setMessageOffset] = useState(0), [conversationOffset, setConversationOffset] = useState(0);
  const active = items.find(c => c.id === selected);
  async function load() { setItems((await api.get<Connection[]>("api/channels")).data); }
  async function history(id: string, offset = 0) { setConversationOffset(offset); setConversations((await api.get<Conversation[]>(`api/channels/${id}/conversations?offset=${offset}`)).data); setUsage((await api.get(`api/channels/${id}/usage`)).data); }
  async function show(c: Conversation, offset = 0) { setConversation(c); setMessageOffset(offset); setMessages((await api.get<Message[]>(`api/channels/conversations/${c.id}/messages?offset=${offset}`)).data); }
  async function action(work: () => Promise<void>) { setBusy(true); setError(""); setNotice(""); try { await work(); } catch (e) { setError(aiError(e)); } finally { setBusy(false); } }
  useEffect(() => { void Promise.all([load(), api.get("api/kb/all").then(r => setKnowledge(r.data))]).catch(e => setError(aiError(e))); }, []);
  useEffect(() => { setConversation(null); setMessages([]); setConversations([]); setUsage(null); if (selected) void history(selected).catch(e => setError(aiError(e))); }, [selected]);
  async function policy(patch: Partial<Connection>) { if (!active) return; await api.put(`api/channels/${active.id}`, { ...active, ...patch }); await load(); }
  return <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 1100, mx: "auto" }}><Stack gap={3}>
    <Typography variant="h5" component="h2">IA-Portal Service · Canais</Typography>
    <Alert severity="info">MVP local: agente extrativo e respostas simuladas. Cloud API recebe mensagens assinadas somente após configuração pelo operador. Envio real, Embedded Signup e Coexistence estão pendentes.</Alert>
    <WhatsAppSetupCard />
    {error && <Alert severity="error">{error}</Alert>}{notice && <Alert severity="success">{notice}</Alert>}
    <Typography variant="h6" component="h3">Cadastrar conexão</Typography>
    <TextField label="Nome da conexão" value={name} onChange={e => setName(e.target.value)} inputProps={{ maxLength: 100 }} />
    <TextField select label="Provedor do canal" value={provider} onChange={e => setProvider(e.target.value)}><MenuItem value="simulator">Simulador local</MenuItem><MenuItem value="whatsapp-cloud">WhatsApp Cloud API · somente recebimento</MenuItem></TextField>
    <Stack direction={{ xs: "column", sm: "row" }} gap={2}><TextField fullWidth label="ID da conta externa" value={account} onChange={e => setAccount(e.target.value)} /><TextField fullWidth label="ID do número externo" value={phone} onChange={e => setPhone(e.target.value)} /></Stack>
    <Typography>Documentos autorizados para o agente (nenhum por padrão)</Typography>
    {knowledge.map(k => <FormControlLabel key={k.id} label={k.title || k.keyName} control={<Checkbox checked={ids.includes(k.id)} onChange={e => setIds(e.target.checked ? [...ids, k.id] : ids.filter(id => id !== k.id))} />} />)}
    <Button disabled={busy || name.trim().length < 2 || !account || !phone} onClick={() => void action(async () => { const { data } = await api.post("api/channels", { displayName: name, provider, externalAccountId: account, externalPhoneNumberId: phone, knowledgeIds: ids }); await load(); setSelected(data.id); setNotice("Conexão cadastrada. Respostas automáticas desativadas."); })}>Cadastrar canal</Button>
    {items.length === 0 ? <Typography>Nenhum canal cadastrado.</Typography> : <TextField select label="Canal selecionado" value={selected} onChange={e => setSelected(e.target.value)}><MenuItem value="">Selecione</MenuItem>{items.map(c => <MenuItem key={c.id} value={c.id}>{c.displayName} · {c.provider}</MenuItem>)}</TextField>}
    {active && <>
      <Typography>Status: {states[active.status] || "Indisponível"}. Agente: {active.agentId}</Typography>
      <Stack direction={{ xs: "column", sm: "row" }}>
        <FormControlLabel label="Canal habilitado" control={<Checkbox disabled={busy} checked={active.isEnabled} onChange={e => void action(() => policy({ isEnabled: e.target.checked }))} />} />
        <FormControlLabel label="Agente habilitado" control={<Checkbox disabled={busy} checked={active.agentEnabled} onChange={e => void action(() => policy({ agentEnabled: e.target.checked }))} />} />
        <FormControlLabel label="Respostas automáticas simuladas" control={<Checkbox disabled={busy || active.provider !== "simulator"} checked={active.autoReply} onChange={e => void action(() => policy({ autoReply: e.target.checked }))} />} />
      </Stack>
      <Typography variant="body2">Limite mensal: {active.monthlyLimit} respostas por número. Conteúdo retido por {active.retentionDays} dias; identificadores mínimos permanecem para deduplicação.</Typography>
      <Stack direction={{ xs: "column", sm: "row" }} gap={2}>
        <TextField fullWidth label="Limite de respostas por mês" type="number" value={active.monthlyLimit} disabled={busy} inputProps={{ min: 0, max: 10000 }} onChange={e => { const value = Number(e.target.value); if (Number.isInteger(value) && value >= 0 && value <= 10000) setItems(items.map(c => c.id === active.id ? { ...c, monthlyLimit: value } : c)); }} />
        <TextField fullWidth label="Retenção do conteúdo (dias)" type="number" value={active.retentionDays} disabled={busy} inputProps={{ min: 1, max: 365 }} onChange={e => { const value = Number(e.target.value); if (Number.isInteger(value) && value >= 1 && value <= 365) setItems(items.map(c => c.id === active.id ? { ...c, retentionDays: value } : c)); }} />
        <Button disabled={busy} onClick={() => void action(async () => { await policy({}); setNotice("Limites salvos."); })}>Salvar limites</Button>
      </Stack>
      {usage && <Typography>Mensagens neste mês: {usage.inbound} recebidas · {usage.outbound} respostas reservadas. Custos Meta, LLM e infraestrutura: não medidos. O agente local não chama LLM.</Typography>}
      {active.provider === "simulator" && <><TextField label="Mensagem sintética" value={question} onChange={e => setQuestion(e.target.value)} multiline maxRows={4} />
        <Button disabled={busy || !question.trim()} onClick={() => void action(async () => { await api.post(`api/channels/${active.id}/simulate`, { externalId: crypto.randomUUID(), contactId: "synthetic-contact", text: question, timestamp: new Date().toISOString() }); setQuestion(""); await history(active.id); setNotice("Mensagem persistida. Aguarde o processamento e atualize o histórico."); })}>Simular mensagem recebida</Button></>}
      <Button disabled={busy} onClick={() => void action(async () => { await history(active.id); if (conversation) { const { data } = await api.get<Conversation[]>(`api/channels/${active.id}/conversations`); const fresh = data.find(c => c.id === conversation.id); if (fresh) await show(fresh); } })}>Atualizar histórico</Button>
      {conversations.length === 0 && <Typography>Nenhuma conversa neste canal.</Typography>}
      {conversations.map(c => <Button key={c.id} onClick={() => void action(() => show(c))}>Conversa {c.id.slice(0, 8)} · {c.humanHandoff ? "Atendimento humano" : "Agente"}</Button>)}
      <Stack direction="row" gap={1}><Button disabled={busy || conversationOffset === 0} onClick={() => void action(() => history(active.id, conversationOffset - 50))}>Conversas anteriores</Button><Button disabled={busy || conversations.length < 50} onClick={() => void action(() => history(active.id, conversationOffset + 50))}>Próximas conversas</Button></Stack>
      {conversation && <>
        <FormControlLabel label="Encaminhar para atendimento humano (pausar agente)" control={<Checkbox disabled={busy} checked={conversation.humanHandoff} onChange={e => void action(async () => { await api.post(`api/channels/conversations/${conversation.id}/handoff`, { version: conversation.version, humanHandoff: e.target.checked }); const { data } = await api.get<Conversation[]>(`api/channels/${active.id}/conversations`); const fresh = data.find(c => c.id === conversation.id); if (fresh) await show(fresh); })} />} />
        {messages.map(m => <Box key={m.id} sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 2 }}><Typography variant="caption">{m.direction === "inbound" ? "Recebida" : "Resposta"} · {states[m.state] || "Indisponível"} · {new Date(m.createdAt).toLocaleString("pt-BR")}</Typography><Typography sx={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>{m.text || "Conteúdo removido pela retenção."}</Typography>{m.sourcesJson !== "[]" && <Typography variant="caption" sx={{ overflowWrap: "anywhere" }}>Fontes: {sourceTitles(m.sourcesJson)}</Typography>}</Box>)}
        <Stack direction="row" gap={1}><Button disabled={busy || messageOffset === 0} onClick={() => void action(() => show(conversation, messageOffset - 50))}>Mensagens anteriores</Button><Button disabled={busy || messages.length < 50} onClick={() => void action(() => show(conversation, messageOffset + 50))}>Próximas mensagens</Button></Stack>
      </>}
    </>}
  </Stack></Box>;
}
