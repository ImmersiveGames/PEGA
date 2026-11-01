# ADR-POI-01 — Identidade, manifestação e conhecimento de POIs

- **Status:** Aceito; decisão técnica revisada para reutilizar os contratos do Immersive Framework
- **Revisado:** 2026-11-01 — adoção de arquitetura Framework-first
- **Data:** 2026-10-30
- **Owner:** PEGA — mundo, identidade de POIs e integração com conhecimento/GOAP

## Contexto

O GDD descreve POIs como objetos relevantes do cenário e distingue o **World State real** dos fatos legitimamente disponíveis ao **Knowledge State**. Conhecer uma entidade não significa conhecer todas as suas propriedades; o GOAP parte de Targets conhecidos e não consulta estado oculto para completar planos.

A decisão técnica aprovada anteriormente criou identidade e resolução física próprias do PEGA. Essa decisão é **substituída**: os contratos instalados do Immersive Framework para identidade `ObjectEntry` e binding entre identidade lógica e ocorrência física não haviam sido devidamente considerados. A decisão anterior duplicava uma autoridade que deve pertencer ao Framework.

Este ADR preserva os requisitos funcionais de POIs, identidade, localização e conhecimento, mas substitui as decisões técnicas incompatíveis. Ele registra a arquitetura alvo e as dependências pendentes; não declara que a nova integração esteja implementada ou validada.

## Decisões vigentes

1. **POI continua sendo um objeto individual relevante do mundo.** Sua identidade lógica não é um perfil reutilizável, uma oportunidade ou um comportamento. O POI não precisa ser um Actor; sua identidade é independente de `ActorId` e `ActorProfileId`.
2. **`ObjectEntryId` é a identidade lógica de cada POI.** POIs logicamente distintos no PEGA devem ter IDs distintos em todo o projeto. Uma mesma identidade pode continuar sendo conhecida quando não houver manifestação física. A autoria deve permitir selecionar a identidade sem digitar manualmente o ID.
3. **O Framework é a autoridade sobre manifestações físicas.** `ObjectEntryDeclaration` declara a identidade da ocorrência; os lifecycles Route/Activity do Framework determinam a composição e o tempo de vida aplicáveis. O PEGA não mantém um registro paralelo de manifestações nem define outro lifecycle para elas.
4. **A resolução física usa o binding existente do Framework.** O Framework deverá oferecer uma API pública read-only para resolver um `ObjectEntryId` no contexto/owner aplicável, reutilizando o binding oficial. Essa API é uma dependência futura: na versão instalada, os mecanismos de binding/resolução são internos e o snapshot público de Object Entries expõe descritores, não a ocorrência física. Nenhuma API do Framework foi implementada por este ADR.
5. **Identidade, manifestação e Knowledge são responsabilidades separadas.** O binding informa se há uma ocorrência física disponível no owner ativo; não concede conhecimento ao NPC nem publica fatos. A ausência ou indisponibilidade da manifestação não apaga a identidade lógica nem fatos previamente conhecidos.
6. **Identidade conhecida não implica localização conhecida.** Conhecimento sobre um POI pode referenciar sua identidade sem posição. Identidade, localização e outros conhecimentos são fatos distintos no Knowledge State, com proveniência, validade, atualização e invalidação determinadas pelas definições e políticas de cada fato. O Knowledge não depende da presença da manifestação.
7. **A localização só é adquirida por evidência ou autorização legítima.** A decisão funcional de distinguir um POI conhecido de um POI conhecido e previamente localizável permanece válida. Quando explicitamente autorizado, uma fonte legítima poderá resolver a manifestação e publicar sua posição como fato de localização, sujeito às políticas de Knowledge. A autorização para localização não decorre apenas de possuir a identidade. Sem evidência válida, a posição continua desconhecida; não se autoram coordenadas manuais para simular resolução.
8. **Conhecimento e planejamento preservam as fronteiras do GDD.** Conhecimento prévio ou adquirido sobre POIs pode pertencer aos escopos Individual e Group, com o Group e a membership controlando seu acesso conforme o GDD; os três escopos Individual, Group e Global mantêm autoridades e políticas próprias. Compartilhar um fato não compartilha Desejos nem força todos os membros a executar a mesma Action. A localização só pode tornar o POI candidato a navegação quando o fato válido estiver disponível e as condições do GOAP permitirem. O GOAP consome fatos disponíveis no Knowledge State; não consulta a ocorrência física para completar informação oculta. Actions validam o mundo real quando executam.
9. **O exemplo de gameplay não limita o mecanismo.** POIs, navegação e localizabilidade são aplicações das definições de fatos, fontes e políticas existentes. Uma especialização de fonte ou Action é legítima quando atende seu contrato sem substituir a identidade, o binding ou a autoridade geral de Knowledge.

