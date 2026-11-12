import { useEffect, useRef, useState } from "react";
import { Alert, Box, Button, Chip, LinearProgress, MenuItem, Paper, Stack, Tab, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Tabs, TextField, Typography } from "@mui/material";
import { Link, useSearchParams } from "react-router-dom";
import api from "../services/api";
import { useAuth } from "../auth/AuthProvider";
import { aiError } from "../components/ConversationModelPicker";
import type { Session } from "../hooks/useChatLogic";

type Totals = { requests: number; errors: number; unknown: number; reportedRequests: number; totalTokenRequests: number; pricedRequests: number; inputTokens: number | null; outputTokens: number | null; totalTokens: number | null; cacheTokens: number | null; estimatedCostUsd: number | null; tokensComplete: boolean; costComplete: boolean; localApiRequests: number };
type Group = { key: { userId: number; provider: string; model: string; purpose: string; year: number; month: number; day: number }; totals: Totals };
type Call = { id: string; provider: string; model: string; createdAt: string; purpose: string; status: string; totalTokens: number | null; inputTokens: number | null; outputTokens: number | null; cacheTokens: number | null; durationMs: number | null; estimatedCostUsd: number | null; apiBilling: string };
type Page<T> = { items: T[]; nextPage: number | null };
type Params = Record<string, string | number>;
const count = (n: number | null | undefined) => n == null ? "Indisponível" : n.toLocaleString("pt-BR");
const money = (n: number | null | undefined) => n == null ? "Indisponível" : new Intl.NumberFormat("pt-BR", { style: "currency", currency: "USD", maximumFractionDigits: 6 }).format(n);
const purpose: Record<string, string> = { chat: "Chat", title: "Título automático", document: "Documento" };
const status: Record<string, string> = { started: "Resultado desconhecido", completed: "Concluída", failed: "Falha", timeout: "Tempo limite", cancelled: "Cancelada", invalid_response: "Resposta inválida" };
const surface = { p: { xs: 2, md: 3 }, border: "1px solid", borderColor: "divider", borderRadius: 3, boxShadow: "var(--portal-shadow)", minWidth: 0 };
const groupLabel = (group: Group, by: string) => by === "model" ? `${group.key.provider} · ${group.key.model}` : by === "provider" ? group.key.provider : by === "purpose" ? purpose[group.key.purpose] || group.key.purpose : by === "user" ? `Usuário #${group.key.userId}` : `${group.key.year}-${String(group.key.month).padStart(2, "0")}${group.key.day ? "-" + String(group.key.day).padStart(2, "0") : ""}`;

