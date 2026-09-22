import { useState, useEffect, useRef, useCallback } from "react";
import { useChatUI } from "../context/ChatUIContext";
import api from "../services/api";

interface Message {
  id: string;
  from: "user" | "ia";
  streamText?: string;
  finalText?: string;
  finished?: boolean;
  rating?: number;
  rated?: boolean;
  expanded?: boolean;
}

const GUID = () =>
  "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : ((r & 0x3) | 0x8);
    return v.toString(16);
  });

export const useChatLogic = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [sessionId, setSessionId] = useState("");
  const [loading, setLoading] = useState(false);


  const [statusMessage, setStatusMessage] = useState("");

  // 🔥 Snackbar (mantido)
  const [snackbar, setSnackbar] = useState({
    open: false,
    type: "success" as "success" | "error",
    text: "",
  });

  const { setHasAnyCollapsed, registerToggle } = useChatUI();
  const autoScrollRef = useRef(true);
  const activeStreamIdRef = useRef<string | null>(null);

  // =========================
  // BUFFER SEMÂNTICO
  // =========================
  const semanticBufferRef = useRef("");
  const fullTextRef = useRef("");

  const shouldFlush = (text: string) =>
    text.endsWith(". ") ||
    text.endsWith("? ") ||
    text.endsWith("! ") ||
    text.endsWith("\n\n") ||
    text.endsWith("```") ||
    text.endsWith("'''");

  // =========================
  // Sessão
  // =========================
  useEffect(() => {
    let sid = localStorage.getItem("portal_session_id");
    if (!sid) {
      sid = GUID();
      localStorage.setItem("portal_session_id", sid);
    }
    setSessionId(sid);
  }, []);

  // =========================
  // Histórico
  // =========================
  useEffect(() => {
    if (!sessionId) return;

    const loadHistory = async () => {
      try {
        const res = await api.get(`/api/chatBot/history/${sessionId}`);
        const history = res.data ?? [];

        setMessages(
          history.map((item: any) => ({
            id: item.id ?? GUID(),
            from: item.role === "assistant" ? "ia" : "user",
            finalText: item.content || "",
            finished: true,
            rating: item.rating,
            rated: !!item.rating,
            expanded: false,
          }))
        );
      } catch (err) {
        console.error(err);
      }
    };

    loadHistory();
  }, [sessionId]);

  // =========================
  // Expansão global (mantido)
  // =========================
  const hasAnyCollapsed = messages.some(
    (m) => m.from === "ia" && m.finished && m.finalText && m.expanded === false
  );

  useEffect(() => {
    setHasAnyCollapsed(hasAnyCollapsed);
  }, [hasAnyCollapsed, setHasAnyCollapsed]);

  const toggleExpanded = useCallback((id: string) => {
    setMessages((prev) =>
      prev.map((m) =>
        m.id === id ? { ...m, expanded: !(m.expanded !== false) } : m
      )
    );
  }, []);

  useEffect(() => {
    registerToggle(() => {
      setMessages((p) =>
        p.map((m) =>
          m.from === "ia" && m.finished && m.finalText
            ? { ...m, expanded: hasAnyCollapsed }
            : m
        )
      );
    });
  }, [registerToggle, hasAnyCollapsed]);

  // =========================
  // Avaliação (mantida)
  // =========================
  const sendRating = async (score: number, idx: number) => {
    const question = messages[idx - 1]?.finalText;
    try {
      await api.post("/api/chatkb/rate", {
        userId: sessionId,
        question,
        rating: score,
      });
      setMessages((p) =>
        p.map((m, i) => (i === idx ? { ...m, rated: true, rating: score } : m))
      );
      setSnackbar({ open: true, type: "success", text: "Avaliação salva" });
    } catch {
      setSnackbar({ open: true, type: "error", text: "Erro ao salvar avaliação" });
    }
  };

  // =========================
  // STREAM
  // =========================
  const sendMessage = async (prompt?: string) => {
    const q = (prompt ?? input).trim();
    if (!q || loading || !sessionId) return;

    setLoading(true);
    setInput("");
    setStatusMessage("");

    const aiId = GUID();
    activeStreamIdRef.current = aiId;
    semanticBufferRef.current = "";
    fullTextRef.current = "";

    setMessages((p) => [
      ...p,
      { id: GUID(), from: "user", finalText: q, finished: true, expanded: true },
      { id: aiId, from: "ia", streamText: "", finished: false, expanded: true },
    ]);

    try {
      const auth = JSON.parse(localStorage.getItem("auth") || "null");
      const res = await fetch(`${api.defaults.baseURL}api/ia/ask/stream`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...(auth?.token ? { Authorization: `Bearer ${auth.token}` } : {}),
        },
        body: JSON.stringify({ pergunta: q, sessionId }),
      });

      if (!res.ok) throw new Error(`Falha no chat (HTTP ${res.status})`);
      if (!res.body) throw new Error("Resposta sem conteúdo de streaming");

      const reader = res.body.getReader();
      const decoder = new TextDecoder("utf-8");
      let buffer = "";

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const events = buffer.split("\n\n");
        buffer = events.pop() || "";

        for (const evt of events) {
          if (activeStreamIdRef.current !== aiId) return;

          let evtName = "data";
          const dataLines: string[] = [];

          for (const line of evt.split("\n")) {
            if (line.startsWith("event:")) {
              evtName = line.slice(6).trim();
              continue;
            }
            if (line.startsWith("data:")) {
              dataLines.push(line.slice(5));
            }
          }

          const rawData = dataLines.join("\n");
          if (evtName === "error") throw new Error(rawData);


          if (evtName.includes("status") || evtName.includes("step")) {
            console.log('status recebido ' + rawData)
            const cleanStatus = rawData
              .replace(/^[⏳🔎🤖🧠]\s*/g, "")
              .trim();

            setMessages((p) =>
              p.map((m) => {
                if (m.id !== aiId) return m;

                const content = m.streamText ?? "";

                // remove status anterior (blockquote de status)
                const withoutStatus = content.replace(
                  /^> \*\*.*?\*\*\n?\n?/m,
                  ""
                );

                return {
                  ...m,
                  streamText:
                  `> **${cleanStatus}**\n\n` +
                  withoutStatus.trimStart(),
                };
              })
            );


            continue;
          }

          if (rawData.includes("[*ENDSTREAN*]")) {
            fullTextRef.current += semanticBufferRef.current;

            setMessages((p) =>
              p.map((m) =>
                m.id === aiId
                  ? {
                    ...m,
                    finished: true,
                    finalText: fullTextRef.current,
                    streamText: "",
                  }
                  : m
              )
            );


            setLoading(false);
            activeStreamIdRef.current = null;
            return;
          }

          if (evtName !== "data") continue;
          semanticBufferRef.current += rawData;

          if (shouldFlush(semanticBufferRef.current)) {
            fullTextRef.current += semanticBufferRef.current;
            semanticBufferRef.current = "";

            setMessages((p) =>
              p.map((m) =>
                m.id === aiId ? { ...m, streamText: fullTextRef.current } : m
              )
            );
          }
        }
      }
      throw new Error("A conexão foi interrompida antes de concluir a resposta. Tente novamente.");
    } catch (err) {
      console.error(err);
      setMessages((p) => p.filter((m) => m.id !== aiId));
      setSnackbar({ open: true, type: "error", text: err instanceof Error ? err.message : "Falha na resposta da IA" });
    } finally {
      setLoading(false);
      activeStreamIdRef.current = null;
    }
  };

  // =========================
  // Scroll / Snackbar
  // =========================
  const uploadDocument = async (file: File) => {
    if (loading || !sessionId) return;
    if (file.size > 20 * 1024 * 1024) {
      setSnackbar({ open: true, type: "error", text: "O limite é de 20 MB por documento." });
      return;
    }
    setLoading(true);
    setStatusMessage("Extraindo e analisando documento...");
    try {
      const form = new FormData();
      form.append("file", file);
      form.append("sessionId", sessionId);
      const { data } = await api.post("api/ocr/analisar", form);
      if (!data.texto?.trim()) throw new Error("Nenhum texto foi encontrado no documento.");
      setMessages((previous) => [...previous,
        { id: GUID(), from: "user", finalText: data.texto, finished: true, expanded: false },
        { id: GUID(), from: "ia", finalText: data.analise, finished: true, expanded: true },
      ]);
    } catch (error) {
      setSnackbar({ open: true, type: "error", text: error instanceof Error ? error.message : "Falha na análise do documento" });
    } finally {
      setStatusMessage("");
      setLoading(false);
    }
  };

  const handleScroll = useCallback((isAtBottom: boolean) => {
    autoScrollRef.current = isAtBottom;
  }, []);

  const closeSnackbar = () => {
    setSnackbar((p) => ({ ...p, open: false }));
  };

  return {
    messages,
    input,
    loading,
    statusMessage,
    snackbar,
    setInput,
    sendMessage,
    uploadDocument,
    toggleExpanded,
    sendRating,
    handleScroll,
    closeSnackbar,
  };
};
