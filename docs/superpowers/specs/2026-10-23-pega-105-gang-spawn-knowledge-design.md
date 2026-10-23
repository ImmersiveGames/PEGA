# #105 — Spawn Point e conhecimento compartilhado da Gangue

## Intenção e resultado

Quando um larápio atravessa fisicamente a entrada de um Spawn Point no sentido de ingresso, o PEGA registra essa entrada como saída potencial conhecida da Gangue. O fato fica disponível a membros já vinculados e a membros que recebam o vínculo depois, como Target de planejamento com proveniência de Knowledge State.

## Abordagens consideradas

- Usar Route Spatial Entry ou Activity Explicit Relocation: recusada porque esses contratos controlam placement/relocation de Player e não representam uso de entrada por NPC.
- Copiar fatos para cada `PEGANpcKnowledgeState`: recusada porque duplicaria ownership individual e exigiria sincronização manual para membros posteriores.
- Manter um `PEGANpcGangKnowledgeState` do PEGA por Gangue, com referência explícita em cada membro e Spawn Point: escolhida por manter a Gangue como unidade de conhecimento, sem serviço global ou autoridade no Framework.

## Escopo e limites

- O estado da Gangue armazena fatos estruturais de entradas utilizadas durante o lifetime da Mission.
- O Spawn Point registra somente entrada validada por contato físico e direção de movimento; possuir referência de cena a `PEGANpcEscapeDestination` não concede conhecimento.
- O volume físico usa `BoxCollider` trigger com `Rigidbody` cinemático no Spawn Point, mantendo o `CharacterController` do NPC e sem alterar a colisão dos Actors.
- Cada membro consulta o estado compartilhado por vínculo explícito. O produtor futuro de Actors deverá associar o membro ao mesmo estado ao criá-lo, antes de disponibilizar o Actor ao gameplay.
- O Target de saída usa identidade estável do Spawn Point, posição capturada no momento da evidência e proveniência `KnowledgeState`.
- O contrato de evidência identifica sua origem como ingresso físico em Spawn Point; o estado compartilha o mesmo Target imutável entre os membros vinculados.
- #105 publica a mudança e fornece Targets. Não cria Actors, receitas temporais completas, seleção/replanejamento GOAP, nem execução de fuga do #91 N05.
- A validação temporária usa Mission, a fonte compartilhada de navegação física do PEGA e `PEGALogging`; não executa Actions/GOAP nem grava conhecimento sem atravessar o Spawn Point. Como proprietária da requisição temporária, a sonda só a encerra com Success após observar a posição física do Actor dentro do raio de chegada já configurado no PEGA.

## Chamadas de implementação

- O estado é um componente PEGA de runtime por Gangue/Mission; a referência ao estado é a identidade explícita do vínculo, mantendo Gangues isoladas.
- O Spawn Point possui identidade estável e vínculo explícito à Gangue. Seu transform representa a entrada/saída potencial; sua posição só entra no Target quando um membro da Gangue cruza fisicamente o trigger para dentro.
- Membros tardios passam pelo mesmo contrato de associação e leem os fatos já existentes, sem copiar o Knowledge State para o NPC.
- A validação emite somente pedidos temporários à fonte de navegação compartilhada para atravessar o volume e reporta separadamente rota disponível, intenção recebida, deslocamento físico, callbacks por collider e travessias lógicas por Actor, uso legítimo do ponto e leitura do Target por dois membros atuais e um membro associado depois da evidência. Um Actor composto pode gerar mais de um callback de trigger durante a mesma travessia; isso continua sendo um único ingresso lógico.

## Aceite observável

No Unity Mission, logs canônicos demonstram: ausência de Target antes do uso; uso físico válido que cria um único fato/Target; leitura por membro atual e por membro associado posteriormente; estado separado de outra Gangue sem o Target; direção inválida ou evidência repetida não cria fato duplicado. Nenhum teste de decisão GOAP ou fuga física é declarado.
