import { useChatLogic } from "../hooks/useChatLogic";
import "./markdown.css";
import { ChatInput, MessageList } from "../components/ChatPage";
import React, { useEffect, useMemo, useRef } from "react";
import {
  Box,
  Snackbar,
  Alert,
  Button,
  Typography,
  LinearProgress,
} from "@mui/material";
import "./markdown.css";
import { useChatScroll } from "../hooks/useChatScroll";
import ChatWelcome from "../components/ChatWelcome";
import ConversationPanel from "../components/ConversationPanel";
import ConversationModelPicker from "../components/ConversationModelPicker";
import BusinessSourcePicker from "../components/BusinessSourcePicker";
import { ChatStatus } from "../components/ChatPage/ChatStatus";
import { Link } from "react-router-dom";

export default function ChatPage() {
  const chat = useChatLogic();
  const {
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
  } = chat;

  const containerRef = useRef<HTMLDivElement>(null);
  const endRef = useRef<HTMLDivElement>(null);

  const { scrollHardToBottom, scrollCardSoft } = useChatScroll(containerRef);
  const autoScrollRef = useRef(true);
  const lastScrollAtRef = useRef(0);
  const rafRef = useRef<number | null>(null);

  const scheduleScrollToBottom = (behavior: ScrollBehavior = "auto") => {
    if (!autoScrollRef.current) return;

    const now = Date.now();
    if (now - lastScrollAtRef.current < 480) return;
    lastScrollAtRef.current = now;
    scrollHardToBottom();

    if (rafRef.current) cancelAnimationFrame(rafRef.current);
    rafRef.current = requestAnimationFrame(() => {
      endRef.current?.scrollIntoView({ behavior, block: "end" });
    });
  };
  const onScroll = (isAtBottom: boolean) => {
    autoScrollRef.current = isAtBottom;
    handleScroll(isAtBottom);
  };

  const isStreaming = useMemo(() => {
    const last = messages[messages.length - 1];
    return last?.from === "ia" && !last?.finished;
  }, [messages]);

  useEffect(() => {
    if (!messages.length) return;
    const last = messages[messages.length - 1];
    if (last?.from === "ia" && !last.finished) {
      scheduleScrollToBottom("smooth");
    }
  }, [messages.length]);

  useEffect(() => {
    if (!isStreaming) return;
    scheduleScrollToBottom("auto");
  }, [isStreaming, messages]);

  const prevLastFinishedRef = useRef<boolean | null>(null);
  useEffect(() => {
    const last = messages[messages.length - 1];
    if (!last || last.from !== "ia") return;

    const prev = prevLastFinishedRef.current;
    prevLastFinishedRef.current = !!last.finished;

    if (prev === false && last.finished) {
      scheduleScrollToBottom("smooth");
      scrollCardSoft(last.id);
      scrollHardToBottom();
    }
  }, [messages]);

  const onToggleExpand = (id: string) => {
    const m = messages.find((x) => x.id === id);
    const nextExpanded = !(m?.expanded !== false);
    toggleExpanded(id);
    scrollCardSoft(id);
  };

  return (
    <Box
      sx={{
        height: "100%",
        display: "flex",
        minWidth: 0,
        position: "relative",
      }}
    >
      <ConversationPanel chat={chat} />
      <Box
        sx={{
          height: "100%",
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
          flex: 1,
          minWidth: 0,
        }}
      >
        <Box
          sx={{
            px: { xs: 7, lg: 4 },
            py: 1.5,
            borderBottom: "1px solid",
            borderColor: "divider",
          }}
        >
          <Typography fontSize={13} fontWeight={600} noWrap>
            {chat.session?.title || "Nova conversa"}
          </Typography>
          <Typography fontSize={10} color="text.secondary">
            {chat.session ? `${chat.session.provider} · ${chat.session.model}` : "Seu modelo padrão será usado ao enviar."}
          </Typography>
          {chat.session && <ConversationModelPicker session={chat.session} disabled={loading || chat.historyLoading} refresh={chat.reloadCurrent} />}
          {chat.session && <Button component={Link} to={`/usage?sessionId=${chat.session.id}`} size="small" sx={{ mt: 0.5 }}>Consumo desta conversa</Button>}
          <BusinessSourcePicker sourceId={chat.dataSourceId} referenceDate={chat.referenceDate} onSource={chat.setDataSourceId} onDate={chat.setReferenceDate} onChoose={setInput} disabled={loading || chat.historyLoading}/>
        </Box>
        {chat.historyLoading && (
          <LinearProgress aria-label="Carregando histórico" />
        )}
        {chat.messagesCursor && (
          <Button
            disabled={chat.historyLoading || loading}
            onClick={() => void chat.loadOlder()}
          >
            Carregar mensagens anteriores
          </Button>
        )}
        {messages.some((m) => m.status && m.status !== "completed") && (
          <Alert severity="info" sx={{ mx: 2, mt: 1 }}>
            Há uma interação pendente ou interrompida neste histórico. Confira
            antes de enviar novamente.
          </Alert>
        )}
        {!messages.length ? (
          <Box sx={{ flex: 1, minHeight: 0, overflow: "auto" }}>
            <ChatWelcome
              onChoose={(prompt) => {
                setInput(prompt);
                requestAnimationFrame(() =>
                  document
                    .querySelector<HTMLTextAreaElement>("[data-chat-input]")
                    ?.focus(),
                );
              }}
            />
          </Box>
        ) : (
          <MessageList
            messages={messages}
            statusMessage={statusMessage}
            onToggleExpand={onToggleExpand}
            onSendRating={sendRating}
            onScroll={onScroll}
            containerRef={containerRef}
            endRef={endRef}
          />
        )}

        {loading && <Box sx={{ width: "100%", maxWidth: 1040, mx: "auto", flexShrink: 0 }}>
          <ChatStatus text={statusMessage || "Processando sua solicitação…"} />
        </Box>}
        <ChatInput
          input={input}
          loading={loading || chat.historyLoading}
          onInputChange={setInput}
          onUpload={uploadDocument}
          onSend={() => {
            autoScrollRef.current = true;
            scheduleScrollToBottom("smooth");
            sendMessage();
          }}
        />

        <Snackbar
          open={snackbar.open}
          autoHideDuration={3000}
          onClose={closeSnackbar}
        >
          <Alert severity={snackbar.type} variant="filled">
            {snackbar.text}
          </Alert>
        </Snackbar>
      </Box>
    </Box>
  );
}
