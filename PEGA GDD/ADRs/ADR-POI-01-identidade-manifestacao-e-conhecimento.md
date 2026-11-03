# ADR-POI-01 — Identidade, manifestação e conhecimento de POIs

- **Status:** Aceito quanto à autoridade e à semântica; autoria canônica de Position(ObjectEntryId) implementada. Sensores e integração com Targets/navegação pendentes.
- **Revisado:** 2026-11-03 — posição Object Entry autorada e observável nos escopos Group e Global
- **Data:** 2026-10-30
- **Owner:** PEGA — mundo, identidade de POIs e integração com conhecimento/GOAP

## Contexto

O GDD descreve POIs como objetos relevantes do cenário e distingue o **World State real** dos fatos legitimamente disponíveis ao **Knowledge State**. Conhecer uma entidade não significa conhecer todas as suas propriedades; o GOAP parte de Targets conhecidos e não consulta estado oculto para completar planos.

A decisão técnica aprovada anteriormente criou identidade e resolução física próprias do PEGA. Essa decisão é **substituída**: os contratos instalados do Immersive Framework para identidade `ObjectEntry` e binding entre identidade lógica e ocorrência física não haviam sido devidamente considerados. A decisão anterior duplicava uma autoridade que deve pertencer ao Framework.

Este ADR preserva os requisitos funcionais de POIs, identidade, localização e conhecimento, mas substitui as decisões técnicas incompatíveis. Ele registra a arquitetura, o estado implementado e as integrações que continuam pendentes. A validação manual reportada pelo usuário é identificada como tal; esta atualização não a reexecutou.

## Decisões vigentes

