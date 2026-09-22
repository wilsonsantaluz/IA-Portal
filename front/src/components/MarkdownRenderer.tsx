import React, { useMemo } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

// ⚠️ Se você não usa MermaidRenderer neste path, ajuste o import:
import MermaidRenderer from "./MermaidRenderer";

// Se você tiver CSS tipo `.markdown-body`, mantemos essa classe
// e também deixamos um wrapper por "variant".
type MarkdownRendererProps = {
  text: string;
  variant?: "ia" | "user";
  className?: string;
};

/**
 * NÃO sanitiza / NÃO normaliza whitespace do Markdown.
 * Só normaliza quebra Windows -> Unix (seguro e necessário).
 */
function normalizeNewlines(input: string) {
  return (input ?? "").replace(/\r\n/g, "\n");
}

/**
 * Extrai o conteúdo do bloco de código SEM:
 * - trim agressivo
 * - replace de espaços
 * - replace de \n
 * Apenas remove UM \n final comum de fence (opcional e seguro).
 */
function extractCode(children: React.ReactNode) {
  const raw = String(children ?? "");
  return raw.endsWith("\n") ? raw.slice(0, -1) : raw;
}

export default function MarkdownRenderer({
  text,
  variant = "ia",
  className,
}: MarkdownRendererProps) {
  const md = useMemo(() => normalizeNewlines(text), [text]);

  /**
   * OBS:
   * - Não usamos rehypeRaw aqui porque HTML inline pode ser bloqueado por segurança
   *   e também pode causar inconsistência no streaming.
   * - Mermaid é interceptado via `code` renderer (AST), que é o jeito certo.
   */
  return (
    <div className={`markdown-body chat-md--${variant} ${className ?? ""}`.trim()}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        // Mantém o texto como está (não transforma quebras em <br/> automaticamente)
        // GFM já dá suporte a tabela, lista, etc.
        components={{
          /**
           * Intercepta blocos ```mermaid
           * e renderiza com MermaidRenderer.
           *
           * Isso evita completamente:
           * - Markdown colapsar espaços
           * - normalizações externas destruírem as linhas
           */
          code({ inline, className, children, ...props }) {
            const lang = (className || "").replace("language-", "").trim().toLowerCase();
            const isMermaid = !inline && (lang === "mermaid");

            if (isMermaid) {
              const mermaidCode = extractCode(children);
              return <MermaidRenderer code={mermaidCode} />;
            }

            // bloco de código normal
            if (!inline) {
              return (
                <pre className={className}>
                  <code {...props}>{children}</code>
                </pre>
              );
            }

            // inline code
            return (
              <code className={className} {...props}>
                {children}
              </code>
            );
          },

          /**
           * Opcional: garante que <pre> não receba
           * estilos estranhos vindos do markdown.
           */
          pre({ children }) {
            return <>{children}</>;
          },
        }}
      >
        {md}
      </ReactMarkdown>
    </div>
  );
}
