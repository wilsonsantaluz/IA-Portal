import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Chip, CircularProgress, Divider, FormControlLabel, MenuItem, Paper, Stack, Switch, TextField, Typography } from "@mui/material";
import RefreshOutlined from "@mui/icons-material/RefreshOutlined";
import { Link } from "react-router-dom";
import api from "../services/api";
import { useAuth } from "../auth/AuthProvider";
import { aiError, type ProviderOption } from "../components/ConversationModelPicker";

type Provider = ProviderOption & { enabled: boolean; configured: boolean; endpoint: string; version: number; keyEnvironmentVariable: string; configurationError?: string };
type Selection = { provider: string; model: string };
type GlobalDefault = Selection & { version: number };
const names: Record<string, string> = { deepseek: "DeepSeek", openai: "OpenAI", ollama: "Ollama", litellm: "LiteLLM" };
const value = (selection: Selection) => JSON.stringify({ provider: selection.provider, model: selection.model });
const surface = { p: { xs: 2, md: 3 }, border: "1px solid", borderColor: "divider", borderRadius: 3, boxShadow: "var(--portal-shadow)" };

function PersonalPreference() {
  const [providers, setProviders] = useState<ProviderOption[]>([]);
  const [selected, setSelected] = useState("");
  const [busy, setBusy] = useState(true);
  const [feedback, setFeedback] = useState<{ error: boolean; text: string } | null>(null);
  useEffect(() => { let active = true; Promise.all([api.get<ProviderOption[]>("api/ai/providers"), api.get<Selection>("api/me/ai-preference")]).then(([p, s]) => { if (active) { setProviders(p.data); setSelected(value(s.data)); } }).catch(e => { if (active) setFeedback({ error: true, text: aiError(e) }); }).finally(() => { if (active) setBusy(false); }); return () => { active = false; }; }, []);
  const options = providers.flatMap(p => p.models.map(model => ({ value: value({ provider: p.id, model }), label: `${names[p.id] || p.id} · ${model}` })));
  async function save() { setBusy(true); setFeedback(null); try { await api.put("api/me/ai-preference", JSON.parse(selected)); setFeedback({ error: false, text: "Preferência salva. Será usada nas novas conversas." }); } catch (e) { setFeedback({ error: true, text: aiError(e) }); } finally { setBusy(false); } }
  return <Paper sx={surface}>
    <Typography component="h2" variant="h6">Seu modelo padrão</Typography>
    <Typography color="text.secondary" variant="body2" sx={{ mt: 1, mb: 2 }}>Escolha entre os modelos habilitados. Conversas existentes mantêm sua própria seleção.</Typography>
    {feedback && <Alert severity={feedback.error ? "error" : "success"} sx={{ mb: 2 }}>{feedback.text}</Alert>}
    {!busy && !options.length && <Alert severity="info" sx={{ mb: 2 }}>Nenhum provedor disponível. Solicite a configuração ao administrador.</Alert>}
    <Stack direction={{ xs: "column", sm: "row" }} gap={2}>
      <TextField select fullWidth size="small" label="Modelo padrão pessoal" value={selected} disabled={busy} onChange={e => setSelected(e.target.value)}>
        {selected && !options.some(o => o.value === selected) && <MenuItem value={selected} disabled>Preferência atual indisponível</MenuItem>}
        {options.map(o => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
      </TextField>
      <Button variant="contained" disabled={busy || !options.some(o => o.value === selected)} onClick={() => void save()} sx={{ flexShrink: 0 }}>Salvar preferência</Button>
    </Stack>
  </Paper>;
}

function ProviderCard({ provider, updated }: { provider: Provider; updated: (saved: Provider) => void }) {
  const [enabled, setEnabled] = useState(provider.enabled);
  const [endpoint, setEndpoint] = useState(provider.endpoint);
  const [models, setModels] = useState(provider.models.join(", "));
  const [defaultModel, setDefaultModel] = useState(provider.defaultModel);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [test, setTest] = useState<{ success: boolean; status: string; testedAt: string } | null>(null);
  useEffect(() => { setEnabled(provider.enabled); setEndpoint(provider.endpoint); setModels(provider.models.join(", ")); setDefaultModel(provider.defaultModel); setTest(null); setError(""); }, [provider]);
  const choices = [...new Set(models.split(",").map(m => m.trim()).filter(Boolean))];
  const dirty = enabled !== provider.enabled || endpoint !== provider.endpoint || choices.join(",") !== provider.models.join(",") || defaultModel !== provider.defaultModel;
  async function save() { setBusy(true); setError(""); try { const r = await api.put<Provider>(`api/admin/ai/providers/${provider.id}`, { enabled, endpoint, models: choices, defaultModel, version: provider.version }); updated(r.data); } catch (e) { setError(aiError(e)); } finally { setBusy(false); } }
  async function check() { setBusy(true); setError(""); try { const r = await api.post(`api/admin/ai/providers/${provider.id}/test`); setTest(r.data); } catch (e) { setError(aiError(e)); } finally { setBusy(false); } }
  const statuses: Record<string, string> = { connected: "Conectividade confirmada", authentication: "Credencial recusada", not_configured: "Chave não configurada", invalid_endpoint: "Endpoint inválido", rate_limit: "Limite de requisições atingido", timeout: "Tempo limite atingido", unavailable: "Serviço indisponível", invalid_response: "Resposta de diagnóstico inválida" };
  return <Paper sx={surface} component="section" aria-label={`Provedor ${names[provider.id]}`}>
    <Stack direction="row" justifyContent="space-between" alignItems="center" gap={1} flexWrap="wrap">
      <Typography component="h3" variant="h6">{names[provider.id]}</Typography>
      <Chip size="small" color={provider.configured ? "success" : "warning"} variant="outlined" sx={{ color: "text.primary" }} label={provider.id === "ollama" ? "Chave opcional" : provider.configured ? "Chave configurada" : "Chave ausente"} />
    </Stack>
    <FormControlLabel control={<Switch checked={enabled} disabled={busy} onChange={e => setEnabled(e.target.checked)} />} label={`Habilitar ${names[provider.id]}`} />
    <Stack gap={2} sx={{ mt: 1 }}>
      {(error || provider.configurationError) && <Alert severity="error">{error || provider.configurationError}</Alert>}
      <TextField label={`Endpoint ${names[provider.id]}`} value={endpoint} disabled={busy} onChange={e => setEndpoint(e.target.value)} fullWidth size="small" helperText="O destino precisa estar autorizado no servidor." />
      <TextField label={`Modelos permitidos ${names[provider.id]}`} value={models} disabled={busy} onChange={e => setModels(e.target.value)} fullWidth multiline minRows={2} helperText="Identificadores separados por vírgula. Informe modelos disponíveis na conta ou instalados no servidor." />
      <TextField select label={`Modelo padrão ${names[provider.id]}`} value={defaultModel} disabled={busy} onChange={e => setDefaultModel(e.target.value)} size="small">
        <MenuItem value="">Nenhum</MenuItem>
        {defaultModel && !choices.includes(defaultModel) && <MenuItem value={defaultModel} disabled>{defaultModel} (fora da lista)</MenuItem>}
        {choices.map(m => <MenuItem key={m} value={m}>{m}</MenuItem>)}
      </TextField>
      <Typography variant="caption" color="text.secondary">{provider.apiBilling === "local" ? "Execução local: sem taxa de API externa; infraestrutura não incluída." : "Custos dependem do endpoint/modelo e dos preços configurados."} Credencial: {provider.keyEnvironmentVariable}, somente no servidor.</Typography>
      <Stack direction="row" gap={1} flexWrap="wrap"><Button variant="contained" disabled={busy || !dirty} onClick={() => void save()}>Salvar {names[provider.id]}</Button><Button variant="outlined" disabled={busy || dirty} onClick={() => void check()}>Testar {names[provider.id]}</Button></Stack>
      <Typography variant="caption" color="text.secondary">O teste verifica a configuração salva, sem gerar texto ou instalar modelos.</Typography>
      {test && <Alert severity={test.success ? "success" : "warning"}>{statuses[test.status] || "Falha no teste"}. {new Date(test.testedAt).toLocaleString("pt-BR")}. {test.success && "Disponibilidade dos modelos não verificada."}</Alert>}
    </Stack>
  </Paper>;
}

export default function AiSettingsPage() {
  const { hasPermission } = useAuth(); const admin = hasPermission("ai.manage"); const personal = hasPermission("chat.access");
  const [providers, setProviders] = useState<Provider[]>([]);
  const [global, setGlobal] = useState<GlobalDefault | null>(null);
  const [selected, setSelected] = useState("");
  const [loading, setLoading] = useState(admin);
  const [error, setError] = useState(""); const [notice, setNotice] = useState(""); const [revision, setRevision] = useState(0);
  const load = useCallback(async () => { if (!admin) return; setLoading(true); setError(""); try { const [p, g] = await Promise.all([api.get<Provider[]>("api/admin/ai/providers"), api.get<GlobalDefault>("api/admin/ai/default")]); setProviders(p.data); setGlobal(g.data); setSelected(value(g.data)); setRevision(r => r + 1); } catch (e) { setError(aiError(e)); } finally { setLoading(false); } }, [admin]);
  useEffect(() => { void load(); }, [load]);
  const options = providers.filter(p => p.enabled && p.configured && !p.configurationError).flatMap(p => p.models.map(model => ({ value: value({ provider: p.id, model }), label: `${names[p.id]} · ${model}` })));
  async function saveGlobal() { if (!global) return; setLoading(true); setError(""); try { await api.put("api/admin/ai/default", { ...JSON.parse(selected), version: global.version }); setNotice("Padrão global salvo. Preferências pessoais e conversas existentes foram preservadas."); await load(); } catch (e) { setError(aiError(e)); } finally { setLoading(false); } }
  return <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 1180, mx: "auto", width: "100%" }}>
    <Stack direction="row" justifyContent="space-between" alignItems="start" gap={2} sx={{ mb: 3 }}><Box><Typography color="text.secondary" variant="overline">Configurações</Typography><Typography variant="h5" component="h2" fontWeight={700}>Inteligência Artificial</Typography><Typography color="text.secondary" sx={{ mt: 1 }}>Modelos, integrações e suas preferências de conversa.</Typography></Box>{admin && <Button startIcon={<RefreshOutlined />} disabled={loading} onClick={() => void load()}>Atualizar</Button>}</Stack>
    <Stack gap={3}>
      {error && <Alert severity="error">{error}</Alert>}{notice && <Alert severity="success" onClose={() => setNotice("")}>{notice}</Alert>}
      {personal && <PersonalPreference key={revision} />}
      {admin && <><Divider /><Alert severity="info">Chaves de API são configuradas por variáveis de ambiente no servidor. Seus valores nunca são enviados à interface nem salvos em texto puro no banco.</Alert>
        {loading && <Box display="flex" gap={1} alignItems="center"><CircularProgress size={20} /><Typography>Carregando configurações…</Typography></Box>}
        <Paper sx={surface}><Typography component="h2" variant="h6" sx={{ mb: 2 }}>Padrão global para novas conversas</Typography><Stack direction={{ xs: "column", sm: "row" }} gap={2}>
          <TextField select fullWidth size="small" label="Modelo padrão global" value={selected} disabled={loading} onChange={e => setSelected(e.target.value)}>{selected && !options.some(o => o.value === selected) && <MenuItem value={selected} disabled>Padrão atual indisponível</MenuItem>}{options.map(o => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}</TextField>
          <Button variant="contained" disabled={loading || !global || !options.some(o => o.value === selected)} onClick={() => void saveGlobal()} sx={{ flexShrink: 0 }}>Salvar padrão global</Button>
        </Stack></Paper>
        <Box display="grid" gridTemplateColumns={{ xs: "1fr", lg: "repeat(2, minmax(0, 1fr))" }} gap={3}>{providers.map(p => <ProviderCard key={p.id} provider={p} updated={saved => { setNotice("Configuração salva."); setProviders(prev => prev.map(item => item.id === saved.id ? saved : item)); setRevision(r => r + 1); }} />)}</Box></>}
      {(personal || hasPermission("usage.admin")) && <Button component={Link} to="/usage" sx={{ alignSelf: "start" }}>Consultar consumo de IA</Button>}
    </Stack>
  </Box>;
}
