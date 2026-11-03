# ADR-GROUP-KNOWLEDGE-01 — Conhecimento compartilhado e prévio de Groups

- **Status:** Princípios arquiteturais preservados; Knowledge Individual, Group e Global, autoria inicial, diagnóstico e integração de posição ObjectEntry do Group ao GOAP implementados no PEGA. O início do deslocamento foi reportado visualmente no Unity; chegada e satisfação final do Goal não estão comprovadas neste registro.
- **Data:** 2026-10-30
- **Revisado:** 2026-11-03 — autoria inicial, diagnóstico e integração Group Knowledge → Target/Action/GOAP; início do deslocamento reportado no Play Mode
- **Owner:** PEGA — integração de Actor Groups e conhecimento

## Contexto

O GDD estabelece três escopos de conhecimento — **Individual, Gangue e Global** — e exige fatos independentes, proveniência, validade, políticas de atualização e propagação. No gameplay, a **Gangue** é a unidade de cooperação dos larápios. No Immersive Framework, **Actor Group** é a abstração técnica genérica de identidade e membership, utilizável por outros tipos de agrupamento.

A auditoria da base após o commit `73e8f60` (`Reset`) identificou que o conhecimento individual já possui implementação e evidências anteriores de execução, enquanto a implementação coletiva existente é especializada em fatos de Spawn Point. A remoção de `PEGANpcMissionSpawnEntry` também eliminou os únicos chamadores conhecidos de criação dos Groups vivos e associação dos membros da cena. Permaneceram referências de Group e componentes de conhecimento, mas **configuração serializada não comprova Group materializado nem membership estabelecida**. A auditoria foi estática; não atesta o comportamento do runtime após o corte.

Acrescentar conhecimento prévio a essa especialização, sem antes definir a responsabilidade geral do conhecimento coletivo, produziria dependências artificiais entre Gangue, Spawn Point, POI, composição da Activity e GOAP. Este ADR fixa a intenção arquitetural e seus limites, sem presumir que o código atual a realize.

## Decisão

1. **Group é o conceito técnico; Gangue é domínio do jogo.** A capacidade de conhecimento coletivo deve aplicar-se a qualquer Actor Group autorizado, sem depender de que seja uma Gangue de larápios. O GDD continua usando **Gangue** como escopo e regra de gameplay do PEGA; este ADR não substitui essa terminologia nos documentos de design.

2. **O conhecimento compartilhado pertence logicamente ao Group, não a seus membros.** Cada Group possui seu conjunto de fatos e suas políticas de conhecimento, identificado e isolado em relação a outros Groups. Um NPC não se torna proprietário do fato coletivo por consultá-lo. A implementação pode oferecer uma vista de leitura aos membros sem copiar todo o estado coletivo para cada Knowledge State Individual.

3. **Identidade/membership e conhecimento possuem autoridades distintas.** O Immersive Framework é autoridade para identidade, existência, membership e lifecycle de Actor Groups. O PEGA é autoridade para conteúdo dos fatos, fontes, políticas de conhecimento e sua integração com os consumidores de gameplay. `GroupProfile` identifica/configura um Group; não é, por si só, um banco de fatos nem prova de que o Group esteja vivo.

4. **Acesso depende de participação autorizada e vigente.** Referenciar o mesmo `GroupProfile`, um componente de conhecimento ou um identificador não concede acesso automático. A disponibilização e a atualização do conhecimento coletivo devem respeitar o Group real, o owner de conteúdo pertinente, a membership confirmada pelo Framework e as políticas aplicáveis. Não se deve contornar essa verificação para fazer um demonstrador funcionar.

5. **Conhecimento prévio é uma origem legítima de fatos.** Um Group pode iniciar sua participação em um contexto de gameplay já conhecendo fatos explicitamente autorados, sem necessitar de ingresso físico em Spawn Point, percepção ou Action para legitimá-los. A autoria declarativa segue o [ADR-GOAP-02](ADR-GOAP-02-modelo-causal-e-planejamento.md): o designer seleciona definições tipadas canônicas disponíveis e fornece valores compatíveis; propriedades, tipos e políticas novos são responsabilidade de programação. Fatos iniciais, adquiridos por sensores ou disponibilizados a membros autorizados usam a mesma autoridade de Knowledge e os mesmos contratos de evidência, validade e escopo. Esses fatos entram no conjunto do Group com a política apropriada; sua configuração inicial não se confunde com a identidade do Group nem prova uma ocorrência física.

