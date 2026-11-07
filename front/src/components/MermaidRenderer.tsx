import { useEffect, useState } from "react";
import { Alert, Box } from "@mui/material";
import mermaid from "mermaid";
import { allowedMermaid } from "./financialChartData";

mermaid.initialize({startOnLoad:false,securityLevel:"strict",suppressErrorRendering:true,flowchart:{htmlLabels:false}});
let sequence=0;
export default function MermaidRenderer({code}:{code:string}) {
  const [source,setSource]=useState("");
  const [failed,setFailed]=useState(false);
  useEffect(() => {
    let active=true;setSource("");setFailed(false);
    if (!allowedMermaid(code)) {setFailed(true);return;}
    mermaid.render(`portal-diagram-${sequence++}`,code).then(({svg}) => {
      if (!active) return;
      // Strict Mermaid parsing plus an opaque-origin frame with no scripts or network.
      const document=new DOMParser().parseFromString(svg,"image/svg+xml");
      if (document.querySelector("parsererror")) throw new Error("Invalid SVG");
      document.querySelectorAll("script,foreignObject,iframe,object,embed,form,a").forEach(node => node.remove());
      document.querySelectorAll("*").forEach(node => Array.from(node.attributes).forEach(attribute => {
        if (/^on/i.test(attribute.name) || (/href$/i.test(attribute.name) && !attribute.value.startsWith("#"))) node.removeAttribute(attribute.name);
      }));
      const safe=new XMLSerializer().serializeToString(document.documentElement);
      setSource(`<!doctype html><html lang="pt-BR"><head><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'none'; style-src 'unsafe-inline'; img-src data:; base-uri 'none'; form-action 'none'"><style>body{margin:0;background:#fff}svg{max-width:100%;height:auto}</style></head><body>${safe}</body></html>`);
    }).catch(() => {if(active) setFailed(true);});
    return () => {active=false;};
  },[code]);
  return <Box className="mermaid-container" sx={{my:2}}>
    {failed ? <><Alert severity="warning">Não foi possível renderizar o diagrama. Código original:</Alert><pre>{code}</pre></>
      : source ? <iframe title="Diagrama Mermaid" sandbox="" referrerPolicy="no-referrer" srcDoc={source} style={{width:"100%",height:420,border:0,borderRadius:8}}/>
      : <span role="status">Preparando diagrama…</span>}
  </Box>;
}
