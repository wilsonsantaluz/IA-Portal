# Atlas Serviços Digitais — Base de conhecimento fictícia (v1)

> DEMONSTRAÇÃO. Empresa, pessoas, contratos, políticas, indicadores e valores são inteiramente fictícios. Não são aconselhamento jurídico ou financeiro. NãN?o utilizar dados de empregadores nem de clientes reais.

## 01 — Perfil da organização
**ID:** atlas-company-profile
**Categoria:** institucional
**Título:** Atlas Serviços Digitais — visão geral
A Atlas Serviços Digitais é uma empresa fictícia brasileira que fornece serviços de suporte, automação e gestão de assinaturas a pequenas e médias empresas. Seu portfólio demonstrativo inclui planos Essencial, Profissional e Enterprise. A organização opera com cerca de 500 clientes simulados. O objetivo desta base é explicar os processos e apoiar análises de faturas, pagamentos, recebíveis e inadimplência gerados artificialmente.

## 02 — Ciclo de faturamento
**ID:** atlas-billing-cycle
**Categoria:** faturamento
**Título:** Emissão, vencimento e conciliação
Cada contrato ativo gera uma fatura mensal. Uma fatura possui identificador, cliente, contrato, competência, data de emissão, vencimento, valor bruto, descontos e valor líquido. Pagamentos podem ser parciais, pontuais ou posteriores ao vencimento. O saldo em aberto é o valor líquido menos a soma de pagamentos confirmados, limitado inferiormente a zero. Uma fatura só é considerada quitada quando seu saldo em aberto chega a zero. Cancelamentos e estornos são eventos distintos e devem manter trilha de auditoria.

## 03 — Classificação de atraso
**ID:** atlas-aging-policy
**Categoria:** cobrança
**Título:** Faixas de envelhecimento dos recebíveis
Para a demonstração, uma fatura vencida com saldo positivo é classificada pelo número de dias corridos após o vencimento: 1–15 dias (atraso inicial); 16–30 dias (atenção); 31–60 dias (risco elevado); 61–90 dias (crítico); mais de 90 dias (recuperação especializada). Faturas ainda não vencidas ficam em 'a vencer'. Esta classificação é uma política sintética, não uma exigência legal ou padrão regulatório.

## 04 — Política de cobrança
**ID:** atlas-collections-policy
**Categoria:** cobrança
**Título:** Régua de cobrança demonstrativa
Até 5 dias de atraso: lembrete cordial. De 6 a 15 dias: contato de acompanhamento e conferência de pagamento. De 16 a 30 dias: atendimento humano e análise de dificuldade operacional. De 31 a 60 dias: proposta de negociação sujeita a aprovação. Acima de 60 dias: encaminhamento para equipe especializada. Nenhuma ação é executada automaticamente por um modelo de IA; o sistema apenas recomenda ações, com aprovação humana e registro de auditoria. Contatos respeitam consentimento, preferências e legislação aplicável.

## 05 — Negociação
**ID:** atlas-negotiation-policy
**Categoria:** cobrança
**Título:** Regras de negociação simuladas
Acordos são propostas, não alterações automáticas de contrato. Para demonstrar o fluxo, um operador pode simular parcelamento em até três parcelas e desconto de até 5% sobre o saldo em aberto, condicionado à autorização de um supervisor. O sistema deve apresentar impacto no fluxo de caixa e registrar aprovador, justificativa e versão da proposta. Não aplicar automaticamente juros, multas ou descontos sem base contratual e revisão jurídica.

## 06 — Indicadores financeiros
**ID:** atlas-kpi-glossary
**Categoria:** indicadores
**Título:** Glossário de indicadores e fórmulas
**Receita faturada:** soma do valor líquido das faturas emitidas no período, excluindo canceladas.
**Recebimento no período:** soma dos pagamentos confirmados por data de pagamento, independentemente da competência.
**Saldo a receber:** soma dos saldos positivos de faturas não canceladas.
**Valor vencido:** soma dos saldos positivos cujo vencimento é anterior à data de referência.
**Taxa de inadimplência por valor:** valor vencido dividido pelo saldo a receber, quando o denominador for maior que zero.
**Prazo médio de recebimento demonstrativo:** média ponderada dos dias entre emissão e pagamento, ponderada pelo valor pago, apenas para parcelas recebidas.
O sistema deve declarar data de referência, moeda BRL, filtros e denominadores; não misturar recebimentos de caixa com faturamento por competência.

## 07 — Qualidade e rastreabilidade
**ID:** atlas-data-governance
**Categoria:** governança
**Título:** Qualidade dos dados e explicabilidade
Toda resposta analítica deve informar período, origem, filtros e horário da consulta. Valores monetários usam BRL. Não inventar dados ausentes, nem afirmar causalidade com base apenas em correlação. Uma resposta baseada em política deve citar o documento e sua versão; uma resposta numérica deve citar a consulta, fonte e período. Havendo conflito entre documento e banco, explicar a divergência em vez de escolher silenciosamente.

## 08 — Privacidade e acesso
**ID:** atlas-access-policy
**Categoria:** segurança
**Título:** Acesso mínimo e segregação
Dados de clientes devem ser fictícios e sem CPF, e-mail ou telefone reais. Conexões analíticas usam contas somente leitura, limites de linhas e tempo, catálogos permitidos e isolamento por organização. Não revelar SQL de outras organizações, credenciais ou dados privados em logs e respostas. A IA não deve executar comandos de escrita nem expor tabelas não autorizadas.

## 09 — Atendimento e contestação
**ID:** atlas-dispute-process
**Categoria:** atendimento
**Título:** Contestação de faturas
Quando um cliente contesta uma cobrança, o operador registra motivo, evidências, data e responsável. A fatura fica sinalizada para revisão, sem baixa automática. O fluxo é: receber contestação → validar contrato e fatura → analisar pagamentos e ajustes → aprovar correção ou manter cobrança com justificativa → notificar cliente → registrar desfecho. O diagrama Mermaid deve refletir essas etapas, sem inventar aprovações.

## 10 — Perguntas de demonstração
**ID:** atlas-demo-questions
**Categoria:** demonstração
**Título:** Exemplos de perguntas para o portal
1. Qual é o valor vencido hoje, por faixa de atraso?
2. Mostre os dez maiores saldos em aberto e o percentual que representam.
3. Compare faturamento e recebimentos nos últimos seis meses.
4. Qual política se aplica a uma fatura com 37 dias de atraso?
5. Gere um fluxograma Mermaid para contestação de faturas.
6. Quais clientes têm maior concentração de recebíveis, sem expor dados pessoais?
7. Quais indicadores não podem ser calculados com as tabelas disponíveis?
8. Explique as diferenças entre receita faturada e caixa recebido.
9. Qual seria o efeito de receber 20% do saldo vencido em até 30 dias? Identifique como simulação.
10. Combine as faixas de atraso com a régua de cobrança e proponha um plano de priorização, sujeito a aprovação humana.

## Requisitos de ingestão
- Cada seção numerada é um documento independente, com ID estável, categoria, título, texto, versão 1 e origem 'synthetic-demo'.
- A ingestão deve ser idempotente, sem duplicar entradas ou sobrescrever documentos editados pelo usuário.
- Verificar a entidade e o esquema reais antes de criar migration ou seed. Preferir o mecanismo de versionamento já presente no projeto.
- A base textual não deve conter valores analíticos inventados: esses virão de um dataset sintético separado e consultável por Trino.
