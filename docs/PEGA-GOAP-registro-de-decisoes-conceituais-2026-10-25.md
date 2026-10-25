# PEGA — Registro consolidado das decisões conceituais do GOAP

**Data:** 25 de outubro de 2026  
**Situação:** decisões de produto e diretrizes arquiteturais acordadas em discussão; documento-base para reconciliação com o GDD e posterior formalização em ADRs.  
**Âmbito:** inteligência dos larápios e diretriz transversal de arquitetura do PEGA.  
**Natureza:** descreve **o que o sistema deve possibilitar e por quê**, sem prescrever classes, algoritmos, arquivos ou estruturas de Unity.

> **Regra de autoridade:** este documento registra as decisões amadurecidas na discussão. O GDD continua sendo a autoridade das regras de gameplay vigentes, até sua revisão explícita; ADRs futuros registrarão as decisões de arquitetura. Divergências devem ser reconciliadas sem apagar o histórico nem substituir decisões silenciosamente. A demonstração atual do MVP não define os limites do sistema final.

## 1. Diretriz transversal: modularidade, intercambialidade e escalabilidade

Os sistemas do PEGA devem crescer **preferencialmente por composição** de capacidades e responsabilidades independentes. Novas mecânicas, comportamentos, fontes de conhecimento, critérios de avaliação e perfis devem poder ser acrescentados ou substituídos sem exigir alterações recorrentes nos respectivos núcleos.

- **Modularidade:** responsabilidades bem delimitadas e dependências justificadas.
- **Intercambialidade:** alternativas compatíveis podem cumprir um mesmo contrato e ser combinadas por configuração.
- **Escalabilidade:** expansão do repertório e da complexidade com estrutura sustentável e custos operacionais controláveis.

A modularidade não exige abstrair tudo. Soluções específicas são legítimas onde a generalização não agrega valor. O núcleo muda quando surgir uma **necessidade semântica geral comprovada**, não simplesmente porque foi adicionada uma nova mecânica de gameplay. Essa diretriz vale além do GOAP.

## 2. Fronteiras do PEGA e do ecossistema Immersive

O GOAP é uma capacidade de gameplay dos larápios e permanece no domínio do **PEGA**, sem extração antecipada para um pacote genérico. O Framework continua responsável por identidade, ocorrência e lifecycle do Actor; as mecânicas reais são fornecidas pela composição concreta do Actor; o arquétipo **configura e especializa**, sem criar mecanicamente essas ocorrências ou componentes.

O GOAP decide a intenção, avalia soluções e seleciona planos. Mecânicas/providers especializados continuam responsáveis por validação e execução física; as autoridades de estado e conhecimento confirmam fatos e resolvem propagação. Recursos do Immersive devem ser reutilizados quando forem adequados, sem transferir a eles responsabilidades de gameplay que não lhes pertencem. O predicado booleano de FSM do Foundation não deve ser confundido com as condições triestado avaliáveis sobre estados hipotéticos do GOAP.

## 3. Contextos de gameplay

Preservam-se os **três Contextos Globais de Desejos** definidos no GDD, com exatamente um ativo por vez:

**Normal → Item Principal em Jogo → Fuga Final.**

O contexto determina a configuração de Desejos aplicável a cada arquétipo — inclusive elegibilidade e pesos —, mas não substitui o GOAP por comandos fixos. Na Fuga Final, escapar pode ganhar importância sem se tornar obrigatório para todos. O objetivo principal do assalto é uma regra de missão, **não** uma hierarquia obrigatória de Goals mestres sobre todos os larápios.

Contexto Global de Desejos é distinto de **Conhecimento Global**. Mudanças de contexto provocam reavaliação, mas não apagam conhecimento nem Effects efetivamente concluídos.

## 4. Goals, Desejos, intenções, planos e execução

Separam-se conceitualmente cinco responsabilidades:

1. **Goal:** condição de estado desejado; diz *o que* alcançar, não qual Action usar.
2. **Desejo:** motivação que torna um Goal aplicável e atribui valor/prioridade de acordo com arquétipo, contexto e satisfação.
3. **Intenção ativa:** compromisso atual do Actor com um Desejo/Goal selecionado.
4. **Plano ativo:** sequência de Actions e Targets considerada para realizar essa intenção.
5. **Action em execução:** operação real que está sendo realizada agora.

Trocar uma Action, trocar de plano e trocar de intenção são decisões distintas. A intenção pode continuar enquanto o plano é reconstruído. Não se exige uma hierarquia rígida de Goals mestres/secundários nem uma pilha permanente de Goals suspensos.

## 5. Condições e satisfação

Goals, Preconditions e demais condições utilizam **Predicados modulares e componíveis** (`AND`, `OR`, `NOT`), avaliados sobre a fonte de estado apropriada. O resultado distingue **Verdadeiro, Falso e Desconhecido**. Desconhecido não é falso; `NOT Desconhecido` permanece desconhecido.

