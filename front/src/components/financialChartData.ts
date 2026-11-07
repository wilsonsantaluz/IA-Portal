export type FinancialChart = {title:string;series:string[];rows:({label:string}&Record<string,string|number>)[]};
export function parseFinancialChart(code:string): FinancialChart | null {
  if (code.length>50000) return null;
  try {
    const data=JSON.parse(code);
    if (!data || typeof data.title!=="string" || data.title.length>150 || !Array.isArray(data.series) || data.series.length<1 || data.series.length>4
      || !data.series.every((s:unknown) => typeof s==="string" && /^[a-z][a-z0-9_]{0,50}$/.test(s) && s!=="label")
      || new Set(data.series).size!==data.series.length || !Array.isArray(data.rows) || data.rows.length<1 || data.rows.length>100) return null;
    const rows=[];
    for (const row of data.rows) {
      if (!row || typeof row.label!=="string" || row.label.length>100 || !row.values || typeof row.values!=="object") return null;
      const item: {label:string}&Record<string,string|number>={label:row.label};
      for (const series of data.series) {
        const value=row.values[series];
        if (typeof value!=="number" || !Number.isFinite(value) || Math.abs(value)>1e15) return null;
        item[series]=value;
      }
      rows.push(item);
    }
    return {title:data.title,series:data.series,rows};
  } catch { return null; }
}
export function allowedMermaid(code:string) {
  return code.length<=20000 && /^(flowchart|graph|sequenceDiagram|classDiagram|stateDiagram(?:-v2)?|erDiagram|gantt|pie|journey|xychart-beta|mindmap|timeline)\b/.test(code.trim())
    && !/%%\{|<\/?(?:script|iframe|object|embed|foreignObject)|^\s*click\s/im.test(code);
}
