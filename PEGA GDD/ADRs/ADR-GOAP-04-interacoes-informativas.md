# ADR-GOAP-04 — Tentativas informativas e evidências de interação

- **Status:** Aceito
- **Data:** 2026-10-25
- **Owner:** PEGA — mecânicas de interação, conhecimento e GOAP

## Contexto

Uma interação pode ter requisitos de sucesso ainda desconhecidos. Tratar conhecimento incompleto como se fosse sucesso possível torna o plano onisciente; proibir qualquer tentativa impede que uma mecânica informe legitimamente o motivo de uma recusa. O resultado da execução também não é a mesma coisa que o Effect físico ou o conhecimento que a tentativa produziu.

## Decisão

1. **Condições para tentar** uma interação são distintas das **Preconditions de sucesso**. A mecânica define quando uma tentativa é legítima. Uma precondição de sucesso Desconhecida nunca é considerada satisfeita para prever sucesso.
2. Quando a mecânica permitir, o GOAP pode considerar uma tentativa informativa entre alternativas, sem assumir que terá sucesso nem conhecer antecipadamente a informação revelada.
3. Uma interação pode informar requisitos reais que impediram seu sucesso — inclusive informação privilegiada não disponível aos sensores comuns — somente quando essa revelação for definida pela regra da mecânica. Isso não revela automaticamente a localização de recursos necessários.
4. Resultado de execução, Effects físicos observados e informação descoberta são resultados distintos. A recusa não confirma Effects físicos de sucesso, não conta como conclusão bem-sucedida da Action recusada e não acumula a satisfação correspondente. Conhecimento só é atualizado por informação efetivamente obtida e autorizada.
5. Uma evidência autorizada pode provocar reavaliação por evento. Se os impedimentos conhecidos continuam iguais, a mesma tentativa não pode repetir-se indefinidamente sem justificativa nova.
6. Logs textuais não são autoridade de conhecimento. Providers/mecânicas reportam resultados conforme seus contratos; as autoridades do Knowledge State resolvem a informação; o GOAP avalia a decisão seguinte.
7. Este ADR fixa as semânticas, não o formato de código. A representação técnica do resultado informativo deve preservar a separação acima e os contratos existentes, sem inferir uma enumeração, interface ou canal específico.

## Alternativas consideradas

- **Tratar desconhecido como verdadeiro e planejar sucesso garantido:** rejeitada; confunde incerteza com evidência.
- **Proibir tentativa até todas as Preconditions serem conhecidas:** rejeitada; remove oportunidades informativas que o gameplay autorizou.
- **Aplicar Effects declarados quando a interação é recusada:** rejeitada; permite que o planejamento confirme resultado que não ocorreu.
- **Extrair conhecimento de mensagens de log ou mensagens de diagnóstico não autorizadas:** rejeitada; texto de apresentação não é evidência tipada nem fonte de autoridade.

## Consequências

- A arquitetura pode representar uma tentativa informativa como alternativa sem afirmar que o objetivo físico será alcançado.
- Knowledge State, execução da Action e World State continuam independentes; uma descoberta real não materializa o recurso nem altera sua localização.
- Replanejamento pode reagir à evidência obtida sem criar polling, timeout genérico ou repetição cega.
- A regra de quais interações revelam quais requisitos continua sob autoridade do GDD e da mecânica responsável.

## Não decidido

Enum/categoria técnica para recusa, payload e serialização da evidência, forma de comunicação entre provider e Knowledge State e mecanismo de deduplicação. Esses detalhes devem ser escolhidos durante implementação, sem mudar os contratos desta decisão.

## Relação com decisões anteriores

Refina o contrato existente de Actions para distinguir requisitos de sucesso de autorização de tentativa e para permitir resultado informativo sem sucesso físico. Mantém a regra de que Effects de conclusão só são confirmados após sucesso observado e deixa a representação do resultado em aberto.

## Autoridade e rastreabilidade

- [GDD — Contrato de Actions](../GDD.md#contrato-de-actions)
- [GDD — Conhecimento modular, memória e validade da informação](../GDD.md#conhecimento-modular-memória-e-validade-da-informação)
- [GDD — Áreas, POIs e investigação](../GDD.md#áreas-pois-e-investigação)
- [Registro conceitual — interações com requisitos desconhecidos](../../docs/PEGA-GOAP-registro-de-decisoes-conceituais-2026-10-25.md)

