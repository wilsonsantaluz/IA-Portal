// src/types/message.ts
export interface Message {
    id: string;
    from: "user" | "ia";
    raw: string;          // Texto bruto acumulado do stream
    final?: string;       // Markdown final processado
    thinking?: string;    // Pensamento da IA
    finished?: boolean;   // Se já terminou de receber
    rating?: number;      // Avaliação (1-10)
    rated?: boolean;      // Se já foi avaliado
  }