As condições devem permitir expressar objetivos compostos, de conquista, manutenção ou restauração, conforme a necessidade do gameplay. Um Goal satisfeito não obriga o Actor a continuar executando Actions só para produzir atividade. A qualidade relativa de estados alcançados pode ser avaliada separadamente de sua satisfação básica, quando fizer sentido.

Preserva-se a **satisfação individual acumulada** do GDD: ela é um histórico de conquistas comprovadas, não a mera avaliação do estado atual. Pode ser configurada para execução única, limitada ou repetida. Uma Action concluída pode satisfazer mais de um Desejo quando os resultados reais confirmarem as condições correspondentes. Tentativas, interrupções e Effects apenas hipotéticos não acumulam satisfação.

## 6. Actions independentes de Goals

Uma Action descreve possibilidades de transformação de estado, Preconditions, Effects esperados, requisitos de capacidade, Targets, custos, reservas e regras pertinentes à execução. **Não depende de identificadores de Goals específicos.**

Diferentes Actions podem oferecer caminhos alternativos ao mesmo estado desejado: usar uma chave, hackear, arrombar ou contornar uma porta. O GOAP descobre relações causais e cadeias a partir de Preconditions e Effects, em vez de receber sequências de comportamento pré-programadas. Exigências intermediárias — obter chave, restabelecer energia, alcançar uma posição — geralmente são condições/Actions de um plano, não novos Goals de alto nível.

Capacidades determinam quais soluções o Actor pode executar; elas não devem ser confundidas com preferências de personalidade ou competência de raciocínio.

## 7. Conhecimento: conteúdo, proveniência e três escopos

Preservam-se os três escopos **Individual, Gangue e Global**, com políticas configuráveis de validade, atualização, conflito e propagação. Um fato individual não é compartilhado automaticamente; conhecimento da Gangue é distribuído conforme sua política; fatos Globais alcançam explicitamente as gangues definidas.

Conhecer uma entidade não significa conhecer todas as suas propriedades. Fontes podem incluir percepção visual, área percebida radial, investigação, resultados de Actions, comunicação autorizada e eventos globais. O conhecimento negativo explícito é distinto de desconhecimento.

Diferenciam-se:

- **World State real:** a realidade efetiva, validada pelas mecânicas de gameplay.
- **Knowledge State:** fatos legitimamente disponíveis ao larápio, com seus escopos e qualidade.
- **Estado hipotético de planejamento:** projeção local de mudanças previstas por Actions, sem alterar conhecimento ou mundo real.
- **Previsão externa opcional:** estimativa incerta de acontecimentos futuros, distinta tanto de observação quanto de Effect hipotético da própria Action.

O planejador não tem acesso indiscriminado a fatos ocultos. Entretanto, uma **interação de gameplay pode legitimamente revelar informação privilegiada**, se isso tiver sido definido como seu resultado informativo.

## 8. Planejamento encadeado e projeções

O GOAP deve encontrar planos de múltiplas Actions que alcancem condições desejadas, aplicando **Effects previstos a estados hipotéticos**. Uma mudança simulada não é um fato confirmado. A busca parte do conhecimento legítimo e dos Targets conhecidos/compatíveis; não pode inventar a localização de objetos nem o resultado futuro de investigações.

O GDD não fixa comprimento máximo de plano. Limites de busca são uma necessidade de desempenho configurável; o sistema deve distinguir **sem solução conhecida** de **busca esgotada sem conclusão**.

O uso de Effects e estados projetados não exige reproduzir toda a física do Unity nem transformar toda geometria contínua numa álgebra simbólica universal.

## 9. Avaliadores especializados e perfis intercambiáveis

Mecânicas complexas, como **navegação**, **fuga e procura de objetivos**, **confronto** e futuras especializações, podem oferecer **avaliações próprias** de viabilidade, custo e qualidade de alternativas. Essas avaliações devem considerar os estados hipotéticos pertinentes; quando isso não for possível, retornam a limitação/desconhecimento, sem usar silenciosamente o mundo real como se já refletisse os Effects projetados.

Critérios e parâmetros especializados podem ser montados em **perfis determinísticos, reutilizáveis e intercambiáveis** por Actor ou arquétipo. O mesmo núcleo GOAP utiliza essas avaliações sem incorporar regras particulares de rota, ataque ou investigação. Avaliar não significa executar; a autoridade física continua na mecânica especializada.

## 10. Seleção conjunta de Goals e planos

A intenção final não deve ser fixada **apenas pelo peso de um Desejo, antes de considerar suas soluções**. Goals aplicáveis e alternativas de planos devem ser avaliados conjuntamente, considerando prioridade, custo do plano completo, oportunidade, informação, qualidade estimada e consequências de mudar de decisão.