6. **Os escopos não se confundem.** O conhecimento Individual continua pertencendo ao agente; não é propagado ao Group automaticamente. Conhecimento coletivo é disponibilizado aos membros autorizados segundo sua política; membership não equivale a copiar fatos nem a conceder conhecimento de outras entidades ou grupos. Fatos no escopo Global são consultáveis por qualquer consumidor GOAP do contexto de gameplay correspondente, sem membership. Isso define acesso à autoridade Global, não a produção automática de fatos ou a propagação entre escopos; fontes e publicação continuam explícitas segundo o GDD. Nenhum desses escopos implica compartilhamento de Desejos, decisões, satisfação ou execução de Actions.

7. **As fontes e os consumidores são desacoplados.** Fontes podem produzir fatos prévios ou adquiridos em runtime; o Knowledge State decide sua aceitação e manutenção. O GOAP, Actions, Desires e diagnóstico consomem somente as informações legitimamente acessíveis, preservando proveniência e estados conhecidos/desconhecidos. O planejamento não consulta o World State oculto nem converte, por conveniência, uma referência de Group ou de entidade em conhecimento adicional.

   **Esclarecimento de aplicação do GDD:** identificadores técnicos oficiais, como `ActorId` e `ObjectEntryId`, servem para endereçar fatos sobre entidades; possuir uma referência não significa que o NPC conheça a entidade ou suas propriedades. Conhecimentos sobre papel ou categoria de gameplay, localização, estado, conteúdo e outras características são fatos independentes, adquiridos e disponibilizados segundo suas próprias evidências e políticas. Nome, aparência e `ActorProfileId` não são requisitos gerais de identificação para o GOAP: só interessam quando uma regra concreta de gameplay precisar dessas informações. Fontes autorizadas podem consultar o Framework para produzir fatos; consumidores não podem completar fatos ausentes recorrendo à Player Session, ao Profile ou à manifestação física. Este esclarecimento não introduz um sistema de reconhecimento nem altera as regras do GDD.

8. **A inicialização do Group não é efeito implícito de consultar conhecimento.** A composição deve usar o contrato oficial de criação e associação do Framework no owner/lifecycle adequado. A autoria da cena, a existência de um componente ou um binding serializado não substituem a efetiva criação do Group e a confirmação de seus membros. No PEGA, a composição da Mission cria os Groups pelo binding oficial; a authority opcional de Knowledge aplica fatos iniciais somente após confirmar o Group vivo criado por esse owner, sem recuperar a antiga composição de ingresso ou acoplar a inicialização de Groups a Spawn Points.

9. **Especializações não definem o contrato geral.** Gangues, Spawn Points e POIs podem fornecer fatos e regras específicas do PEGA, mas não podem ser pré-requisitos para a capacidade geral de conhecimento de Groups. É permitido preservar e adaptar peças existentes quando sua responsabilidade for legítima; não se aprova a criação de autoridades paralelas, camadas redundantes ou mudanças no Framework sem necessidade demonstrada.

## Consequências e critérios de avaliação

- Deve ser possível representar dois Groups distintos com fatos coletivos distintos, sem vazamento de conhecimento entre eles.
- Deve ser possível autorar um fato inicial de Group e disponibilizá-lo a um membro autorizado, **sem** inventar uma observação física ou criar um Target artificial.
- Um Actor sem membership vigente não deve receber fatos coletivos apenas porque possui referências serializadas.
- Um membro admitido depois pode acessar os fatos ainda válidos do Group conforme a política aplicável, sem exigir reexecução das fontes originais.
- Fatos Individual, de Group e Global permanecem distinguíveis por escopo e proveniência; compartilhar um fato não determina uma Action ou um Desire.
- Uma configuração com `GroupProfile` mas sem Group vivo não atende à capacidade, mesmo que o código compile e a cena não apresente referências órfãs.
- A avaliação deve começar pela fidelidade à propriedade coletiva, às autoridades e às políticas de acesso. Somente depois deve verificar composição técnica e comportamento em Unity; um NPC mover-se ou uma Action funcionar, isoladamente, não demonstra conformidade.

## Não decidido neste ADR

