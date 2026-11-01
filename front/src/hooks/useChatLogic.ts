import { useState, useEffect, useRef, useCallback } from "react";
import { useChatUI } from "../context/ChatUIContext";
import { useAuth } from "../auth/AuthProvider";
import api from "../services/api";
export type Session = {
  id: string;
  title: string;
  titleSource: string;
  createdAt: string;
  updatedAt: string;
  provider: string | null;
  model: string | null;
  version: number;
};
type StoredMessage = {
  id: number;
  role: string;
  content: string;
  createdAt: string;
  status: string;
  provider?: string;
  model?: string;
};
interface Message {
  id: string;
  from: "user" | "ia";
  streamText?: string;
  finalText?: string;
  finished?: boolean;
  rating?: number;
  rated?: boolean;
  expanded?: boolean;
  status?: string;
}
function failure(error: unknown) {
  return (
    (error as { response?: { data?: { error?: string } } }).response?.data
      ?.error ||
    (error instanceof Error
      ? error.message
      : "Não foi possível concluir a operação.")
  );
}
function fromStored(m: StoredMessage): Message {
  return {
    id: String(m.id),
    from: m.role === "assistant" ? "ia" : "user",
    finalText: m.content,
    finished: true,
    expanded: true,
    status: m.status,
  };
}
export const useChatLogic = () => {
  const { user } = useAuth();
  const key = `portal_session_id_${user?.userId}`;
  const [sessions, setSessions] = useState<Session[]>([]);
  const [session, setSession] = useState<Session | null>(null);
  const [sessionsCursor, setSessionsCursor] = useState<string | null>(null);
  const [messagesCursor, setMessagesCursor] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [sessionsLoading, setSessionsLoading] = useState(false);
  const [statusMessage, setStatusMessage] = useState("");
  const [snackbar, setSnackbar] = useState({
    open: false,
    type: "success" as "success" | "error",
    text: "",
  });
  const { setHasAnyCollapsed, registerToggle } = useChatUI();
  const selectedRef = useRef<string | null>(null);
  const streamAbort = useRef<AbortController | null>(null);
  const sessionListVersion = useRef(0);
  const mounted = useRef(true);
  const initialized = useRef(false);
  const report = useCallback(
    (error: unknown) =>
      setSnackbar({ open: true, type: "error", text: failure(error) }),
    [],
  );
  const refreshSessions = useCallback(
    async (cursor?: string) => {
      const version = ++sessionListVersion.current;
      setSessionsLoading(true);
      try {
        const { data } = await api.get<{
          items: Session[];
          nextCursor: string | null;
        }>("api/chatBot/sessions", { params: { search, limit: 30, cursor } });
        if (!mounted.current || version !== sessionListVersion.current) return;
        setSessions((previous) =>
          cursor
            ? [
                ...previous,
                ...data.items.filter(
                  (s) => !previous.some((p) => p.id === s.id),
                ),
              ]
            : data.items,
        );
        setSessionsCursor(data.nextCursor);
        return data.items;
      } catch (error) {
        if (mounted.current && version === sessionListVersion.current)
          report(error);
      } finally {
        if (mounted.current && version === sessionListVersion.current)
          setSessionsLoading(false);
      }
    },
    [search, report],
  );
  const loadMessages = useCallback(
    async (id: string, before?: string) => {
      setHistoryLoading(true);
      try {
        const { data } = await api.get<{
          items: StoredMessage[];
          nextCursor: string | null;
        }>(`api/chatBot/sessions/${id}/messages`, {
          params: { limit: 50, before },
        });
        if (!mounted.current || selectedRef.current !== id) return;
        setMessages((previous) =>
          before
            ? [...data.items.map(fromStored), ...previous]
            : data.items.map(fromStored),
        );
        setMessagesCursor(data.nextCursor);
      } catch (error) {
        if (selectedRef.current === id && mounted.current) report(error);
      } finally {
        if (selectedRef.current === id && mounted.current)
          setHistoryLoading(false);
      }
    },
    [report],
  );
  const selectSession = useCallback(
    async (item: Session) => {
      initialized.current = true;
      selectedRef.current = item.id;
      setSession(item);
      localStorage.setItem(key, item.id);
      setMessages([]);
      setMessagesCursor(null);
      setStatusMessage("");
      setInput("");
      await loadMessages(item.id);
    },
    [key, loadMessages],
  );
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      streamAbort.current?.abort();
    };
  }, []);
  useEffect(() => {
    const timer = window.setTimeout(() => {
      void refreshSessions().then(async (items) => {
        if (!items || initialized.current || !mounted.current) return;
        initialized.current = true;
        const saved = localStorage.getItem(key);
        if (saved) {
          try {
            const { data } = await api.get<Session>(
              `api/chatBot/sessions/${saved}`,
            );
            if (mounted.current) await selectSession(data);
            return;
          } catch {
            localStorage.removeItem(key);
          }
        }
        if (items[0] && mounted.current) await selectSession(items[0]);
      });
    }, 250);
    return () => clearTimeout(timer);
  }, [refreshSessions, key, selectSession]);
  const newConversation = async () => {
    if (loading || historyLoading) return null;
    setHistoryLoading(true);
    try {
      const { data } = await api.post<Session>("api/chatBot/sessions");
      await selectSession(data);
      await refreshSessions();
      return data;
    } catch (error) {
      report(error);
      return null;
    } finally { setHistoryLoading(false); }
  };
  const renameConversation = async (item: Session, title: string) => {
    try {
      const { data } = await api.patch<Session>(
        `api/chatBot/sessions/${item.id}`,
        { title, version: item.version },
      );
      if (selectedRef.current === item.id) setSession(data);
      await refreshSessions();
      return true;
    } catch (error) {
      report(error);
      await refreshSessions();
      return false;
    }
  };
  const deleteConversation = async (item: Session) => {
    try {
      await api.delete(`api/chatBot/sessions/${item.id}`);
      if (selectedRef.current === item.id) {
        selectedRef.current = null;
        setSession(null);
        setMessages([]);
        setMessagesCursor(null);
        localStorage.removeItem(key);
      }
      await refreshSessions();
      return true;
    } catch (error) {
      report(error);
      return false;
    }
  };
  const reload = async (id: string) => {
    if (!mounted.current) return;
    await loadMessages(id);
    try {
      const { data } = await api.get<Session>(`api/chatBot/sessions/${id}`);
      if (selectedRef.current === id) setSession(data);
    } catch (error) {
      report(error);
    }
    await refreshSessions();
  };
  const sendMessage = async (prompt?: string) => {
    const question = (prompt ?? input).trim();
    if (!question || loading || historyLoading) return;
    let active = session;
    if (!active) active = await newConversation();
    if (!active) return;
    const id = active.id;
    const requestId = crypto.randomUUID();
    const aiId = "pending-" + requestId;
    setLoading(true);
    setInput("");
    setStatusMessage("Preparando resposta...");
    setMessages((previous) => [
      ...previous,
      {
        id: "question-" + requestId,
        from: "user",
        finalText: question,
        finished: true,
        expanded: true,
      },
      { id: aiId, from: "ia", streamText: "", finished: false, expanded: true },
    ]);
    const abort = new AbortController();
    streamAbort.current = abort;
    let accepted = false;
    try {
      const token = JSON.parse(localStorage.getItem("auth") || "null")?.token;
      const response = await fetch(`${api.defaults.baseURL}api/ia/ask/stream`, {
        method: "POST",
        signal: abort.signal,
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ pergunta: question, sessionId: id, requestId }),
      });
      if (!response.ok) {
        const data = await response.json().catch(() => null);
        throw new Error(
          data?.error || `Falha no envio (HTTP ${response.status}).`,
        );
      }
      if (!response.body)
        throw new Error("Resposta de streaming indisponível.");
      const reader = response.body.getReader();
      accepted = true;
      const decoder = new TextDecoder();
      let buffer = "";
      let answer = "";
      let completed = false;
      let persisted = false;
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const events = buffer.split("\n\n");
        buffer = events.pop() || "";
        for (const event of events) {
          let type = "data";
          const lines: string[] = [];
          for (const line of event.split("\n")) {
            if (line.startsWith("event:")) type = line.slice(6).trim();
            if (line.startsWith("data:")) lines.push(line.slice(5));
          }
          const text = lines.join("\n");
          if (type === "error") throw new Error(text);
          if (type === "status") setStatusMessage(text);
          if (type === "persisted") persisted = true;
          if (type === "done" && text.includes("[*ENDSTREAN*]"))
            completed = persisted;
          if (type === "data") {
            answer += text;
            setMessages((previous) =>
              previous.map((m) =>
                m.id === aiId ? { ...m, streamText: answer } : m,
              ),
            );
          }
        }
      }
      if (!completed)
        throw new Error(
          "Conexão interrompida. Confira o histórico antes de enviar novamente.",
        );
    } catch (error) {
      if (mounted.current) { if (!accepted) setInput(question); report(error); }
    } finally {
      streamAbort.current = null;
      if (mounted.current) {
        setLoading(false);
        setStatusMessage("");
        await reload(id);
      }
    }
  };
  const uploadDocument = async (file: File) => {
    if (loading || historyLoading) return;
    if (file.size > 20 * 1024 * 1024) {
      report(new Error("O limite é de 20 MB por documento."));
      return;
    }
    let active = session;
    if (!active) active = await newConversation();
    if (!active) return;
    setLoading(true);
    setStatusMessage("Extraindo e analisando documento...");
    try {
      const form = new FormData();
      form.append("file", file);
      form.append("sessionId", active.id);
      form.append("requestId", crypto.randomUUID());
      await api.post("api/ocr/analisar", form);
    } catch (error) {
      report(error);
    } finally {
      setLoading(false);
      setStatusMessage("");
      await reload(active.id);
    }
  };
  const toggleExpanded = useCallback(
    (id: string) =>
      setMessages((previous) =>
        previous.map((m) =>
          m.id === id ? { ...m, expanded: m.expanded === false } : m,
        ),
      ),
    [],
  );
  const hasCollapsed = messages.some(
    (m) => m.from === "ia" && m.expanded === false,
  );
  useEffect(() => {
    setHasAnyCollapsed(hasCollapsed);
    registerToggle(() =>
      setMessages((previous) =>
        previous.map((m) =>
          m.from === "ia" ? { ...m, expanded: hasCollapsed } : m,
        ),
      ),
    );
  }, [hasCollapsed, registerToggle, setHasAnyCollapsed]);
  const sendRating = async (score: number, index: number) => {
    try {
      await api.post("api/chatkb/rate", {
        question: messages[index - 1]?.finalText,
        rating: score,
      });
      setMessages((previous) =>
        previous.map((m, i) =>
          i === index ? { ...m, rating: score, rated: true } : m,
        ),
      );
    } catch (error) {
      report(error);
    }
  };
  return {
    sessions,
    session,
    sessionsCursor,
    messagesCursor,
    search,
    setSearch,
    sessionsLoading,
    historyLoading,
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
    handleScroll: useCallback((_atBottom: boolean) => {}, []),
    closeSnackbar: () =>
      setSnackbar((previous) => ({ ...previous, open: false })),
    newConversation,
    selectSession,
    renameConversation,
    deleteConversation,
    refreshSessions,
    loadOlder: () => {
      if (session && messagesCursor)
        return loadMessages(session.id, messagesCursor);
    },
  };
};
