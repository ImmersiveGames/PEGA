# ADR-GOAP-01 — Domínio e evolução por composição

- **Status:** Aceito
- **Data:** 2026-10-25
- **Owner:** PEGA — gameplay dos larápios

## Contexto

O GDD define o GOAP como sistema de gameplay dos larápios, composto com as mecânicas concretas de cada Actor e configurado por arquétipo. Este ADR aplica ao GOAP a diretriz arquitetural transversal do PEGA registrada no [ADR de pré-produção, §13](../ADR-PREPRODUCAO.md#13-diretriz-arquitetural-transversal-do-pega), preservando a independência entre conhecimento, decisão e execução.

A implementação vigente ainda é um recorte da demonstração: `PEGANpcPlanSelector` seleciona planos de uma Action e associa o efeito previsto diretamente ao Goal do Desejo. Essa forma é evidência do estágio atual, não um limite arquitetural a preservar. No ecossistema Immersive, identidade, ocorrência e lifecycle do Actor pertencem ao Framework; mecânicas e regras de gameplay permanecem no PEGA. O `IPredicate` booleano do FSM no Foundation não recebe um estado para avaliar e não expressa conhecimento Desconhecido; é um contrato distinto e não pode ser ampliado pelo PEGA.

## Decisão

1. O GOAP permanece uma capacidade de gameplay do PEGA. Não será extraído antecipadamente para Foundation, Framework ou package independente.
2. A composição concreta do Actor determina as mecânicas presentes. O arquétipo configura e especializa capacidades, Actions, Desejos, parâmetros e perfis; não materializa nem remove mecânicas da ocorrência.
3. No domínio GOAP, capacidades, Goals, Desejos, Actions, conhecimento e avaliações especializadas aplicam a diretriz transversal por contratos que permitam composição e reutilização sem prejudicar clareza, determinismo, desempenho e manutenção.
4. O PEGA não modifica nem redefine o predicado booleano de FSM do Foundation para representar condições de GOAP. O GOAP precisa de condições que recebam a fonte de estado apropriada e expressem conhecimento incompleto; essa semântica pertence ao domínio de decisão do PEGA.

## Alternativas consideradas

- **Mover o GOAP ao Framework ou Foundation:** rejeitada; a semântica depende de Goals, conhecimento, capacidades e regras de gameplay do PEGA e não corresponde a uma autoridade genérica existente nesses pacotes.
- **Evoluir `IPredicate` do Foundation para atender o planejador:** rejeitada; o contrato pertence ao FSM, é booleano e sem contexto de estado, além de o pacote Foundation ser uma baseline técnica congelada.
- **Manter uma Action especializada por Desejo como regra do sistema:** rejeitada; isso prende novas soluções ao núcleo de decisão e impede a composição causal definida nos ADRs seguintes.
- **Criar um package genérico de GOAP agora:** rejeitada; não há evidência de um contrato independente de gameplay e um ciclo de versão próprio.

## Consequências

- O PEGA mantém a autoridade de decisão GOAP; providers/mecânicas permanecem responsáveis por oferecer e executar suas capacidades; as autoridades de conhecimento e mundo confirmam fatos e efeitos.
- Os ADRs seguintes definem as semânticas que a composição deve preservar, sem prescrever classes, interfaces, algoritmo de busca ou authoring Unity.
- O runtime atual não é declarado conforme à arquitetura-alvo por este registro. Sua evolução requer implementação e validação posteriores, fora deste ADR.
- Uma possível extração após a Demo continua sujeita à avaliação por evidência de reutilização, independência de domínio, estabilidade de contrato e custo de manutenção, conforme o ADR de pré-produção.

## Não decidido

Nomes de assemblies/classes/interfaces, localização exata de arquivos, estrutura de assets, formato de serialização, algoritmo de busca e eventual promoção futura a package.

## Relação com decisões anteriores

Este é o primeiro ADR técnico específico de GOAP. Preserva a fronteira de gameplay já definida no GDD e a estratégia de produção PP-37; não substitui decisões de Contextos, conhecimento, satisfação, reservas ou escopo do MVP. A associação direta entre Action e Goal observada na implementação atual é tratada como limite do recorte demonstrativo, não como contrato normativo.

## Autoridade e rastreabilidade

- [GDD — Planejamento inimigo por GOAP](../GDD.md#planejamento-inimigo-por-goap)
- [GDD — Contextos e satisfação dos Desejos](../GDD.md#contextos-e-satisfação-dos-desejos)
- [ADR de pré-produção — diretriz arquitetural transversal](../ADR-PREPRODUCAO.md#13-diretriz-arquitetural-transversal-do-pega)
- [Registro conceitual — fronteiras do PEGA e do Immersive](../../docs/PEGA-GOAP-registro-de-decisoes-conceituais-2026-10-25.md)