- Como representar e combinar múltiplas memberships de um mesmo Actor, quando essa capacidade for necessária.
- A distribuição de mudanças, os conflitos entre fontes, a retenção e eventual reentrada de Groups permanecem pendentes. A semântica comum de autoria continua vinculada ao ADR-GOAP-02; autoria genérica por assets de qualquer condição GOAP permanece pendente. Esta demonstração adiciona condições PEGA tipadas de aplicabilidade e Goals de investigação/saída, sem torná-las uma superfície universal.
- Fontes concretas de investigação para fatos Globais, sensores de Object Entry, produtores de eventos reais do assalto e propagação entre escopos continuam pendentes; consumidores GOAP do contexto podem compor authority Global sem membership. A demonstração Second Group usa apenas a authority Group para seus POIs e inclui investigação observada e saída após o conjunto obrigatório. Outras propriedades/fontes e formas de descobrir ou atualizar posições continuam futuras. O [ADR-POI-01](ADR-POI-01-identidade-manifestacao-e-conhecimento.md) fixa a autoridade de ObjectEntryId/ocorrência e distingue posição conhecida de manifestação física.
- A atualização de nomes de classes e arquivos históricos, a reversão ou preservação de cada componente existente e qualquer mudança no Framework. Essas decisões exigem investigação específica do workspace e dos contratos efetivos.

## Relação com o estado atual e decisões existentes

O código `PEGANpcGangKnowledgeState` e `PEGANpcGangMembership` representa um recorte histórico especializado; não se torna normativo pelo nome ou por estar presente na cena. O antigo `PEGANpcMissionSpawnEntry` misturava inicialização de Groups e ingresso físico de NPC: sua remoção não elimina a necessidade de Group vivo, mas também não autoriza sua restauração automática.

Este ADR preserva **Gangue** como termo de gameplay do GDD e **Group** como identidade/membership técnica do Framework. A autorização para ler fatos Globais não depende de membership; leituras de fatos Group dependem da membership vigente confirmada pelo Framework. Os registros abaixo separam implementação, prova manual relatada e funcionalidades que continuam pendentes.

### Implementação PEGA — primeira etapa de Global Knowledge (2026-11-02)

A [implementação registrada](../Trabalho/reports/2026-11-02-global-knowledge-implementation.md) adiciona uma autoridade de fatos Global pertencente a uma contribuição da Activity da Mission. Ela reutiliza `PEGAKnowledgeFactStore` no escopo `Global`, definições tipadas e a política de evidência existente. `IPEGAKnowledgeFactReader` é composta como capability de uma vista GOAP; o mecanismo genérico de GOAP continua sem dependência de tipos PEGA.

A autoridade Global é compartilhada pelos consumidores GOAP explicitamente compostos no contexto e não depende de membership. Individual permanece em armazenamento e escopo próprios. A prova `PEGAGlobalKnowledgeProofWindow` foi executada manualmente no Unity e teve resultado PASS, conforme relato do usuário; o Codex não executou a prova. A produção de fatos por eventos reais e a propagação entre escopos permanecem pendentes.

### Implementação PEGA — Group Knowledge (2026-11-02)

A [implementação registrada](../Trabalho/reports/2026-11-02-group-knowledge-implementation.md) mantém uma authority PEGA opcional junto à composição de Groups do owner Activity/Route. Ela cria stores genéricos independentes para os `GroupId` reais confirmados pelo Framework; a criação do Actor Group não requer o componente de Knowledge. Cada leitura verifica novamente o Group vivo e a membership oficial vigente por `ActorGroupAccessBinding`; uma consulta sem autorização retorna `NotMember` e um fato vazio `Unknown`, sem valor ou evidência. A saída revoga o acesso e o encerramento do owner limpa os stores.

A composição PEGA cria os Groups por `ActorGroupAccessBinding` no owner Activity/Route comprometido e configura membership no Framework. Group Knowledge é anexado opcionalmente ao mesmo owner; sua ausência ou falha de registro não impede a criação do Group. Esta implementação foi comprovada na Activity da Mission; outras combinações de owners/contextos não fizeram parte da prova.

A prova `PEGAGroupKnowledgeProofWindow` foi executada manualmente no Unity e teve resultado PASS, conforme relato do usuário; o Codex não executou a prova. A execução usou os três larápios e dois Groups da Mission para conferir compartilhamento entre os dois membros do primeiro Group, fato separado no segundo, isolamento das leituras, revogação/restauração de acesso por Leave/Join, acesso Global e independência do Knowledge Individual. Não executou Actions nem decisões de planejamento.

### Autoria inicial e diagnóstico (2026-11-03)

A evolução posterior reutiliza um catálogo canônico de definições e a mesma linha serializada **Identidade → Propriedade → Valor** nos escopos Individual, Group e Global. Os fatos entram nos stores e authorities existentes, preservando as políticas, evidências, temporalidade e validade próprias de cada definição. Fatos Group só são inicializados para Groups realmente criados pelo owner; leituras do Inspector confirmam novamente Group vivo e membership atual antes de mostrar valores.

