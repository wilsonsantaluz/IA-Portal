import React, { useEffect, useRef } from "react";
import { Box, Stack } from "@mui/material";
import MessageItem from "./MessageItem";

interface Message {
  id: string;
  from: "user" | "ia";
  streamText?: string;
  finalText?: string;
  finished?: boolean;
  rating?: number;
  rated?: boolean;
  expanded?: boolean;
  streamFadingOut?: boolean;
}

interface MessageListProps {
  messages: Message[];
  statusMessage: string;
  onToggleExpand: (id: string) => void;
  onSendRating: (score: number, idx: number) => void;
  onScroll: (isAtBottom: boolean) => void;

  containerRef: React.RefObject<HTMLDivElement>;
  endRef: React.RefObject<HTMLDivElement>;
}

export default function MessageList({
  messages,
  statusMessage,
  onToggleExpand,
  onSendRating,
  onScroll,
  containerRef,
  endRef,
}: MessageListProps) {
  const internalContainerRef = useRef<HTMLDivElement>(null);
  const internalEndRef = useRef<HTMLDivElement>(null);

  const resolvedContainerRef = containerRef ?? internalContainerRef;
  const resolvedEndRef = endRef ?? internalEndRef;

  useEffect(() => {
    const el = resolvedContainerRef.current;
    if (!el) return;

    const handle = () => {
      const isAtBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 20;
      onScroll(isAtBottom);
    };

    el.addEventListener("scroll", handle);
    return () => el.removeEventListener("scroll", handle);
  }, [onScroll, resolvedContainerRef]);

  return (
    <Box sx={{ flex: 1, minHeight: 0, overflow: "auto" }} ref={containerRef}>
      <Stack maxWidth={1040} mx="auto" px={{ xs: 2, md: 4 }} spacing={3} py={3}>
        {messages.map((message, index) => (
          <MessageItem
            key={message.id}
            message={message}
            index={index}
            statusMessage={statusMessage}
            onToggleExpand={onToggleExpand}
            onSendRating={onSendRating}
          />
        ))}
        <div ref={endRef} />
      </Stack>
    </Box>
  );
}
