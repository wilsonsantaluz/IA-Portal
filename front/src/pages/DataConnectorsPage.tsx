import { useEffect, useState } from "react";
import { Alert, Box, Button, Checkbox, Chip, CircularProgress, FormControlLabel, MenuItem, Paper, Stack, TextField, Typography } from "@mui/material";
import api from "../services/api";

type Profile = { id: string; category: string; endpoint: string; catalog: string; schema: string; tables: string[]; license?: string; sourceUrl?: string };
type Connector = { id: number; name: string; profileId: string; category: string; endpoint: string; catalog: string; schema: string; tables: string[]; enabled: boolean; groupIds: number[]; status: string; testedAt?: string; version: number };
const categories: Record<string,string> = { demo: "Demo Dataset", enterprise: "Enterprise Connector", public: "Public Data Connector" };
const statuses: Record<string,string> = { not_tested: "Não testada", connected: "Conectada", timeout: "Tempo limite", unavailable: "Indisponível", not_configured: "Revisar configuração" };
export default function DataConnectorsPage() {
  const [rows,setRows] = useState<Connector[]>([]);
  const [profiles,setProfiles] = useState<Profile[]>([]);
  const [groups,setGroups] = useState<{id:number;name:string}[]>([]);
  const [editing,setEditing] = useState<Connector | null>(null);
  const [name,setName] = useState("");
  const [profileId,setProfileId] = useState("");
  const [tables,setTables] = useState<string[]>([]);
  const [groupIds,setGroupIds] = useState<number[]>([]);
  const [enabled,setEnabled] = useState(false);
  const [busy,setBusy] = useState(false);
  const [error,setError] = useState("");
  const [metadata,setMetadata] = useState<{table_name:string;column_name:string;data_type:string}[] | null>(null);
  const profile = profiles.find(p => p.id === profileId);
  async function load() {
    const [connections,sources,roles] = await Promise.all([api.get("/api/data-connectors"),api.get("/api/data-connectors/profiles"),api.get("/api/data-connectors/groups")]);
    setRows(connections.data); setProfiles(sources.data); setGroups(roles.data);
  }
  async function perform(action: () => Promise<void>) {
    setBusy(true);setError("");
    try { await action(); } catch (e) { setError((e as {response?:{data?:{error?:string}}}).response?.data?.error || "Não foi possível concluir a operação."); }
    finally { setBusy(false); }
  }
  useEffect(() => { void perform(load); },[]);
  function edit(row: Connector | null) {
    setEditing(row);setName(row?.name || "");setProfileId(row?.profileId || "");setTables(row?.tables || []);setGroupIds(row?.groupIds || []);setEnabled(row?.enabled || false);setMetadata(null);
  }
  async function save() {
    const payload = { name,profileId,tables,groupIds,enabled,version:editing?.version || 0 };
    if (editing) await api.put(`/api/data-connectors/${editing.id}`,payload); else await api.post("/api/data-connectors",payload);
    edit(null);await load();
  }
  return <Box sx={{p:{xs:2,md:3},maxWidth:1200,mx:"auto"}}>
    <Stack spacing={2}>
      <Typography component="h2" variant="h5">Data Connectors</Typography>
      <Typography color="text.secondary">Fontes da organização ativa. Catálogos, schemas e destinos precisam estar aprovados no servidor; credenciais permanecem no ambiente.</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {busy && <CircularProgress size={24} aria-label="Processando conectores" />}
      {!busy && profiles.length === 0 && <Alert severity="info">Nenhuma fonte aprovada está configurada para esta organização. Configure TRINO_CONNECTORS_FILE no gateway para cadastrar uma conexão.</Alert>}
      {rows.length === 0 && !busy && <Typography>Nenhuma conexão cadastrada.</Typography>}
      {rows.map(row => <Paper key={row.id} variant="outlined" sx={{p:2}}>
        <Stack spacing={1}>
          <Typography variant="h6">{row.name}</Typography>
          <Typography variant="body2">{categories[row.category]} · {row.catalog}.{row.schema}</Typography>
          <Stack direction="row" gap={1} flexWrap="wrap"><Chip size="small" label={row.enabled ? "Habilitada" : "Desabilitada"}/><Chip size="small" label={statuses[row.status] || "Desconhecido"}/></Stack>
          <Typography variant="caption">Último teste: {row.testedAt ? new Date(row.testedAt).toLocaleString("pt-BR") : "não realizado"}. O teste não garante disponibilidade futura.</Typography>
          <Stack direction="row" gap={1} flexWrap="wrap">
            <Button disabled={busy} onClick={() => edit(row)}>Editar</Button>
            <Button disabled={busy} onClick={() => void perform(async () => {await api.post(`/api/data-connectors/${row.id}/test`);await load();})}>Testar conexão</Button>
            <Button disabled={busy} onClick={() => void perform(async () => {setMetadata((await api.get(`/api/data-connectors/${row.id}/metadata`)).data);})}>Descobrir tabelas e colunas</Button>
          </Stack>
        </Stack>
      </Paper>)}
      {metadata && <Paper variant="outlined" sx={{p:2,overflowX:"auto"}}><Typography variant="h6">Metadados autorizados</Typography>{metadata.length === 0 ? <Typography>Nenhuma tabela encontrada.</Typography> : <table><thead><tr><th>Tabela</th><th>Coluna</th><th>Tipo</th></tr></thead><tbody>{metadata.map((c,i) => <tr key={i}><td>{c.table_name}</td><td>{c.column_name}</td><td>{c.data_type}</td></tr>)}</tbody></table>}</Paper>}
      {profiles.length > 0 && <Paper variant="outlined" sx={{p:2}}><Stack spacing={2}>
        <Typography variant="h6">{editing ? "Editar conexão" : "Cadastrar conexão"}</Typography>
        <TextField label="Nome" value={name} onChange={e => setName(e.target.value)} inputProps={{maxLength:100}}/>
        <TextField select label="Fonte aprovada" value={profileId} onChange={e => {setProfileId(e.target.value);setTables([]);}}>{profiles.map(p => <MenuItem key={p.id} value={p.id}>{p.id} · {categories[p.category]}</MenuItem>)}</TextField>
        {profile && <>
          <TextField label="Endpoint" value={profile.endpoint} InputProps={{readOnly:true}}/>
          <Stack direction={{xs:"column",sm:"row"}} gap={2}><TextField label="Catálogo" value={profile.catalog} InputProps={{readOnly:true}}/><TextField label="Schema" value={profile.schema} InputProps={{readOnly:true}}/></Stack>
          {profile.category === "public" && <Typography>Origem: {profile.sourceUrl} · Licença: {profile.license}</Typography>}
          <Typography fontWeight={600}>Tabelas disponíveis para IA</Typography>
          <Box>{profile.tables.map(t => <FormControlLabel key={t} label={t} control={<Checkbox checked={tables.includes(t)} onChange={(_,checked) => setTables(checked ? [...tables,t] : tables.filter(x => x !== t))}/>}/>)}</Box>
        </>}
        <Typography fontWeight={600}>Grupos autorizados</Typography>
        <Box>{groups.map(g => <FormControlLabel key={g.id} label={g.name} control={<Checkbox checked={groupIds.includes(g.id)} onChange={(_,checked) => setGroupIds(checked ? [...groupIds,g.id] : groupIds.filter(x => x !== g.id))}/>}/>)}</Box>
        <Typography variant="caption">O grupo também precisa da permissão “Consultar fontes” (data.query). Nenhum grupo ganha acesso automaticamente.</Typography>
        <FormControlLabel label="Habilitar conexão" control={<Checkbox checked={enabled} onChange={(_,checked) => setEnabled(checked)}/>}/>
        <Stack direction="row" gap={1}><Button variant="contained" disabled={busy || !profileId || !name.trim()} onClick={() => void perform(save)}>Salvar</Button><Button disabled={busy} onClick={() => edit(null)}>Limpar</Button></Stack>
      </Stack></Paper>}
    </Stack>
  </Box>;
}