O custo temporal básico previsto no GDD é o tempo estimado de deslocamento somado às durações resolvidas das Actions. O peso do Desejo e o custo do plano são dimensões distintas; a fórmula de agregação permanece configurável, sem fixação antecipada. A escolha do Target resulta da comparação das soluções, não de uma regra genérica de escolher o mais próximo.

## 11. Personalidade, competência decisória e perfis

São dimensões independentes:

- **Personalidade:** o que o larápio valoriza; inclui prioridades, aversões e **tendência investigativa**.
- **Competência decisória:** quão bem o larápio procura, estima e escolhe soluções, independentemente de suas preferências.
- **Capacidades de gameplay:** aquilo que o Actor efetivamente consegue realizar no mundo.

A **Competência Decisória** pode combinar quatro aspectos configuráveis e intercambiáveis:

1. **Amplitude/profundidade da busca:** quantas alternativas e cadeias consegue explorar.
2. **Qualidade da avaliação:** quão bem estima custos, oportunidades e consequências.
3. **Range de aceitação:** quão boa uma solução precisa ser para ser considerada suficientemente satisfatória.
4. **Estabilidade decisória:** quanta vantagem justifica substituir uma intenção ou um plano já escolhido.

Range de aceitação e margem de troca são relacionados, **mas não idênticos**. O primeiro orienta a seleção entre alternativas; a segunda orienta a substituição de uma decisão ativa. Um NPC pode ser competente e persistente, competente e adaptável, ou menos competente sem necessariamente agir ao acaso. As escolhas permanecem determinísticas, salvo quando uma mecânica probabilística for explicitamente configurada.

## 12. Estabilidade em três níveis

Existe continuidade independente em três níveis:

- **Intenção:** pode permanecer ativa durante mudanças de plano; sua substituição considera mudança de contexto, validade, satisfação e vantagem efetiva de outra intenção.
- **Plano:** pode permanecer ativo enquanto válido, mesmo diante de uma melhoria pequena; é substituído quando a vantagem de outra solução justifica a troca.
- **Action em execução:** preserva progresso quando continua válida e selecionada, conforme as regras de execução do GDD.

Estabilidade e margens não são uma obrigação de manter decisões inválidas nem de insistir por tempo já gasto. Comparam-se os **custos restantes** e as consequências reais da troca. Preservar o plano **ativo enquanto válido** não contradiz a regra do GDD de não manter planos antigos abandonados: permanecem os fatos e resultados concluídos, e futuros planos podem ser reconstruídos.

## 13. Investigação e valor informacional

Adquirir informação pode ser uma oportunidade legítima, não um comportamento obrigatório diante de toda incerteza. Um larápio pode preferir um roubo concreto a uma tentativa investigativa — ou o contrário — conforme o contexto, valor potencial da informação, custo, personalidade e competência.

A avaliação pode estimar **o potencial de obter informação**, mas não assumir antecipadamente **qual fato será encontrado**. A tendência investigativa integra a personalidade; a competência determina a qualidade da comparação. O mesmo valor informacional não deve ser contado duplicadamente como bônus arbitrários sobre o mesmo resultado.

A investigação de presença, Áreas e POIs conserva as regras específicas já definidas no GDD. Não se cria um mecanismo paralelo de investigação que contorne percepção e conhecimento.

## 14. Interações com requisitos desconhecidos e diagnóstico

**Condições para tentar** uma interação são distintas das **Preconditions necessárias ao sucesso**. Uma precondition de sucesso desconhecida não é presumida verdadeira; ainda assim, uma tentativa legítima pode ocorrer, quando a mecânica permitir, para obter informação relevante.

Ao tentar abrir uma porta, por exemplo, a própria interação pode informar **por que não foi possível**, revelando chave, energia, habilidade ou combinações de requisitos, mesmo quando essa informação não era perceptível por sensores convencionais. Esse diagnóstico é uma fonte legítima de conhecimento definida pelas regras do gameplay; não significa revelar automaticamente a localização dos recursos necessários.

O **resultado da execução**, os **Effects físicos confirmados** e as **informações descobertas** são responsabilidades distintas. A ausência do Effect de abertura não impede um diagnóstico útil. Não se confirmam Effects de sucesso nem satisfação acumulada por uma tentativa recusada. A forma técnica dos resultados de execução e das evidências tipadas deve preservar/reconciliar os contratos existentes; logs textuais não são autoridade de conhecimento.

Se os impedimentos conhecidos não mudaram, a mesma tentativa não deve ser repetida indefinidamente sem justificativa nova.

## 15. Execução, reservas e replanejamento

