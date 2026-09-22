import { RefObject, useEffect, useRef } from "react";

export function useChatScroll(
  containerRef: RefObject<HTMLDivElement>
) {
  const autoScrollRef = useRef(true);

  // =========================
  // Detecta se usuário subiu
  // =========================
  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    const onScroll = () => {
      const nearBottom =
        el.scrollHeight - el.scrollTop - el.clientHeight < 80;
      autoScrollRef.current = nearBottom;
    };

    el.addEventListener("scroll", onScroll, { passive: true });
    return () => el.removeEventListener("scroll", onScroll);
  }, [containerRef]);

  // =========================
  // Observa DOM real
  // =========================
  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    const scrollIfAllowed = () => {
      if (!autoScrollRef.current) return;
      el.scrollTop = el.scrollHeight;
    };

    const mutationObserver = new MutationObserver(scrollIfAllowed);
    mutationObserver.observe(el, {
      childList: true,
      subtree: true,
      characterData: true,
    });

    const resizeObserver = new ResizeObserver(scrollIfAllowed);
    resizeObserver.observe(el);

    return () => {
      mutationObserver.disconnect();
      resizeObserver.disconnect();
    };
  }, [containerRef]);

  // =========================
  // API ANTIGA (MANTIDA)
  // =========================
  const scrollHardToBottom = () => {
    autoScrollRef.current = true;
    const el = containerRef.current;
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  };

  const scrollCardSoft = (cardId: string) => {
    const container = containerRef.current;
    if (!container) return;

    const card = document.querySelector(
      `[data-msg-id="${cardId}"]`
    ) as HTMLElement | null;

    if (!card) return;

    card.scrollIntoView({
      behavior: "smooth",
      block: "end",
    });
  };

  return {
    scrollHardToBottom, // ✅ NÃO QUEBRA ChatPage
    scrollCardSoft,     // ✅ NÃO QUEBRA ChatPage
  };
}
