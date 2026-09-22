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
    <Box sx={{ flex: 1, overflow: "auto" }} ref={containerRef}>
      <Stack maxWidth={1200} mx="auto" px={2} spacing={1} pt={2}>
        {messages.map((message, index) => (
          <MessageItem
            key={message.id}
            message={message}
            index={index}
            isLast={index === messages.length - 1}
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
