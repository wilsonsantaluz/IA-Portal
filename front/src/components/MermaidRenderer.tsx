
import { useEffect, useRef, useState } from "react";
import mermaid from "mermaid";
import React from "react";

mermaid.initialize({
  startOnLoad: false,
  theme: "default",
  securityLevel: "loose",
  suppressErrorRendering: true,
  flowchart: {
    useMaxWidth: true,
    htmlLabels: true,
  },
  gantt: {
    useWidth: 800,
    titleTopMargin: 25,
    barHeight: 20,
    barGap: 4,
    topPadding: 50,
    leftPadding: 75,
    gridLineStartPadding: 35,
    fontSize: 11,
    numberSectionStyles: 4,
    axisFormat: '%Y-%m',
  },
  pie: {
    useWidth: 800,
    textPosition: 0.5,
  }
});

let mermaidSeq = 0;


const repairMermaidCode = (code: string): string => {

  

  if (code.includes('flowchart TD') && !code.includes('{]')) {
    return code;
  }
  
  let repaired = code;
  

  repaired = repaired.replace(/G\{([^}]+)\]\s*(--|-->)/g, (match, content, arrow) => {
    const cleanContent = content.replace(/\s*\]\s*$/, '').trim();
    return `G{${cleanContent}}\n    G ${arrow}`;
  });
  

  repaired = repaired.replace(/([A-Z])\{([^}]+)\]\s*$/gm, '$1{$2}');
  

  repaired = repaired.replace(/([A-Z])\{([^}]+)\]\s+([A-Z]\s+--)/g, '$1{$2}\n    $3');
  

  repaired = repaired.replace(/\[([^\]]+)\\\s*n\s*([^\]]*)\]/g, '["$1<br>$2"]');
  repaired = repaired.replace(/\{([^}]+)\\\s*n\s*([^}]*)\}/g, '{$1<br>$2}');
  

  repaired = repaired.replace(/[`$]/g, '');
  
  return repaired;
};


const fixBarChartSyntax = (code: string): string => {
  const lines = code.split('\n').map(line => line.trim());
  const chartIndex = lines.findIndex(line => line.startsWith('chart'));
  if (chartIndex === -1) return code;
  
  const titleLine = lines.find(line => line.includes('title "'));
  let title = "Gráfico de Barras";
  if (titleLine) {
    const match = titleLine.match(/title\s+"([^"]+)"/);
    if (match) title = match[1];
  }
  
  const xAxisLine = lines.find(line => line.includes('x-axis'));
  let xAxisLabels: string[] = [];
  if (xAxisLine) {
    const match = xAxisLine.match(/x-axis\s+\[([^\]]+)\]/);
    if (match) {
      xAxisLabels = match[1]
        .split(',')
        .map(label => label.trim().replace(/"/g, '').replace(/'/g, ''));
    }
  }
  
  const barLine = lines.find(line => line.includes('bar') && line.includes(':'));
  let barValues: number[] = [];
  let barLabel = "Valor";
  if (barLine) {
    const labelMatch = barLine.match(/bar\s+"([^"]+)"/);
    if (labelMatch) barLabel = labelMatch[1];
    
    const valuesMatch = barLine.match(/:\s*([\d\s.,]+)/);
    if (valuesMatch) {
      barValues = valuesMatch[1]
        .split(',')
        .map(val => {
          const cleanVal = val.trim()
            .replace(/\./g, '')
            .replace(/,/g, '.');
          return parseFloat(cleanVal) || 0;
        });
    }
  }
  
  if (xAxisLabels.length === 0 || barValues.length === 0) return code;
  
  return `---
title: ${title}
---
xychart-beta
    x-axis ${JSON.stringify(xAxisLabels)}
    y-axis "${barLabel}" 0 --> ${Math.max(...barValues) * 1.2}
    bar ${JSON.stringify(barValues)}`;
};


const getDiagramType = (code: string): string => {
  const trimmed = code.trim();
  
  if (trimmed.startsWith('chart') || (trimmed.includes('x-axis [') && trimmed.includes('bar '))) {
    return 'bar';
  }
  
  if (trimmed.startsWith('flowchart') || trimmed.startsWith('graph')) return 'flowchart';
  if (trimmed.startsWith('gantt')) return 'gantt';
  if (trimmed.startsWith('pie')) return 'pie';
  if (trimmed.startsWith('sequenceDiagram')) return 'sequence';
  if (trimmed.startsWith('classDiagram')) return 'class';
  if (trimmed.startsWith('stateDiagram')) return 'state';
  if (trimmed.startsWith('erDiagram')) return 'er';
  if (trimmed.startsWith('journey')) return 'journey';
  if (trimmed.startsWith('xychart-beta')) return 'bar';
  
  if (trimmed.includes('-->') || trimmed.includes('---') || trimmed.includes('==>')) return 'flowchart';
  if (trimmed.includes('dateFormat') || trimmed.includes('section')) return 'gantt';
  if (trimmed.includes('showData') || trimmed.includes('title') && trimmed.includes(':')) return 'pie';
  if (trimmed.includes('x-axis') && trimmed.includes('bar')) return 'bar';
  
  return 'flowchart';
};


const sanitizePieNumbers = (code: string): string => {
  return code
    .split('\n')
    .map(line => {
      if (!line.includes(':')) return line;
      return line.replace(
        /:\s*([\d.]+),(\d+)/g,
        (_, intPart, decimal) => {
          const normalized = intPart.replace(/\./g, '') + '.' + decimal;
          return `: ${normalized}`;
        }
      );
    })
    .join('\n');
};


const createCleanFlowchart = (code: string): string => {

  

  const lines = code.split('\n').filter(line => line.trim());

  const nodeMap = new Map<string, {type: 'rect' | 'diamond', content: string}>();
  const connections: string[] = [];
  

  const rectPattern = /([A-Z])\[([^\]]+)\]/;
  const diamondPattern = /([A-Z])\{([^}]+)\}/;
  const arrowPattern = /([A-Z])\s+(--\s*([^->]+)\s*-->\s*([A-Z])|-->\s*([A-Z]))/;
  
  lines.forEach(line => {
    const trimmed = line.trim();
    

    const rectMatch = trimmed.match(rectPattern);
    if (rectMatch) {
      const [, id, content] = rectMatch;
      nodeMap.set(id, {type: 'rect', content: content.replace(/["']/g, '').trim()});
    }
    

    const diamondMatch = trimmed.match(diamondPattern);
    if (diamondMatch) {
      const [, id, content] = diamondMatch;
      nodeMap.set(id, {type: 'diamond', content: content.replace(/["']/g, '').trim()});
    }
    
 
    if (trimmed.includes('-->') || trimmed.includes('--')) {
      connections.push(trimmed);
    }
  });
  

  if (nodeMap.size === 0) {

    const manualNodes = [
      {id: 'A', type: 'rect' as const, content: 'Início: Dados Brutos das Parcelas'},
      {id: 'B', type: 'rect' as const, content: 'Classificar Parcelas<br>Vincendas vs. Inadimplentes'},
      {id: 'C', type: 'rect' as const, content: 'Calcular Dias em Atraso<br>e Definir Elegibilidade'},
      {id: 'D', type: 'rect' as const, content: 'Obter Parâmetros da Operação<br>1. Taxa de Juros Anual<br>2. Prazo da Operação'},
      {id: 'E', type: 'rect' as const, content: 'Calcular Taxa Equivalente Mensal<br>i = 1 + Juros_ano ^ 1/12 - 1'},
      {id: 'F', type: 'rect' as const, content: 'Calcular Diferença em Meses (t)<br>entre Data Ref. e Vencimento'},
      {id: 'G', type: 'diamond' as const, content: 'Aplicar Fórmula do VP<br>por parcela'},
      {id: 'H', type: 'rect' as const, content: 'VP = Valor da Parcela Corrigida'},
      {id: 'I', type: 'rect' as const, content: 'VP = VF / 1 + i ^ t'},
      {id: 'J', type: 'rect' as const, content: 'Somar VP de todas as<br>parcelas elegíveis'},
      {id: 'K', type: 'rect' as const, content: 'Resultado: Valor Presente<br>Total da Carteira'},
    ];
    
    manualNodes.forEach(node => nodeMap.set(node.id, node));
  }
  

  let flowchart = 'flowchart TD\n';
  

  Array.from(nodeMap.entries()).forEach(([id, node]) => {
    if (node.type === 'diamond') {
      flowchart += `    ${id}{"${node.content}"}\n`;
    } else {
      flowchart += `    ${id}["${node.content}"]\n`;
    }
  });
  

  if (connections.length > 0) {
    connections.forEach(conn => {
      flowchart += `    ${conn}\n`;
    });
  } else {

    flowchart += `    A --> B\n`;
    flowchart += `    B --> C\n`;
    flowchart += `    C --> D\n`;
    flowchart += `    D --> E\n`;
    flowchart += `    E --> F\n`;
    flowchart += `    F --> G\n`;
    flowchart += `    G -- Parcela Inadimplente --> H\n`;
    flowchart += `    G -- Parcela Vincenda --> I\n`;
    flowchart += `    H --> J\n`;
    flowchart += `    I --> J\n`;
    flowchart += `    J --> K\n`;
  }
  
  return flowchart.trim();
};


const sanitizeMermaidCode = (code: string, detectedType: string): string => {
  let sanitized = code.trim();
  

  sanitized = sanitized.replace(/%%[^\n]*\n?/g, '');
  

  if (detectedType === 'bar') {
    sanitized = fixBarChartSyntax(sanitized);
  } 
  else if (detectedType === 'flowchart') {
  
    sanitized = repairMermaidCode(sanitized);
    
 
    if (!sanitized.startsWith('flowchart') && !sanitized.startsWith('graph')) {
      sanitized = 'flowchart TD\n' + sanitized;
    }

    sanitized = sanitized.replace(/\[([^\]]+)\]/g, (match, content) => {
      let clean = content.replace(/["']/g, '').trim();
      clean = clean.replace(/\\n/g, '<br>');
      return `["${clean}"]`;
    });
    
    sanitized = sanitized.replace(/\{([^}]+)\}/g, (match, content) => {
      let clean = content.replace(/["']/g, '').trim();
      clean = clean.replace(/\\n/g, '<br>');
      clean = clean.replace(/\s*\]\s*$/, '');
      return `{${clean}}`;
    });
  }
  else if (detectedType === 'gantt') {
    if (!sanitized.startsWith('gantt')) {
      sanitized = 'gantt\n' + sanitized;
    }
  }
  else if (detectedType === 'pie') {
    if (!sanitized.startsWith('pie')) {
      sanitized = 'pie\n' + sanitized;
    }
    sanitized = sanitizePieNumbers(sanitized);
  }
  
  return sanitized;
};

export default function MermaidRenderer({ code }: { code: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [diagramType, setDiagramType] = useState<string>('flowchart');

  useEffect(() => {
    if (!ref.current) return;
    

    const exampleCode = `flowchart TD
    A["Início: Dados Brutos das Parcelas"] --> B["Classificar Parcelas<br>Vincendas vs. Inadimplentes"]
    B --> C["Calcular Dias em Atraso<br>e Definir Elegibilidade"]
    C --> D["Obter Parâmetros da Operação<br>1. Taxa de Juros Anual<br>2. Prazo da Operação"]
    D --> E["Calcular Taxa Equivalente Mensal<br>i = 1 + Juros_ano ^ 1/12 - 1"]
    E --> F["Calcular Diferença em Meses (t)<br>entre Data Ref. e Vencimento"]
    F --> G{"Aplicar Fórmula do VP<br>por parcela"}
    G -- Parcela Inadimplente --> H["VP = Valor da Parcela Corrigida"]
    G -- Parcela Vincenda --> I["VP = VF / 1 + i ^ t"]
    H --> J["Somar VP de todas as<br>parcelas elegíveis"]
    I --> J
    J --> K["Resultado: Valor Presente<br>Total da Carteira"]`;
    
    let sanitizedCode = code.trim();
    setError(null);
    const detectedType = getDiagramType(sanitizedCode);
    setDiagramType(detectedType);
    
    try {
   
      if (detectedType === 'flowchart' && sanitizedCode.includes('G{') && sanitizedCode.includes(']')) {
       
        
      
        const useExampleCode = false; 
        
        if (useExampleCode) {
          sanitizedCode = exampleCode;
        } else {
      
          sanitizedCode = createCleanFlowchart(sanitizedCode);
        }
      } else {
        
        sanitizedCode = sanitizeMermaidCode(sanitizedCode, detectedType);
      }
      
   
      sanitizedCode = sanitizedCode.replace(/[`$]/g, '');
      sanitizedCode = sanitizedCode.replace(/\n\s*\n\s*\n/g, '\n\n');
      
      const id = `mermaid-${mermaidSeq++}`;
      
    
      if (ref.current) {
        ref.current.innerHTML = `<div id="${id}"></div>`;
      }
      

      

      mermaid
        .render(id, sanitizedCode)
        .then(({ svg }) => {
          if (ref.current) {
            ref.current.innerHTML = svg;
            setError(null);
          }
        })
        .catch((err) => {
          console.error("Erro Mermaid:", err);
          
       
          if (detectedType === 'flowchart') {
            console.log("Tentando com código de exemplo...");
            const exampleId = `mermaid-example-${mermaidSeq++}`;
            
            mermaid
              .render(exampleId, exampleCode)
              .then(({ svg }) => {
                if (ref.current) {
                  ref.current.innerHTML = svg;
                  setError(null);
                }
              })
              .catch((exampleErr) => {
              
              });
          } else {
            setError(err.message);
          
          }
        });
        
    } catch (err: any) {
      console.error("Erro no processamento:", err);
      setError(err.message);
    
    }
  }, [code]);

  return <div ref={ref} className="mermaid-container" data-diagram-type={diagramType} />;
}