export default function UsagePage() {
  const { hasPermission } = useAuth(); const admin = hasPermission("usage.admin"); const personal = hasPermission("chat.access");
  const [searchParams] = useSearchParams();
  const now = new Date(); const [from, setFrom] = useState(new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1)).toISOString().slice(0, 10));
  const [to, setTo] = useState(now.toISOString().slice(0, 10));
  const [scope, setScope] = useState(personal ? "me" : "admin");
  const [conversation, setConversation] = useState(searchParams.get("sessionId") || "");
  const [sessions, setSessions] = useState<Session[]>([]); const [sessionsCursor, setSessionsCursor] = useState<string | null>(null);
  const [userId, setUserId] = useState(""); const [provider, setProvider] = useState(""); const [model, setModel] = useState(""); const [selectedPurpose, setPurpose] = useState("");
  const [periodGroup, setPeriodGroup] = useState("day");
  const [summary, setSummary] = useState<Totals | null>(null);
  const [groups, setGroups] = useState<Record<string, Page<Group>>>({}); const [calls, setCalls] = useState<Page<Call>>({ items: [], nextPage: null });
  const [loading, setLoading] = useState(false); const [error, setError] = useState(""); const [applied, setApplied] = useState<Params | null>(null);
  const requestVersion = useRef(0); const [busyMore, setBusyMore] = useState("");
  async function sessionOptions(cursor?: string) {
    try { const r = await api.get<{ items: Session[]; nextCursor: string | null }>("api/chatBot/sessions", { params: { limit: 30, cursor } }); setSessions(prev => cursor ? [...prev, ...r.data.items.filter(s => !prev.some(p => p.id === s.id))] : r.data.items); setSessionsCursor(r.data.nextCursor); }
    catch (e) { setError(aiError(e)); }
  }
  useEffect(() => { if (personal) { void sessionOptions(); const id = searchParams.get("sessionId"); if (id) api.get<Session>(`api/chatBot/sessions/${id}`).then(r => setSessions(prev => prev.some(s => s.id === id) ? prev : [...prev, r.data])).catch(e => setError(aiError(e))); } return () => { requestVersion.current++; }; }, []);
  const base = scope === "admin" ? "api/usage/admin" : "api/usage";
  async function load() {
    const version = ++requestVersion.current; setLoading(true); setError(""); setSummary(null); setGroups({}); setCalls({ items: [], nextPage: null }); setApplied(null);
    try {
      const start = new Date(from + "T00:00:00Z"); const end = new Date(to + "T00:00:00Z"); end.setUTCDate(end.getUTCDate() + 1);
      if (!from || !to || !Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime()) || start >= end || end.getTime() - start.getTime() > 366 * 86400000) throw new Error("Selecione um período válido de até 366 dias.");
      if (scope === "admin" && userId && (!/^\d+$/.test(userId) || Number(userId) < 1)) throw new Error("Informe um identificador de usuário válido.");
      const params: Params = { from: start.toISOString(), to: end.toISOString() };
      if (provider) params.provider = provider; if (model.trim()) params.model = model.trim(); if (selectedPurpose) params.purpose = selectedPurpose;
      if (scope === "me" && conversation) params.sessionId = conversation;
      if (scope === "admin" && userId) params.userId = Number(userId);
      const dimensions = ["model", "provider", "purpose", periodGroup, ...(scope === "admin" ? ["user"] : [])];
      const [totals, ...buckets] = await Promise.all([api.get<Totals>(base + "/summary", { params }), ...dimensions.map(by => api.get<Page<Group>>(base + "/groups", { params: { ...params, by, limit: 30 } }))]);
      const detail = scope === "me" ? await api.get<Page<Call>>(base + "/calls", { params: { ...params, limit: 30 } }) : null;
      if (version !== requestVersion.current) return;
      setSummary(totals.data as Totals); const next: Record<string, Page<Group>> = {};
      dimensions.forEach((by, i) => { next[by] = buckets[i].data as Page<Group>; }); setGroups(next); setCalls(detail?.data ?? { items: [], nextPage: null }); setApplied(params);
    } catch (e) { if (version === requestVersion.current) setError(e instanceof Error && !('response' in e) ? e.message : aiError(e)); }
    finally { if (version === requestVersion.current) setLoading(false); }
  }
  useEffect(() => { void load(); }, [scope]);
  async function more(by: string) {
    if (!applied) return; const page = by === "calls" ? calls.nextPage : groups[by]?.nextPage; if (!page) return;
    const version = requestVersion.current; setBusyMore(by);
    try { const r = await api.get(base + (by === "calls" ? "/calls" : "/groups"), { params: { ...applied, ...(by !== "calls" ? { by } : {}), page, limit: 30 } }); if (version !== requestVersion.current) return;
      if (by === "calls") setCalls(prev => ({ ...r.data, items: [...prev.items, ...r.data.items] })); else setGroups(prev => ({ ...prev, [by]: { ...r.data, items: [...prev[by].items, ...r.data.items] } }));
    } catch (e) { if (version === requestVersion.current) setError(aiError(e)); } finally { setBusyMore(""); }
  }
  return <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 1400, mx: "auto", width: "100%" }}>
    <Typography variant="h5" component="h2" fontWeight={700}>Consumo de Inteligência Artificial</Typography>
    <Typography color="text.secondary" sx={{ mt: 1, mb: 3 }}>Uso registrado nas conversas, documentos e títulos. Valores ausentes permanecem indisponíveis.</Typography>
    {admin && personal && <Tabs value={scope} onChange={(_, v) => { setScope(v); setConversation(""); setUserId(""); }} sx={{ mb: 2 }} aria-label="Escopo do consumo"><Tab value="me" label="Meu consumo" /><Tab value="admin" label="Todos os usuários" /></Tabs>}
    <Paper component="form" sx={{ ...surface, mb: 3 }} onSubmit={e => { e.preventDefault(); void load(); }}>
      <Box display="grid" gridTemplateColumns={{ xs: "1fr", sm: "repeat(2, minmax(0,1fr))", lg: "repeat(4, minmax(0,1fr))" }} gap={2}>
        <TextField type="date" label="Data inicial (UTC)" value={from} onChange={e => setFrom(e.target.value)} InputLabelProps={{ shrink: true }} disabled={loading} size="small" required />
        <TextField type="date" label="Data final (UTC)" value={to} onChange={e => setTo(e.target.value)} InputLabelProps={{ shrink: true }} disabled={loading} size="small" required />
        <TextField select label="Provedor" value={provider} onChange={e => setProvider(e.target.value)} disabled={loading} size="small"><MenuItem value="">Todos</MenuItem>{["deepseek", "openai", "ollama", "litellm"].map(p => <MenuItem key={p} value={p}>{p}</MenuItem>)}</TextField>
        <TextField label="Modelo (opcional)" value={model} onChange={e => setModel(e.target.value)} disabled={loading} size="small" inputProps={{ maxLength: 200 }} />
        <TextField select label="Finalidade" value={selectedPurpose} onChange={e => setPurpose(e.target.value)} disabled={loading} size="small"><MenuItem value="">Todas</MenuItem>{Object.entries(purpose).map(([p, name]) => <MenuItem key={p} value={p}>{name}</MenuItem>)}</TextField>
        <TextField select label="Agrupar período" value={periodGroup} onChange={e => setPeriodGroup(e.target.value)} disabled={loading} size="small"><MenuItem value="day">Dia</MenuItem><MenuItem value="month">Mês</MenuItem></TextField>
        {scope === "me" ? <Stack><TextField select label="Conversa" value={conversation} onChange={e => setConversation(e.target.value)} disabled={loading} size="small"><MenuItem value="">Todas as conversas</MenuItem>{conversation && !sessions.some(s => s.id === conversation) && <MenuItem value={conversation} disabled>Conversa selecionada</MenuItem>}{sessions.map(s => <MenuItem key={s.id} value={s.id}>{s.title}</MenuItem>)}</TextField>{sessionsCursor && <Button size="small" onClick={() => void sessionOptions(sessionsCursor)}>Mais conversas</Button>}</Stack> : <TextField label="Usuário (ID opcional)" value={userId} onChange={e => setUserId(e.target.value)} disabled={loading} size="small" inputProps={{ inputMode: "numeric" }} />}
        <Button type="submit" variant="contained" disabled={loading || !!busyMore}>Aplicar filtros</Button>
      </Box>
    </Paper>
    {loading && <LinearProgress aria-label="Carregando consumo" sx={{ mb: 2 }} />}{error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    {summary && <>
      <Typography variant="caption" color="text.secondary">Período aplicado: {String(applied?.from).slice(0,10)} até {String(applied?.to).slice(0,10)} (fim exclusivo, UTC).</Typography>
      {summary.requests === 0 && <Alert severity="info" sx={{ my: 2 }}>Nenhuma chamada registrada para este período e filtros.</Alert>}
      <Box display="grid" gridTemplateColumns={{ xs: "1fr", md: "repeat(3,minmax(0,1fr))" }} gap={2} sx={{ my: 2 }}>
        <Paper sx={surface}><Typography color="text.secondary">Chamadas registradas</Typography><Typography variant="h4" sx={{ my: 1 }}>{count(summary.requests)}</Typography><Typography variant="body2">{count(summary.errors)} erros · {count(summary.unknown)} com resultado desconhecido</Typography></Paper>
        <Paper sx={surface}><Stack direction="row" gap={1} flexWrap="wrap"><Typography color="text.secondary">Tokens reportados</Typography>{!summary.tokensComplete && summary.requests > 0 && <Chip size="small" label="Cobertura parcial" color="warning" variant="outlined" sx={{ color: "text.primary" }} />}</Stack><Typography variant="h4" sx={{ my: 1 }}>{count(summary.totalTokens)}</Typography><Typography variant="body2">Total disponível em {summary.totalTokenRequests}/{summary.requests} chamadas.</Typography><Typography variant="caption" color="text.secondary">Entrada: {count(summary.inputTokens)} · Saída: {count(summary.outputTokens)} · Cache: {count(summary.cacheTokens)} (incluído na entrada).</Typography></Paper>
        <Paper sx={surface}><Typography color="text.secondary">API externa estimada (USD)</Typography><Typography variant="h4" sx={{ my: 1 }}>{money(summary.estimatedCostUsd)}</Typography><Typography variant="body2">{summary.pricedRequests}/{summary.requests} chamadas com custo conhecido{!summary.costComplete && summary.pricedRequests > 0 ? " — soma parcial" : ""}.</Typography><Typography variant="caption" color="text.secondary">Exige preço/tokens suficientes. Infraestrutura não incluída.</Typography></Paper>
      </Box>
      {summary.localApiRequests > 0 && <Alert severity="info" sx={{ mb: 2 }}>{summary.localApiRequests} chamadas a endpoints declarados locais, sem taxa de API externa. Custo de infraestrutura não calculado.</Alert>}
      <Box display="grid" gridTemplateColumns={{ xs: "1fr", xl: "repeat(2,minmax(0,1fr))" }} gap={2}>
        {Object.entries(groups).map(([by, page]) => <Paper key={by} sx={surface}><Typography variant="h6" component="h3" sx={{ mb: 1 }}>{({ model: "Por modelo", provider: "Por provedor", purpose: "Por finalidade", day: "Por dia (UTC)", month: "Por mês (UTC)", user: "Por usuário" } as Record<string,string>)[by]}</Typography>
          {!page.items.length ? <Typography color="text.secondary">Sem registros.</Typography> : <TableContainer tabIndex={0} role="region" aria-label={`Tabela de consumo ${by}`} sx={{ "&:focus-visible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: 2 } }}><Table size="small" aria-label={`Consumo ${by}`}><TableHead><TableRow><TableCell>Grupo</TableCell><TableCell align="right">Chamadas</TableCell><TableCell align="right">Tokens</TableCell><TableCell align="right">API estimada</TableCell></TableRow></TableHead><TableBody>{page.items.map((g, i) => <TableRow key={`${groupLabel(g,by)}-${i}`}><TableCell sx={{ overflowWrap: "anywhere", maxWidth: 250 }}>{groupLabel(g,by)}</TableCell><TableCell align="right">{g.totals.requests}</TableCell><TableCell align="right">{count(g.totals.totalTokens)}{g.totals.totalTokens != null && !g.totals.tokensComplete ? " (parcial)" : ""}</TableCell><TableCell align="right">{money(g.totals.estimatedCostUsd)}{g.totals.estimatedCostUsd != null && !g.totals.costComplete ? " (parcial)" : ""}</TableCell></TableRow>)}</TableBody></Table></TableContainer>}
          {page.nextPage && <Button disabled={!!busyMore || loading} onClick={() => void more(by)}>Carregar mais grupos</Button>}
        </Paper>)}
      </Box>
      {scope === "me" && <Paper sx={{ ...surface, mt: 2 }}><Typography variant="h6" component="h3" sx={{ mb: 1 }}>Suas chamadas</Typography><Typography variant="caption" color="text.secondary">Horários locais. Prompts e respostas não fazem parte destes registros.</Typography>
        {!calls.items.length ? <Typography color="text.secondary" sx={{ mt: 2 }}>Sem chamadas neste período.</Typography> : <TableContainer tabIndex={0} role="region" aria-label="Detalhamento das suas chamadas" sx={{ "&:focus-visible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: 2 } }}><Table size="small" aria-label="Chamadas de IA"><TableHead><TableRow>{["Horário", "Provedor / modelo", "Finalidade", "Tokens", "Duração", "Status", "API estimada"].map(label => <TableCell key={label}>{label}</TableCell>)}</TableRow></TableHead><TableBody>{calls.items.map(c => <TableRow key={c.id}><TableCell>{new Date(c.createdAt).toLocaleString("pt-BR")}</TableCell><TableCell sx={{ overflowWrap: "anywhere", minWidth: 130 }}>{c.provider} · {c.model}</TableCell><TableCell>{purpose[c.purpose] || c.purpose}</TableCell><TableCell>{count(c.totalTokens)}<Typography variant="caption" display="block">Entrada {count(c.inputTokens)} · Saída {count(c.outputTokens)} · Cache {count(c.cacheTokens)}</Typography></TableCell><TableCell>{c.durationMs == null ? "—" : `${c.durationMs} ms`}</TableCell><TableCell>{status[c.status] || c.status}</TableCell><TableCell>{money(c.estimatedCostUsd)}{c.apiBilling === "local" && <Typography variant="caption" display="block">API local</Typography>}</TableCell></TableRow>)}</TableBody></Table></TableContainer>}
        {calls.nextPage && <Button disabled={loading || !!busyMore} onClick={() => void more("calls")}>Carregar mais chamadas</Button>}
      </Paper>}
    </>}
    {(hasPermission("chat.access") || hasPermission("ai.manage")) && <Button component={Link} to="/settings/ai" sx={{ mt: 3 }}>Configurações de Inteligência Artificial</Button>}
  </Box>;
}
