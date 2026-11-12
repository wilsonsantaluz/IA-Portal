import { useEffect, useRef, useState } from "react";
import { Alert, Box, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import api from "../services/api";
import { useAuth } from "../auth/AuthProvider";

type Source = {id:number;name:string;category:string;status:string};
function defaultDate(source: Source) {
  if (source.category === "demo") return "2026-11-01";
  const date = new Date();
  return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,"0")}-${String(date.getDate()).padStart(2,"0")}`;
}
export default function BusinessSourcePicker({sourceId,referenceDate,onSource,onDate,onChoose,disabled}: {
  sourceId:number|null;referenceDate:string;onSource:(id:number|null)=>void;onDate:(date:string)=>void;onChoose:(question:string)=>void;disabled:boolean
}) {
  const { hasPermission } = useAuth();
  const [sources,setSources] = useState<Source[]>([]);
  const [error,setError] = useState(false);
  const [loaded,setLoaded] = useState(false);
  const initialized = useRef(false);
  const allowed = hasPermission("data.query");
  useEffect(() => { if (!allowed) return; let active=true; api.get("/api/data/sources").then(({data}) => {if(active) {setSources(data);setLoaded(true);}}).catch(() => {if(active) setError(true);});return () => {active=false;}; },[allowed]);
  useEffect(() => {
    if (!allowed || !loaded || disabled || initialized.current) return;
    initialized.current = true;
    if (sourceId === null && sources.length === 1) {
      onSource(sources[0].id);
      onDate(defaultDate(sources[0]));
    }
  }, [allowed, loaded, disabled, sources, sourceId, onSource, onDate]);
  if (!allowed) return null;
  const demo = sources.find(s => s.id===sourceId)?.category==="demo";
  return <Stack spacing={1} sx={{mt:1}}>
    {error && <Alert severity="error">Não foi possível carregar as fontes autorizadas.</Alert>}
    {loaded && sources.length===0 && <Typography variant="caption">Nenhuma fonte analítica foi concedida ao seu grupo. A consulta documental continua disponível.</Typography>}
    {sources.length>0 && <Stack direction={{xs:"column",sm:"row"}} gap={1}>
      <TextField select size="small" label="Fonte de dados" value={sourceId || 0} disabled={disabled} sx={{minWidth:0,width:{xs:"100%",sm:280}}} onChange={e => {
        const id=Number(e.target.value);onSource(id || null);
        const source = sources.find(s => s.id===id);
        onDate(source ? defaultDate(source) : "");
      }}><MenuItem value={0}>Somente documentos</MenuItem>{sources.map(s => <MenuItem key={s.id} value={s.id}>{s.name} · {s.status==="connected" ? "último teste conectado" : "disponibilidade não confirmada"}</MenuItem>)}</TextField>
      {sourceId && <TextField size="small" type="date" label="Data de referência" InputLabelProps={{shrink:true}} value={referenceDate} disabled={disabled} onChange={e => onDate(e.target.value)}/>}
    </Stack>}
    {demo && <>
      <Typography variant="caption">Atlas · dados sintéticos. Referência fixa sugerida: 01/11/2026. Indicadores só são exibidos após consulta.</Typography>
      <Box component="details"><Box component="summary" sx={{cursor:"pointer",fontSize:12}}>Perguntas sugeridas Atlas</Box><Stack direction="row" gap={0.5} flexWrap="wrap">
        {["Qual é a política de cobrança da Atlas?","Mostre o aging por faixa de atraso.","Compare faturamento e recebimentos nos últimos seis meses.","Mostre o valor vencido e explique quais ações a política de cobrança recomenda.","Gere um fluxograma Mermaid para contestação de faturas."].map(q => <Button key={q} size="small" disabled={disabled} onClick={e => {onChoose(q);const details=e.currentTarget.closest("details");if(details)details.open=false;}}>{q}</Button>)}
      </Stack></Box>
    </>}
  </Stack>;
}