O GOAP permanece **event-driven**. Mudanças relevantes de conhecimento, contexto, capacidades, disponibilidade, reservas ou resultados de Actions podem provocar reavaliação; reavaliar não significa necessariamente interromper ou reiniciar.

A execução confirma os resultados reais pelas autoridades apropriadas. Sucesso e términos alternativos seguem as regras do GDD, inclusive as reservas adquiridas **apenas ao iniciar** a Action e liberadas em seus términos. O progresso de uma Action temporizada é preservado apenas quando a continuidade ainda é legítima.

O sistema deve permitir explicar a cadeia **evento → dado alterado → decisão afetada → reavaliação → manutenção ou troca**.

## 16. Contingência e continuidade de Goals

A **Contingência** prevista no GDD reutiliza o mesmo GOAP, Goals, Actions e critérios de avaliação; não é uma segunda IA. Ela se torna elegível conforme a ausência de oportunidades normais viáveis, respeitando as regras de gameplay vigentes.

Oportunidades informativas devem **competir legitimamente**, sem receber prioridade automática sobre outras alternativas e sem serem confundidas com planos de sucesso garantido. Não existe exigência de uma fila de Goals suspensos: um Goal não selecionado ou temporariamente sem solução pode voltar à avaliação quando fatos, capacidades ou contexto mudarem. A memória está nos conhecimentos e resultados válidos, não em uma obrigação de armazenar cada plano abandonado.

A harmonização exata da elegibilidade de **tentativas informativas versus Contingência** deve preservar essa decisão e o GDD, não introduzir uma preferência implícita por investigar.

## 17. Previsão futura opcional

Previsores especializados poderão estimar acontecimentos externos — como deslocamento de atores ou conclusão de atividades — quando houver utilidade de gameplay. Não são um terceiro sensor que constata o futuro. Suas estimativas distinguem horizonte, incerteza e origem, não se tornam conhecimento factual nem satisfazem Preconditions determinísticas.

A projeção de Effects do próprio plano é parte essencial do GOAP; **previsões externas são opcionais** e não devem elevar a complexidade obrigatória do primeiro recorte. Seu futuro uso deve respeitar ou revisar explicitamente regras existentes do GDD, como o tratamento de perda de alvo.

## 18. O que está decidido e o que não está

**Decidido conceitualmente nesta discussão:** diretriz transversal de composição; independência entre Goal/Action; separação Goal/Desejo/intenção/plano/execução; estados hipotéticos e condições triestado; avaliadores especializados e perfis intercambiáveis; previsões externas opcionais; diagnóstico informativo legítimo; competência decisória separada de personalidade, com range de aceitação e estabilidade; estabilidade própria de intenção e plano; investigação como oportunidade concorrente; seleção conjunta de Goals e soluções.

**Preservado do GDD:** contextos Normal/Item Principal em Jogo/Fuga Final; três escopos de conhecimento; arquétipos e capacidades; prioridade mais custo temporal; Targets conhecidos; satisfação acumulada; Contingência; reservas; execução real como autoridade e replanejamento por eventos; regras particulares de percepção e investigação.

**Não decidido nesta etapa:** classes/interfaces/arquivos; algoritmo de busca; fórmula de pontuação; valores numéricos de perfis, margens e orçamentos; mecanismo exato de navegação hipotética; catálogo de Actions/Goals do jogo completo; serialização e authoring Unity; representação técnica final de recusas/diagnósticos; ordem de implementação. Essas decisões pertencem à investigação técnica ou ao balanceamento, sempre subordinadas às diretrizes já acordadas.

## 19. Orientação para reconciliação documental

1. **Registrar estas decisões como fonte de intenção:** não delegar ao Codex a tarefa de reinventar ou interpretar livremente o que foi decidido nesta conversa.
2. **Confrontar com o GDD e os ADRs vigentes:** preservar regras de gameplay e distinguir incompatibilidade real de diferença de estágio entre GDD e runtime.
3. **Resolver contradições explicitamente:** documentar o que foi preservado, refinado ou substituído, sem criar duas normas concorrentes.
4. **Formalizar em ADRs técnicos posteriormente:** o Codex determina organização, contratos e implicações técnicas segundo o workspace e as autoridades Immersive; não introduz novos requisitos de produto por conta própria.
5. **Não elevar a demonstração do MVP a norma:** suas soluções provisórias são evidência e aprendizado, não fronteiras do GOAP definitivo.

### Síntese executiva

> **O PEGA precisa de um GOAP comum capaz de comparar conjuntamente desejos e soluções, raciocinar sobre estados conhecidos e projetados, executar com validação real e adaptar intenções e planos por eventos. A variedade dos larápios decorre da composição modular de conhecimento, personalidade, competência, capacidades e avaliações especializadas — não de núcleos de IA distintos ou regras rígidas por comportamento.**
