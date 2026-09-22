import React, { createContext, useContext, useRef, useState } from "react";

interface ChatUIContextType {
  hasAnyCollapsed: boolean;
  setHasAnyCollapsed: (v: boolean) => void;
  registerToggle: (fn: () => void) => void;
  toggleExpandAll: () => void;
}

const ChatUIContext = createContext<ChatUIContextType | null>(null);

export function ChatUIProvider({ children }: { children: React.ReactNode }) {
  const [hasAnyCollapsed, setHasAnyCollapsed] = useState(false);

  // função registrada pelo ChatPage
  const toggleRef = useRef<() => void>(() => {});

  const registerToggle = (fn: () => void) => {
    toggleRef.current = fn;
  };

  const toggleExpandAll = () => {
    toggleRef.current?.();
  };

  return (
    <ChatUIContext.Provider
      value={{
        hasAnyCollapsed,
        setHasAnyCollapsed,
        registerToggle,
        toggleExpandAll,
      }}
    >
      {children}
    </ChatUIContext.Provider>
  );
}

export function useChatUI() {
  const ctx = useContext(ChatUIContext);
  if (!ctx) {
    throw new Error("useChatUI must be used inside ChatUIProvider");
  }
  return ctx;
}
