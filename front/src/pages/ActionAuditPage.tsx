import { useEffect, useRef, useState, type FormEvent } from "react";
import { Alert, Box, Button, Chip, Container, Grid, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from "@mui/material";
import SearchOutlined from "@mui/icons-material/SearchOutlined";
import api from "../services/api";
import { useAuth } from "../auth/AuthProvider";

type Entry = { id: number; createdAt: string; organizationId: number | null; actorUserId: number | null; actorName: string | null; action: string; operation: string; area: string; method: string; route: string; targetId: string | null; outcome: string; statusCode: number; durationMs: number; requestId: string; remoteAddress: string | null };
type Page = { items: Entry[]; nextBeforeId: number | null; scope: string };
const results: Record<string, { label: string; color: "success" | "error" | "warning" | "default" }> = {
  success: { label: "Sucesso", color: "success" }, failure: { label: "Falha", color: "error" },
  denied: { label: "Acesso recusado", color: "warning" }, cancelled: { label: "Interrompido", color: "default" },
};
function initialFrom() { const date = new Date(Date.now() - 7 * 86400000); return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16); }
export default function ActionAuditPage() {
  const { user } = useAuth();
  const [from, setFrom] = useState(initialFrom), [to, setTo] = useState("");
  const [q, setQ] = useState(""), [userId, setUserId] = useState(""), [outcome, setOutcome] = useState("");
  const [rows, setRows] = useState<Entry[]>([]), [next, setNext] = useState<number | null>(null);
  const [scope, setScope] = useState(""), [busy, setBusy] = useState(false), [error, setError] = useState("");
  const request = useRef<AbortController | null>(null);
  const applied = useRef<Record<string, string | number>>({});
  async function load(more = false) {
    if (!more && (!from || Number.isNaN(Date.parse(from)) || (to && Number.isNaN(Date.parse(to))))) { setError("Informe um período válido."); return; }
    request.current?.abort(); const controller = new AbortController(); request.current = controller;
    setBusy(true); setError("");
    if (!more) {
      setRows([]); setNext(null);
      applied.current = { from: new Date(from).toISOString(), to: to ? new Date(to).toISOString() : new Date().toISOString(), q, outcome, limit: 50 };
      if (userId) applied.current.userId = Number(userId);
    }
    try {
      const { data } = await api.get<Page>("api/audit/actions", { params: { ...applied.current, ...(more && next ? { beforeId: next } : {}) }, signal: controller.signal });
      if (controller.signal.aborted) return;
      setRows(old => more ? [...old, ...data.items] : data.items); setNext(data.nextBeforeId); setScope(data.scope);
    } catch (e: any) {
      if (!controller.signal.aborted) setError(e.response?.data?.error || "Não foi possível consultar o log de ações. Tente novamente.");
    } finally { if (!controller.signal.aborted) setBusy(false); }
  }
  useEffect(() => { void load(); return () => request.current?.abort(); }, [user?.organizationId]);
  function submit(event: FormEvent) { event.preventDefault(); void load(); }
  return <Container maxWidth="xl" className="portal-page" sx={{ py: { xs: 3, md: 4 } }}>
    <Typography variant="h4">Log de ações</Typography>
    <Typography color="text.secondary" mt={1} mb={3}>Pesquise atividades, autenticações e acessos recusados no IA-Portal.</Typography>
    <Paper sx={{ p: { xs: 2, md: 3 }, mb: 3, border: "1px solid", borderColor: "divider" }}>
      <Box component="form" onSubmit={submit}>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={6} md={3}><TextField fullWidth required label="De" type="datetime-local" value={from} onChange={e => setFrom(e.target.value)} InputLabelProps={{ shrink: true }} /></Grid>
          <Grid item xs={12} sm={6} md={3}><TextField fullWidth label="Até (opcional)" type="datetime-local" value={to} onChange={e => setTo(e.target.value)} InputLabelProps={{ shrink: true }} helperText="Vazio: até o momento da pesquisa." /></Grid>
          <Grid item xs={12} sm={6} md={3}><TextField fullWidth label="ID do usuário" type="number" value={userId} onChange={e => setUserId(e.target.value)} inputProps={{ min: 1, step: 1 }} /></Grid>
          <Grid item xs={12} sm={6} md={3}><TextField fullWidth select label="Resultado" value={outcome} onChange={e => setOutcome(e.target.value)}><MenuItem value="">Todos</MenuItem>{Object.entries(results).map(([key, result]) => <MenuItem key={key} value={key}>{result.label}</MenuItem>)}</TextField></Grid>
          <Grid item xs={12} md={9}><TextField fullWidth label="Pesquisar usuário, ação ou recurso" value={q} onChange={e => setQ(e.target.value)} inputProps={{ maxLength: 160 }} /></Grid>
          <Grid item xs={12} md={3}><Button fullWidth type="submit" variant="contained" startIcon={<SearchOutlined />} disabled={busy} sx={{ minHeight: 56 }}>{busy ? "Pesquisando..." : "Pesquisar"}</Button></Grid>
        </Grid>
      </Box>
    </Paper>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    {scope && <Alert severity="info" sx={{ mb: 2 }}>{scope === "installation" ? "Visão da instalação: todas as organizações e tentativas sem autenticação." : "Visão da organização selecionada. Tentativas sem autenticação ficam disponíveis à administração da instalação."} Os registros são somente de consulta e não incluem senhas, tokens ou conteúdo das conversas.</Alert>}
    <Paper sx={{ overflow: "hidden", border: "1px solid", borderColor: "divider" }}>
      <TableContainer><Table sx={{ minWidth: 850 }} aria-label="Registros de ações">
        <TableHead><TableRow><TableCell>Data e hora</TableCell><TableCell>Usuário</TableCell><TableCell>Ação</TableCell><TableCell>Resultado</TableCell><TableCell>Detalhes</TableCell></TableRow></TableHead>
        <TableBody>{rows.map(row => {
          const result = results[row.outcome] || { label: row.outcome, color: "default" as const };
          return <TableRow key={row.id} hover>
            <TableCell sx={{ whiteSpace: "nowrap" }}>{new Date(row.createdAt.endsWith("Z") ? row.createdAt : row.createdAt + "Z").toLocaleString("pt-BR")}<Typography variant="caption" display="block" color="text.secondary">Registro #{row.id}</Typography></TableCell>
            <TableCell>{row.actorName || (row.actorUserId ? `Usuário #${row.actorUserId}` : "Não autenticado")}<Typography variant="caption" display="block" color="text.secondary">{row.actorUserId ? `ID ${row.actorUserId}` : "Identidade não validada"}</Typography></TableCell>
            <TableCell>{row.action}<Typography variant="caption" display="block" color="text.secondary">{row.area}</Typography></TableCell>
            <TableCell><Chip size="small" label={result.label} color={result.color} variant="outlined" /><Typography variant="caption" display="block" mt={0.5}>HTTP {row.statusCode}</Typography></TableCell>
            <TableCell sx={{ maxWidth: 320, overflowWrap: "anywhere" }}><Box component="details"><Typography component="summary" sx={{ cursor: "pointer", color: "primary.main", fontSize: 13 }}>Ver detalhes</Typography><Stack spacing={0.5} mt={1} fontSize={12}>
              <span>{row.method} {row.route}</span><span>Operação: {row.operation}</span>
              {row.targetId && <span>Recurso: {row.targetId}</span>}
              <span>Organização: {row.organizationId ?? "Não identificada"}</span><span>Duração: {row.durationMs} ms</span>
              {row.remoteAddress && <span>Origem da conexão: {row.remoteAddress}</span>}
              <span>Referência: {row.requestId}</span>
            </Stack></Box></TableCell>
          </TableRow>;
        })}{!rows.length && <TableRow><TableCell colSpan={5} align="center" sx={{ py: 5 }}>{busy ? "Consultando registros..." : error ? "A pesquisa não foi concluída." : "Nenhum registro encontrado para os filtros selecionados."}</TableCell></TableRow>}</TableBody>
      </Table></TableContainer>
      <Stack direction="row" alignItems="center" justifyContent="space-between" p={2} gap={2}><Typography color="text.secondary" fontSize={13}>{rows.length} registros carregados · Horário local</Typography>{next && <Button onClick={() => void load(true)} disabled={busy}>{busy ? "Carregando..." : "Carregar mais"}</Button>}</Stack>
    </Paper>
  </Container>;
}
