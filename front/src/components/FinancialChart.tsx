import { Alert, Box, Typography } from "@mui/material";
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { parseFinancialChart } from "./financialChartData";

const brl=new Intl.NumberFormat("pt-BR",{style:"currency",currency:"BRL"});
const colors=["var(--portal-primary)","var(--portal-cyan)","var(--portal-violet)","var(--portal-muted)"];
export default function FinancialChart({code}:{code:string}) {
  const chart=parseFinancialChart(code);
  if (!chart) return <Box><Alert severity="warning">Gráfico inválido. Consulte a tabela da resposta.</Alert><pre>{code}</pre></Box>;
  return <Box role="figure" aria-label={chart.title} sx={{my:2,minWidth:0}}>
    <Typography fontWeight={600}>{chart.title}</Typography>
    <ResponsiveContainer width="100%" height={280}>
      <BarChart data={chart.rows} margin={{left:15,right:15,bottom:15}}><CartesianGrid stroke="var(--portal-muted)" strokeDasharray="3 3"/><XAxis dataKey="label" tick={{fontSize:11,fill:"var(--portal-muted)"}}/><YAxis tickFormatter={v => brl.format(Number(v))} width={110} tick={{fontSize:10,fill:"var(--portal-muted)"}}/><Tooltip formatter={v => brl.format(Number(v))} contentStyle={{background:"var(--portal-surface)",color:"var(--portal-text)"}}/><Legend/>{chart.series.map((s,i) => <Bar key={s} dataKey={s} fill={colors[i]} isAnimationActive={false} name={s.replace(/_centavos$/, " (BRL)")}/>)}</BarChart>
    </ResponsiveContainer>
    <details><summary>Valores do gráfico em BRL</summary><table><thead><tr><th>Referência</th>{chart.series.map(s => <th key={s}>{s.replace(/_centavos$/, " (BRL)")}</th>)}</tr></thead><tbody>{chart.rows.map((r,i) => <tr key={i}><td>{r.label}</td>{chart.series.map(s => <td key={s}>{brl.format(Number(r[s]))}</td>)}</tr>)}</tbody></table></details>
  </Box>;
}
