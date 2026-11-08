import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Divider, MenuItem, Stack, TextField, Typography } from "@mui/material";
import { Link } from "react-router-dom";
import api from "../services/api";
import { useAuth } from "../auth/AuthProvider";
import OrganizationForm from "../components/OrganizationForm";
import OrganizationInvitations from "../components/OrganizationInvitations";
import { aiError, type ProviderOption } from "../components/ConversationModelPicker";

type Organization = { id: number; name: string; slug: string; policyRole: string | null };
type Member = { userId: number; userName: string; policyRole: string | null; isActive: boolean; version: number; accountActive?: boolean };
type Defaults = { provider: string | null; model: string | null; version: number; inherited: boolean };
type Template = { id: string; name: string; description: string; version: string; exampleQuestions: string[] };
export default function OrganizationsPage() {
  const { user, hasPermission, refresh } = useAuth();
  const [organizations, setOrganizations] = useState<Organization[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [providers, setProviders] = useState<ProviderOption[]>([]);
  const [defaults, setDefaults] = useState<Defaults | null>(null);
  const [templates, setTemplates] = useState<Template[]>([]);
  const [choice, setChoice] = useState("");
  const [transferOpen, setTransferOpen] = useState(false); const [targetId, setTargetId] = useState("");
  const [busy, setBusy] = useState(false); const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const canManage = hasPermission("organizations.manage"), canMembers = hasPermission("access.manage");
  const load = useCallback(async () => {
    const [orgs, settings] = await Promise.all([api.get<Organization[]>("api/organizations"), api.get<Defaults>("api/organizations/current/ai-default")]);
    setOrganizations(orgs.data); setDefaults(settings.data);
    if (canMembers) setMembers((await api.get<Member[]>("api/organizations/current/members")).data);
    if (hasPermission("chat.access")) setProviders((await api.get<ProviderOption[]>("api/ai/providers")).data);
    if (hasPermission("kb.view")) setTemplates((await api.get<Template[]>("api/knowledge-templates")).data);
  }, [canMembers, user?.organizationId]);
  useEffect(() => { void load().catch(e => setError(aiError(e))); }, [load]);
  const active = organizations.find(o => o.id === user?.organizationId);
  const currentMember = members.find(m => m.userId === user?.userId);
  const transferTargets = members.filter(m => m.isActive && m.accountActive !== false && m.userId !== user?.userId && m.policyRole !== "Owner");
  async function perform(action: () => Promise<unknown>, message: string) {
    setBusy(true); setError(""); setNotice("");
    try { await action(); await refresh(); await load(); setNotice(message); return true; } catch (e) { setError(aiError(e)); return false; } finally { setBusy(false); }
  }
  return <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 1040, mx: "auto", width: "100%", overflowY: "auto" }}>
    <Stack gap={2}>
      <Typography variant="h5" component="h2">Organizações</Typography>
      {error && <Alert severity="error">{error}</Alert>}{notice && <Alert severity="success">{notice}</Alert>}
      <Typography>Organização ativa: <strong>{active?.name || "Carregando…"}</strong></Typography>
      <Typography variant="body2" color="text.secondary">Seu papel: {active?.policyRole || "Grupos personalizados"}. Use o seletor do topo para trocar de organização.</Typography>
      <Box component="ul" sx={{ pl: 2.5, overflowWrap: "anywhere" }}>{organizations.map(o => <li key={o.id}>{o.name} · {o.slug}</li>)}</Box>
      {hasPermission("organizations.create") && <>
        <Divider /><Typography variant="h6" component="h3">Nova organização</Typography>
        <OrganizationForm busy={busy} onCreate={async (name, slug) => {
          return await perform(async () => { await api.post("api/organizations", { name, slug }); window.dispatchEvent(new Event("organizations-changed")); }, "Organização criada. Selecione-a no topo para configurar sua base.");
        }} />
      </>}
      {canManage && <>
        <Divider /><Typography variant="h6" component="h3">Modelo padrão desta organização</Typography>
        <Typography variant="body2">{defaults?.inherited ? "Herdando o padrão da instalação." : `${defaults?.provider} · ${defaults?.model}`} Preferências pessoais são separadas por organização. Conversas existentes mantêm o modelo escolhido.</Typography>
        {providers.length === 0 ? <Alert severity="info">Nenhum modelo disponível. Configure um provedor na administração da instalação.</Alert> : <Stack direction={{ xs: "column", sm: "row" }} gap={1}>
          <TextField select label="Modelo padrão da organização" size="small" value={choice} onChange={e => setChoice(e.target.value)} disabled={busy} sx={{ minWidth: 0, flex: 1 }}>
            {providers.flatMap(p => p.models.map(model => <MenuItem key={p.id + model} value={JSON.stringify({ provider: p.id, model })}>{p.id} · {model}</MenuItem>))}
          </TextField>
          <Button disabled={busy || !choice || !defaults} onClick={() => void perform(() => api.put("api/organizations/current/ai-default", { ...JSON.parse(choice), version: defaults!.version }), "Modelo padrão atualizado.")}>Salvar padrão</Button>
        </Stack>}
      </>}
      {canMembers && <>
        <Divider /><Typography variant="h6" component="h3">Membros e papéis</Typography>
        <Typography variant="body2">Somente Owner pode alterar papéis ou desativar associações. O último Owner ativo é protegido. Grupos personalizados continuam disponíveis para o acesso legado.</Typography>
        <Button component={Link} to="/users" sx={{ alignSelf: "flex-start" }}>Cadastrar usuários nesta organização</Button>
        {members.map(m => <Stack key={m.userId} direction={{ xs: "column", sm: "row" }} gap={1} alignItems={{ sm: "center" }}>
          <Typography sx={{ flex: 1, overflowWrap: "anywhere" }}>{m.userName} · {m.isActive ? "Ativo" : "Associação desativada"}</Typography>
          <TextField select size="small" label={`Papel de ${m.userName}`} value={m.policyRole || "legacy"} disabled={busy || active?.policyRole !== "Owner"} sx={{ minWidth: 140 }}
            onChange={e => void perform(() => api.put(`api/organizations/current/members/${m.userId}`, { role: e.target.value, isActive: m.isActive, version: m.version }), "Papel atualizado.")}>
            {!m.policyRole && <MenuItem value="legacy" disabled>Grupos personalizados</MenuItem>}
            {(m.policyRole === "Owner" ? ["Owner", "Admin", "Member", "Viewer"] : ["Admin", "Member", "Viewer"]).map(role => <MenuItem key={role} value={role}>{role}</MenuItem>)}
          </TextField>
          <Button disabled={busy || active?.policyRole !== "Owner"} onClick={() => void perform(() => api.put(`api/organizations/current/members/${m.userId}`, { role: m.policyRole || "Member", isActive: !m.isActive, version: m.version }), "Associação atualizada.")}>{m.isActive ? "Desativar associação" : "Reativar associação"}</Button>
        </Stack>)}
        {active?.policyRole === "Owner" && <Button sx={{alignSelf:"flex-start"}} disabled={busy || transferTargets.length === 0} onClick={() => {setTargetId("");setTransferOpen(true);}}>Transferir propriedade</Button>}
      </>}
      {active?.policyRole === "Owner" && <><Divider /><OrganizationInvitations /></>}
      {hasPermission("kb.manage") && <>
        <Divider /><Typography variant="h6" component="h3">Templates de conhecimento</Typography>
        {templates.map(t => <Box key={t.id}>
          <Typography fontWeight={600}>{t.name} · {t.version}</Typography>
          <Typography variant="body2">{t.description} A importação preserva documentos existentes e não concede ferramentas ou acesso a fontes.</Typography>
          <Button disabled={busy} onClick={() => void perform(() => api.post(`api/knowledge-templates/${t.id}/apply`), "Template importado. Confira os documentos na base e teste as perguntas no chat.")}>Importar template de suporte</Button>
          <Box component="ul" sx={{pl:2.5}}>{t.exampleQuestions.map(q=><li key={q}>{q}</li>)}</Box>
        </Box>)}
      </>}
    </Stack>
    <Dialog open={transferOpen} onClose={() => !busy && setTransferOpen(false)} aria-labelledby="ownership-transfer-title">
      <DialogTitle id="ownership-transfer-title">Transferir propriedade da organização?</DialogTitle>
      <DialogContent>
        <Typography sx={{mb:2}}>O membro selecionado se tornará Owner e poderá gerenciar todos os membros e convites. Você continuará como Admin. A alteração vale somente para esta organização.</Typography>
        {active?.id === 1 && <Alert severity="warning" sx={{mb:2}}>A organização padrão também controla as configurações globais de IA. O novo Owner terá privilégios de administração da instalação.</Alert>}
        <TextField select fullWidth label="Novo Owner" value={targetId} onChange={e=>setTargetId(e.target.value)} disabled={busy}>
          {transferTargets.map(m=><MenuItem key={m.userId} value={String(m.userId)}>{m.userName}</MenuItem>)}
        </TextField>
      </DialogContent>
      <DialogActions><Button disabled={busy} onClick={()=>setTransferOpen(false)}>Cancelar</Button>
        <Button variant="contained" disabled={busy || !targetId || !currentMember} onClick={()=>void (async()=>{
          const target=transferTargets.find(m=>m.userId===Number(targetId));if(!target || !currentMember)return;
          const ok=await perform(()=>api.post("api/organizations/current/transfer-ownership",{targetUserId:target.userId,ownerVersion:currentMember.version,targetVersion:target.version}),"Propriedade transferida. Você agora é Admin nesta organização.");
          if(ok)setTransferOpen(false);
        })()}>Confirmar transferência</Button>
      </DialogActions>
    </Dialog>
  </Box>;
}