## Identidade, localização e autorização

Um POI pode ser conhecido antes de sua manifestação física estar carregada. Identidade conhecida e posição conhecida continuam estados distintos. A configuração/política de localizabilidade prévia autoriza uma fonte apropriada a tentar adquirir localização pela manifestação ativa; não equivale à localização já obtida. A resolução pode falhar por ausência de ocorrência ou por owner/lifecycle indisponível, sem invalidar por si só a identidade conhecida.

A autoria do Knowledge deve referenciar `ObjectEntryId` por uma superfície selecionável pelo designer, sem exigir digitação de IDs. O Inspector atual de `ObjectEntryDeclaration` pode gerar/copiar um ID, mas não constitui sozinho uma superfície de autoria/seleção de identidades para os consumidores de Knowledge do PEGA. Essa integração de autoria e a API pública de resolução permanecem trabalho futuro.

## Consequências e limites desta revisão

- A implementação PEGA anterior de identidade e registro/resolução de manifestações foi retirada. Esta revisão não cria uma substituição temporária.
- Conhecimento genérico, fatos e políticas, Actors, GOAP e consumidores não exclusivos do mecanismo anterior permanecem em seus owners existentes.
- A reconstrução deverá conectar as referências de Knowledge de POIs a `ObjectEntryId` e consumir a API read-only do Framework, sem acoplar Knowledge à ocorrência física.
- Permanecem para decisões de integração: o contrato e a superfície de seleção da API pública; a seleção explícita do owner Route/Activity quando houver mais de um contexto elegível, sem escolher silenciosamente uma ocorrência ambígua; validação de unicidade lógica de IDs no PEGA; regras do Framework para declarações da mesma identidade em owners distintos e em conjuntos compostos; como POIs sem localizabilidade prévia descobrem localização por percepção, investigação ou comunicação; acompanhamento de rotas após mudanças de manifestação; e detalhes de retry/atualização da localização.
- A antiga decisão técnica foi substituída porque deixou de reutilizar os contratos de identidade e binding já disponíveis no Framework. Essa correção documental e a remoção da implementação PEGA não implementam nem validam a futura arquitetura.

## Relação com decisões existentes

Este ADR detalha identidade, manifestação e conhecimento de POIs conforme o GDD, subordinado às autoridades de Knowledge e GOAP. Não altera as regras de gameplay do GDD, os escopos Individual/Group/Global ou os ADRs de GOAP. Qualquer extensão necessária ao Framework deve ocorrer no repositório proprietário, reutilizar o binding existente e permanecer aditiva, sem criar uma autoridade fundamentalmente nova.

## Autoridade e rastreabilidade

- [GDD — Áreas, POIs e investigação](../GDD.md#áreas-pois-e-investigação)
- [GDD — Conhecimento modular, memória e validade da informação](../GDD.md#conhecimento-modular-memória-e-validade-da-informação)
- [GDD — Conhecimento da gangue e informação global](../GDD.md#conhecimento-da-gangue-e-informação-global)
- [GDD — Contrato de Actions](../GDD.md#contrato-de-actions)
- [ADR-GROUP-KNOWLEDGE-01 — Conhecimento compartilhado e prévio](ADR-GROUP-KNOWLEDGE-01-conhecimento-compartilhado-e-previo.md)
- [ADR-GOAP-01 — Domínio e evolução por composição](ADR-GOAP-01-composicao-e-fronteiras.md)
- [ADR-GOAP-02 — Condições, Effects e planejamento causal](ADR-GOAP-02-modelo-causal-e-planejamento.md)
- [ADR-GOAP-04 — Tentativas informativas e evidências de interação](ADR-GOAP-04-interacoes-informativas.md)
