# ADR-GROUP-KNOWLEDGE-01 — Conhecimento compartilhado e prévio de Groups

- **Status:** Aceito quanto aos princípios arquiteturais; decisões de composição e lifecycle técnico ainda abertas
- **Data:** 2026-10-30
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

5. **Conhecimento prévio é uma origem legítima de fatos.** Um Group pode iniciar sua participação em um contexto de gameplay já conhecendo fatos explicitamente autorados, sem necessitar de um ingresso físico em Spawn Point, percepção ou Action para legitimá-los. Esses fatos entram na autoridade coletiva com escopo, origem, validade e política apropriados; sua configuração inicial não se confunde com a identidade do Group. Conhecimento adquirido depois, por fontes legítimas, usa a mesma autoridade geral de conhecimento, respeitando as políticas de cada fato.

6. **Os escopos não se confundem.** O conhecimento Individual continua pertencendo ao agente; não é propagado ao Group automaticamente. Conhecimento coletivo é disponibilizado aos membros autorizados segundo sua política; membership não equivale a copiar fatos nem a conceder conhecimento de outras entidades ou grupos. O escopo Global continua distinto, com distribuição explícita prevista no GDD. Nenhum desses escopos implica compartilhamento de Desejos, decisões, satisfação ou execução de Actions.

7. **As fontes e os consumidores são desacoplados.** Fontes podem produzir fatos prévios ou adquiridos em runtime; o Knowledge State decide sua aceitação e manutenção. O GOAP, Actions, Desires e diagnóstico consomem somente as informações legitimamente acessíveis, preservando proveniência e estados conhecidos/desconhecidos. O planejamento não consulta o World State oculto nem converte, por conveniência, uma referência de Group ou de entidade em conhecimento adicional.

8. **A inicialização do Group não é efeito implícito de consultar conhecimento.** A composição deve usar o contrato oficial de criação e associação do Framework no owner/lifecycle adequado. A autoria da cena, a existência de um componente ou um binding serializado não substituem a efetiva criação do Group e a confirmação de seus membros. A responsabilidade concreta por coordenar isso deve ser determinada em uma etapa própria, sem recuperar automaticamente a antiga composição de ingresso ou acoplar a inicialização de Groups a Spawn Points.

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

- Quais classes, interfaces, assets, campos do Inspector ou estruturas internas realizarão o contrato.
- Qual componente/participante concreto coordena criação e membership, e se o Group Knowledge acompanha o owner Session, Route ou Activity, ou outra composição admitida pelo Framework. Seu lifecycle deve ser compatível com o owner real do Group, sem presumir persistência além dele.
- Como representar e combinar múltiplas memberships de um mesmo Actor, quando essa capacidade for necessária.
- A representação técnica da autoria inicial, inicialização de fatos, distribuição de mudanças, conflitos entre fontes, retenção e eventual reentrada de Groups. Aplicam-se enquanto isso as políticas semânticas gerais do GDD, não uma política técnica presumida.
- A implementação do Knowledge State Global, fontes concretas de percepção/investigação e navegação até POIs, inclusive localizabilidade prévia. O [ADR-POI-01](ADR-POI-01-identidade-manifestacao-e-conhecimento.md) continua responsável por identidade e localização de POIs.
- A atualização de nomes de classes e arquivos históricos, a reversão ou preservação de cada componente existente e qualquer mudança no Framework. Essas decisões exigem investigação específica do workspace e dos contratos efetivos.

## Relação com o estado atual e decisões existentes

O código `PEGANpcGangKnowledgeState` e `PEGANpcGangMembership` representa um recorte histórico especializado; não se torna normativo pelo nome ou por estar presente na cena. O antigo `PEGANpcMissionSpawnEntry` misturava inicialização de Groups e ingresso físico de NPC: sua remoção não elimina a necessidade de Group vivo, mas também não autoriza sua restauração automática.

Este ADR **não altera** o GDD: na ficção e nas regras do PEGA, o escopo continua chamado **Gangue**. Define-se apenas que sua infraestrutura técnica de conhecimento coletivo deve se apoiar na abstração geral **Group**. O ADR não declara capacidade nova já implementada, validação do Unity ou aprovação para reintroduzir POIs.

## Autoridade e rastreabilidade

- [GDD — Conhecimento modular, memória e validade da informação](../GDD.md#conhecimento-modular-memória-e-validade-da-informação)
- [GDD — Conhecimento da gangue e informação global](../GDD.md#conhecimento-da-gangue-e-informação-global)
- [ADR-GOAP-01 — Domínio e evolução por composição](ADR-GOAP-01-composicao-e-fronteiras.md)
- [ADR-GOAP-02 — Condições, Effects e planejamento causal](ADR-GOAP-02-modelo-causal-e-planejamento.md)
- [ADR-POI-01 — Identidade, manifestação e conhecimento de POIs](ADR-POI-01-identidade-manifestacao-e-conhecimento.md)
- Auditoria de leitura da base no commit `73e8f60`; contratos instalados `GroupProfile` e `ActorGroupAccessBinding` do Immersive Framework (Experimental)
