# #105 — Ingresso real de larápio na Mission

## Objetivo

Completar a integração funcional do #105 fazendo um larápio criado pelo PEGA entrar fisicamente na Mission por um Spawn Point real. O uso físico aceito pelo Spawn Point produz o fato de saída potencial no estado compartilhado da Gangue.

## Evidência arquitetural

- O GDD define que Spawn Points produzem os larápios e que a entrada usada por um membro se torna uma saída potencial conhecida pela Gangue. Ele também exige que o larápio pertença à Gangue desde Spawn Safe, antes de atravessar a entrada.
- A Mission atual contém dois NPCs authorados na `MissionActivityContent`, mas não possui produtor de Actor, Spawn Safe, nem fluxo de ingresso de NPC. Os NPCs não atravessam uma entrada ao ativar a Activity.
- O Framework resolvido publica NonPlayer já criado por `ActorOccurrencePublicationScope.TryPublish` sob um owner de Activity já committed. Ele não cria nem move o Actor. O Framework permanece apenas como autoridade de publicação/ocorrência.
- O #105 já implementa o estado compartilhado da Gangue e a evidência física do Spawn Point. O smoke informado pelo usuário (34 PASS, 0 FAIL, 0 UNAVAILABLE) valida compartilhamento, isolamento, idempotência e navegação; essa cobertura não será repetida.

## Desenho aprovado

Adicionar ao PEGA um produtor de ingresso único, pertencente a uma contribuição da Activity Mission. Ao entrar na Activity, ele instancia um prefab de larápio com ActorId próprio em um Spawn Safe authorado, associa-o ao estado da Gangue antes do deslocamento e envia uma solicitação ao `PEGANpcNavMeshMovementIntentSource` compartilhado para alcançar um ponto interno atravessando o trigger do Spawn Point.

Perception e Autonomy permanecem desabilitadas durante o ingresso. Após sucesso físico da navegação e confirmação de que o Spawn Point aceitou o ingresso e registrou o fato no estado da Gangue, o produtor publica a ocorrência do Actor sob o owner exato da Activity e habilita Perception e Autonomy. Qualquer falha cancela a navegação, não publica o Actor e destrói o Actor incompleto. Na saída da Activity, o produtor cancela trabalho pendente, despublica pelo token exato e libera o Actor que criou.

O ponto usa a abertura existente na parede norte do Blockout E01. A preparação Native adiciona novos objetos e associa membership aos dois NPCs já authorados, sem alterar suas posições manuais.

## Escopo

- Um Actor de ingresso por ativação da Activity Mission; sem agenda, batching, receitas temporais, contingentes ou escolha de arquétipo.
- Um Spawn Point físico authorado e um único estado da Gangue para esta composição Mission.
- Membership explícita dos dois NPCs já presentes e associação do ingresso criado antes do deslocamento.
- Reutilização da mecânica física compartilhada via fonte de navegação PEGA; não criar um segundo writer de Transform/CharacterController.
- Publicação de ocorrência pelo escopo e token do owner exato da Activity.
- Logger canônico para início, ingresso físico consolidado, publicação, falha e cleanup.
- Ferramenta temporária Editor somente para authoring idempotente aprovado; não será entregue como produto. Observador temporário não inicia navegação, não altera conhecimento e não comanda Actions.

## Fora do escopo

- Sistema de Spawn Recipes, tempos absolutos, portas, batch ou múltiplos Spawn Points.
- Regras de GOAP, fuga física, busca, replanejamento ou conhecimento artificial.
- Reabrir o smoke já aprovado de conhecimento compartilhado.
- Alterar Framework, os dois Actors authorados, suas posições ou a geometria/cena física existente.

## Aceite da integração

1. Ativar a Activity cria exatamente um larápio no Spawn Safe e o associa à Gangue antes da rota.
2. A rota é solicitada pela fonte PEGA compartilhada e o deslocamento físico do `CharacterController` cruza o trigger do Spawn Point no sentido de ingresso.
3. O Spawn Point aceita uma evidência física e a Gangue recebe um único Target tipado para aquele ponto.
4. A ocorrência do Actor só fica publicada depois do ingresso aceito e usa o owner exato da Activity; então Perception e Autonomy são habilitadas.
5. A saída/cancelamento da Activity não deixa navegação ativa, ocorrência publicada ou Actor incompleto.
6. O logger canônico permite seguir a cadeia causal e distingue falha de configuração/rota de ausência de fato físico.

O teste manual no Unity continua sendo a autoridade de aceitação. O smoke 34/34 anteriormente aprovado segue válido para o contrato de conhecimento; ele não é repetido.