Na etapa anterior de autoria inicial/diagnóstico em `MissionActivityContent`, o Second Group `pega.mission.actor-group.second` e a authority Global usavam a definição `pega.object-entry.position` para o mesmo `ObjectEntryId` `pega.warehouse.storage-rack-west-north`, com valores `(-5, 1, 15.700001)` e `(200, 0, 200)`. Na configuração final, os fatos Group desse rack foram substituídos pelos três POIs de investigação e pela saída. O fato Global antigo do rack permanece independente e não está composto na vista GOAP de `Larapio NPC - Second Group Demo`; nenhum fato Global fornece as posições da atividade.

O usuário reportou validação manual no Unity em 2026-11-03, incluindo conhecimento inicial, membership autorizada, acesso negado, isolamento entre Groups e leitura Global. Esta revisão não repetiu essas provas. Autoria genérica de qualquer condição GOAP, sensores de Object Entry, produtores de eventos de gameplay e propagação entre escopos continuam pendentes. A integração concreta de posição Group ao GOAP, investigação observada de três POIs e navegação até a saída, com Goals satisfeitos segundo relato visual e logs corroborantes, está registrada no [relatório Group Knowledge](../Trabalho/reports/2026-11-02-group-knowledge-implementation.md). Os logs brutos não foram inspecionados pelo Codex.

### Integração de posição ObjectEntry do Group ao GOAP (2026-11-03)

A Action `NavigateToKnownGroupObjectEntryPosition` enumera fatos válidos da definição canônica `pega.object-entry.position` pelo `PEGAActorGroupKnowledgeReader`. O Reader só retorna stores de Groups encontrados pela consulta de membership efetiva do Framework e preserva o `GroupId` proprietário. A Action transforma cada fato autorizado em Target tipado por `GroupId` e `ObjectEntryId`; não lê transform, ocorrência física, World State, Global Knowledge nem Knowledge Individual para descobrir ou completar a posição.

O GOAP cria a Action causal com Effect hipotético de `TargetReached`, avalia a disponibilidade da rota antes de expandir esse Effect e seleciona entre Goals/planos válidos pela política normal de Desire, custo e score. Na execução, a navegação usa a posição conhecida; o Effect só é observado após o resultado de sucesso e a confirmação de chegada, com membership e fato Group ainda válidos. A condição do Goal consulta a mesma authority Group e não considera conhecimento da coordenada, sozinho, como chegada. Nenhuma regra de seleção absoluta foi adicionada.

Na configuração inicial da demonstração, a posição Group do rack coincidia com o centro de seu collider; a cena inclui essa geometria na construção da NavMesh. Como a Action exige rota completa até a posição conhecida, essa autoria era incompatível com uma chegada caminhável e é consistente com os logs de um candidato sem plano. Os logs fornecidos não registram o status concreto da rota, então a rejeição específica do destino original é uma inferência estática ainda sem diagnóstico de execução. A configuração atual usa o POI próprio `pega.warehouse.second-group-demo-poi`, declarado pelo mecanismo oficial de identidade e com posição inicial explícita somente no Second Group. O Goal/Desire do NPC referencia essa identidade; a configuração Global antiga do rack permanece em sua authority separada e não participa deste Target/Goal.

**Evidência daquela etapa de navegação a um POI:** composição, definições, referências, leitores e caminho causal estavam presentes estaticamente no PEGA; a revisão arquitetural independente do corte foi `CLEAR` estático. O responsável reportou visualmente o início do deslocamento. A validação final posterior da atividade completa está registrada na última atualização do [relatório de implementação](../Trabalho/reports/2026-11-02-group-knowledge-implementation.md); esta etapa inicial isolada não registrava chegada.

## Autoridade e rastreabilidade

- [GDD — Conhecimento modular, memória e validade da informação](../GDD.md#conhecimento-modular-memória-e-validade-da-informação)
- [GDD — Conhecimento da gangue e informação global](../GDD.md#conhecimento-da-gangue-e-informação-global)
- [ADR-GOAP-01 — Domínio e evolução por composição](ADR-GOAP-01-composicao-e-fronteiras.md)
- [ADR-GOAP-02 — Condições, Effects e planejamento causal](ADR-GOAP-02-modelo-causal-e-planejamento.md)
- [ADR-POI-01 — Identidade, manifestação e conhecimento de POIs](ADR-POI-01-identidade-manifestacao-e-conhecimento.md)
- Auditoria de leitura da base no commit `73e8f60`; contratos instalados `GroupProfile` e `ActorGroupAccessBinding` do Immersive Framework (Experimental)
