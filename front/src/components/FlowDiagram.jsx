import React, { useCallback } from "react";
import ReactFlow, {
  MiniMap,
  Controls,
  Background,
  useNodesState,
  useEdgesState
} from "reactflow";
import "reactflow/dist/style.css";

export default function FlowDiagram({ data }) {
  const initialNodes = data.nodes.map((n, idx) => ({
    id: n.id,
    position: n.position ?? { x: 100, y: idx * 120 },
    data: { label: n.label },
    style: {
      padding: 10,
      borderRadius: 8,
      border: "2px solid #0A3D62",
      background: "#EAF2F8",
      fontWeight: 600
    }
  }));

  const initialEdges = data.edges.map((e) => ({
    id: `${e.from}-${e.to}`,
    source: e.from,
    target: e.to,
    label: e.label ?? "",
    animated: true,
    style: { stroke: "#0A3D62" },
    labelStyle: { fill: "#0A3D62", fontWeight: 600 }
  }));

  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);

  return (
    <div style={{ width: "100%", height: 420 }}>
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        fitView
      >
        <MiniMap
          nodeColor={() => "#0A3D62"}
          maskColor="rgba(0,0,0,0.1)"
        />
        <Controls position="bottom-right" />
        <Background color="#ddd" gap={18} />
      </ReactFlow>
    </div>
  );
}
