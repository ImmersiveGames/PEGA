# ADR-GOAP-01 — Domínio e evolução por composição

- **Status:** Aceito
- **Data:** 2026-10-25; **revisado:** 2026-10-26
- **Owner:** PEGA — integração e regras de gameplay dos larápios

## Contexto

O GDD define as regras e o comportamento do GOAP dos larápios. Este ADR aplica a diretriz arquitetural transversal do PEGA, registrada no [ADR de pré-produção, §13](../ADR-PREPRODUCAO.md#13-diretriz-arquitetural-transversal-do-pega), e a decisão vigente para GOAP e Perception da [§7](../ADR-PREPRODUCAO.md#7-evolução-arquitetural-de-goap-e-perception): os mecanismos são genéricos e reutilizáveis; cada jogo mantém suas próprias regras e integração.

No ecossistema Immersive, identidade, ocorrência, lifecycle e composição do Actor pertencem ao Framework; as regras de gameplay permanecem no jogo que as define. A integração de um mecanismo genérico não transfere ao Framework os Goals, Desejos, conhecimento, Actions, Targets ou políticas de seleção do PEGA. O `IPredicate` booleano do FSM no Foundation não recebe um estado para avaliar e não expressa conhecimento Desconhecido; é um contrato distinto e não pode ser ampliado pelo PEGA.

A implementação corrente contém um recorte demonstrativo, incluindo seleção de planos de uma Action em versões anteriores. Esse registro histórico não define o limite arquitetural. O código presente, por si só, também não comprova que a arquitetura-alvo foi concluída ou validada em execução.

## Decisão

1. O mecanismo GOAP e o mecanismo Perception são genéricos, independentes entre si e reutilizáveis em diferentes jogos. Ambos podem integrar-se ao ecossistema Immersive sem se tornarem dependências obrigatórias um do outro.
2. Nesta etapa, não serão criados novos pacotes. Os mecanismos podem permanecer no repositório PEGA enquanto suas fronteiras são consolidadas. A localização do código não autoriza dependências de gameplay do PEGA no mecanismo genérico.
3. Cada jogo fornece sua própria superfície de integração. No PEGA, Goals, Desejos, fatos e políticas do Knowledge State, elegibilidade de Targets, Actions, efeitos observados, seleção e execução continuam sob as autoridades de gameplay do GDD e dos ADRs semânticos. Perception fornece observações ao seu consumidor; a interpretação delas como fatos e sua ligação com reavaliação GOAP pertencem à integração do jogo. O mecanismo GOAP não depende de tipos ou eventos de Perception, e o mecanismo Perception não depende de Goals, Actions ou do planejador.
4. A composição concreta do Actor determina as mecânicas presentes. O arquétipo configura e especializa capacidades, Actions, Desejos, parâmetros e perfis; não materializa nem remove mecânicas da ocorrência. Prefira um Root físico mínimo. GOAP e Perception podem ocupar objetos filhos quando suas dependências e o lifecycle da ocorrência forem compatíveis com o Framework. Preserve no Root a autoridade física e os componentes de identidade/publicação que os contratos do Framework exigem. Use os mounts e a composição keyed existentes somente quando ownership ou substituição independente justificarem seu uso.
5. Priorize a estrutura mais simples que mantenha essas fronteiras. Classes e dados internos não precisam ser `MonoBehaviour`; use componentes Unity apenas para lifecycle, authoring serializado ou interação com a cena que necessitem deles. Não crie componentes de coordenação, camadas de abstração ou infraestrutura de composição redundantes.
6. O PEGA não modifica nem redefine o predicado booleano de FSM do Foundation para representar condições de GOAP. O mecanismo GOAP mantém condições com fonte de estado explícita e representação de conhecimento incompleto, conforme o [ADR-GOAP-02](ADR-GOAP-02-modelo-causal-e-planejamento.md).

## Alternativas consideradas

- **Mover as regras de gameplay do GOAP ao Framework ou Foundation:** rejeitada; Goals, Desejos, conhecimento, capacidades e regras do PEGA permanecem no jogo. Essa rejeição não se aplica ao mecanismo genérico e reutilizável, cuja integração ao ecossistema Immersive está aprovada.
- **Evoluir `IPredicate` do Foundation para atender o planejador:** rejeitada; o contrato pertence ao FSM, é booleano e sem contexto de estado, além de o pacote Foundation ser uma baseline técnica congelada.
- **Manter uma Action especializada por Desejo como regra do sistema:** rejeitada; isso prende novas soluções ao núcleo de decisão e impede a composição causal definida nos ADRs seguintes.
- **Criar packages para GOAP ou Perception nesta etapa:** fora de escopo pela decisão vigente da §7 do ADR de pré-produção. Essa restrição de etapa não rejeita mecanismos genéricos nem decide se um package será adequado em uma etapa futura.

## Consequências

- Os mecanismos genéricos fornecem contratos e capacidades reutilizáveis; cada jogo mantém a autoridade sobre regras, fatos, configuração e integração. No PEGA, providers/mecânicas oferecem e executam capacidades, e as autoridades de conhecimento e mundo confirmam fatos e Effects observados.
- Os ADRs GOAP-02, -03 e -04 preservam as semânticas que a integração do PEGA deve respeitar; não prescrevem classes, interfaces ou algoritmo de busca. As regras de gameplay continuam no GDD.
- A integração ao Framework respeita sua autoridade de Actor e seus contratos efetivamente disponíveis. Este ADR não declara uma API genérica de GOAP ou Perception já existente no Framework, nem aprova alterações no repositório do Framework.
- O runtime atual não é declarado conforme à arquitetura-alvo por este registro. Sua evolução requer implementação e validação posteriores, fora deste ADR.

## Não decidido

Nomes e fronteiras exatas de assemblies/classes/interfaces, localização de arquivos no repositório, estrutura de assets, formato de serialização, algoritmo de busca e composição serializada no Unity. A criação futura de packages também não está decidida.

## Relação com decisões anteriores

Este ADR revisado substitui as decisões anteriores deste documento que mantinham o mecanismo GOAP exclusivamente no PEGA ou rejeitavam sua generalização; não altera a autoridade do PEGA sobre regras de gameplay. Preserva a fronteira de comportamento definida pelo GDD e a estratégia de produção PP-37; não substitui decisões de Contextos, conhecimento, satisfação, reservas ou escopo do MVP. A associação direta entre Action e Goal observada em um recorte demonstrativo não é contrato normativo.

## Autoridade e rastreabilidade

- [GDD — Planejamento inimigo por GOAP](../GDD.md#planejamento-inimigo-por-goap)
- [GDD — Contextos e satisfação dos Desejos](../GDD.md#contextos-e-satisfação-dos-desejos)
- [ADR de pré-produção — decisão vigente para GOAP e Perception, §7](../ADR-PREPRODUCAO.md#7-evolução-arquitetural-de-goap-e-perception)
- [ADR de pré-produção — diretriz arquitetural transversal, §13](../ADR-PREPRODUCAO.md#13-diretriz-arquitetural-transversal-do-pega)
- [Registro conceitual — fronteiras do PEGA e do Immersive](../../docs/PEGA-GOAP-registro-de-decisoes-conceituais-2026-10-25.md), registro histórico e não normativo; posições anteriores sobre ownership são supersedidas por esta revisão.

