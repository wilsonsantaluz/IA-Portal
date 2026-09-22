
import { useChatLogic } from "../hooks/useChatLogic";
import "./markdown.css";
import { ChatInput, MessageList } from "../components/ChatPage";
import React, { useEffect, useMemo, useRef } from "react";
import { Box, Snackbar, Alert } from "@mui/material";
import "./markdown.css";
import { useChatScroll } from "../hooks/useChatScroll";


export default function ChatPage() {
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
  } = useChatLogic();

 

  const containerRef = useRef<HTMLDivElement>(null);
  const endRef = useRef<HTMLDivElement>(null);

  const { scrollHardToBottom, scrollCardSoft } =
  useChatScroll(containerRef);
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
    <Box sx={{ height: "100%", display: "flex", flexDirection: "column", overflow: "hidden" }}>
      <MessageList
        messages={messages}
        statusMessage={statusMessage}
        onToggleExpand={onToggleExpand}
        onSendRating={sendRating}
        onScroll={onScroll}
        containerRef={containerRef}
        endRef={endRef}
      />

      <ChatInput
        input={input}
        loading={loading}
        onInputChange={setInput}
        onUpload={uploadDocument}
        onSend={() => {
          autoScrollRef.current = true;
          scheduleScrollToBottom("smooth");
          sendMessage();
        }}
      />

      <Snackbar open={snackbar.open} autoHideDuration={3000} onClose={closeSnackbar}>
        <Alert severity={snackbar.type} variant="filled">
          {snackbar.text}
        </Alert>
      </Snackbar>
    </Box>
  );
}