1. **POI continua sendo um objeto individual relevante do mundo.** Sua identidade lógica não é um perfil reutilizável, uma oportunidade ou um comportamento. O POI não precisa ser um Actor; sua identidade é independente de `ActorId` e `ActorProfileId`.
2. **`ObjectEntryId` é a identidade lógica de cada POI.** POIs logicamente distintos no PEGA devem ter IDs distintos em todo o projeto. Uma mesma identidade pode continuar sendo conhecida quando não houver manifestação física. A autoria deve permitir selecionar a identidade sem digitar manualmente o ID.
3. **O Framework é a autoridade sobre manifestações físicas.** `ObjectEntryDeclaration` declara a identidade da ocorrência; os lifecycles Route/Activity do Framework determinam a composição e o tempo de vida aplicáveis. O PEGA não mantém um registro paralelo de manifestações nem define outro lifecycle para elas.
4. **O Framework é a autoridade sobre ocorrências físicas.** A consulta pública Experimental de Object Entry reutiliza o binding existente e retorna Resolved, Unavailable, InvalidReference ou Ambiguous. O owner Route/Activity ao qual a consulta está vinculada determina por quanto tempo a capacidade permanece disponível; ele não filtra as ocorrências consultadas. A referência StableObjectReference passada à consulta contém o ObjectEntryId e pode incluir seletor tipado e exato de Route ou Activity para selecionar a ocorrência; sem desambiguação, múltiplas correspondências permanecem Ambiguous. A consulta é read-only: não cria, materializa, registra ou vincula objetos, não mantém a ocorrência como identidade persistente e não concede conhecimento ou autorização de gameplay. O pacote Framework resolvido localmente pelo PEGA declara 1.2.0-preview.4; veja o [guia oficial de consultas de ocorrência](https://github.com/ImmersiveGames/com.immersive.framework/blob/v1.2.0-preview.4/Documentation~/Guides/Object-Entry-Occurrence-Queries.md).
5. **Identidade, manifestação e Knowledge são responsabilidades separadas.** O binding informa se há uma ocorrência física disponível no owner ativo; não concede conhecimento ao NPC nem publica fatos. A ausência ou indisponibilidade da manifestação não apaga a identidade lógica nem fatos previamente conhecidos.
6. **Identidade conhecida não implica localização conhecida.** Conhecimento sobre um POI pode referenciar sua identidade sem posição. Identidade, localização e outros conhecimentos são fatos distintos no Knowledge State, com proveniência, validade, atualização e invalidação determinadas pelas definições e políticas de cada fato. O Knowledge não depende da presença da manifestação.
7. **Posição conhecida e resolução física são fatos distintos.** Uma posição pode ser autorada inicialmente como conhecimento se a definição tipada canônica permitir essa origem, ou adquirida por uma fonte autorizada que a publique no Knowledge. Em qualquer caso, o fato preserva a política de evidência, validade e escopo da propriedade. Coordenadas autoradas significam que aquele escopo conhece a posição; não simulam, resolvem, materializam ou provam uma ocorrência física. A consulta de ocorrência não preenche nem altera Knowledge por si só. Sem fato de posição válido, a localização continua desconhecida; possuir ObjectEntryId não autoriza inferi-la a partir do World State.
8. **Knowledge e planejamento preservam suas fronteiras.** Conhecimento prévio ou adquirido sobre POIs pode pertencer aos escopos Individual e Group, com o Group e a membership controlando seu acesso conforme o GDD; os três escopos Individual, Group e Global mantêm autoridades e políticas próprias. Compartilhar um fato não compartilha Desejos nem força todos os membros a executar a mesma Action. Uma posição conhecida pode participar do planejamento somente se o fato estiver acessível e as condições do GOAP permitirem; mapear ObjectEntryId e propriedades conhecidas a um Target elegível e à navegação é uma integração PEGA ainda pendente. O GOAP não consulta a ocorrência para completar informação ausente. Actions validam o mundo real quando executam.
9. **O exemplo de gameplay não limita o mecanismo.** POIs, navegação e localizabilidade são aplicações das definições de fatos, fontes e políticas existentes. Uma especialização de fonte ou Action é legítima quando atende seu contrato sem substituir a identidade, o binding ou a autoridade geral de Knowledge.

## Identidade, localização e autorização

Exemplo implementado na Mission: Group e Global associam o mesmo `ObjectEntryId` `pega.warehouse.storage-rack-west-north` à definição canônica `pega.object-entry.position`, com valores distintos por escopo. A definição `Position` permite autoria inicial e mantém evidência própria; uma definição `Type` ainda não está disponível. As coordenadas afirmam apenas o que cada escopo conhece e não comprovam que a ocorrência física esteja carregada ou resolvida. Usar essa posição em condições GOAP, como Target de navegação, depende de integração PEGA ainda pendente. O usuário reportou validação manual no Unity em 2026-11-03.

A referência de autoria usa ObjectEntryId e preserva as opções do Framework para entrada manual e picker de identidades indexadas. Selecionar o endereço identifica o sujeito do fato; não cria propriedades, atribui posição ou concede autorização de Knowledge.

## Consequências e limites desta revisão

- A implementação PEGA anterior de identidade e registro/resolução de manifestações foi retirada. O Framework agora oferece a consulta pública read-only Experimental; este ADR não cria um substituto PEGA nem afirma que a resposta da consulta seja publicada no Knowledge.
- Conhecimento genérico, fatos e políticas, Actors, GOAP e consumidores não exclusivos do mecanismo anterior permanecem em seus owners existentes.
- A definição canônica `pega.object-entry.position` usa `ObjectEntryId` como endereço e admite posição inicial no Knowledge. Isso ainda não representa a posição da ocorrência física nem integra o fato a um Target.
- Permanecem pendentes: outras definições tipadas de POI, como Type; sensores/fontes que legitimamente descubram ou atualizem posição; vínculo entre ObjectEntryId/posição conhecida, condições GOAP, Targets e navegação; escolha do seletor exato pelo consumidor PEGA quando precisar consultar um owner e tratamento explícito de Ambiguous, sem escolha arbitrária; validação de unicidade lógica de IDs; acompanhamento de rotas após mudança de manifestação; e retry/atualização. A consulta pública, seus resultados explícitos e o seletor de owner já são contratos disponíveis do Framework.
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
