# Instruções do projeto PEGA

## Autoridade e fontes

- GDD é normativo para regras e comportamento do jogo; MVP define escopo e aceitação; ADR registra decisões de arquitetura e produção; TASKS decompõe trabalho aprovado.
- Código e pacotes resolvidos mostram implementação/API disponível, não comprovam comportamento em execução.
- Trello é fonte operacional para estado, responsáveis, checklists, estimativas e progresso. Uma tarefa só está entregue com evidência de aceitação, não apenas pelo estado do cartão.
- GanttFlow é projeção derivada. Não sobrescreva fontes normativas nem resolva divergências por inferência.
- Ao implementar/refatorar gameplay ou runtime em `Assets/PEGA`, ou ao auditar, preparar, reconciliar ou revisar trabalho de produção, consulte `.agents/skills/pega-production-governance/SKILL.md` e escolha somente o fluxo solicitado. Implementação direta é um fluxo distinto de Audit/Review e não herda o modo somente leitura desses fluxos.

## Limites de alteração

- Não altere GDD, MVP, TASKS, ADR, estado/cartões/checklists Trello, GitHub ou GanttFlow durante Audit, Prepare Task, Reconcile ou Review Delivery. A Fase 1 de governança é somente leitura.
- Não exponha nem solicite credenciais. Se uma integração estiver indisponível, continue as verificações locais e identifique o que ficou sem confirmação externa.
- Não altere configurações/skills do Codex implicitamente como efeito colateral da governança do PEGA. Quando o usuário solicitar explicitamente criar ou melhorar uma skill, edite a fonte da skill no repositório/localização que a mantém, seguindo o fluxo de criação de skills e as permissões disponíveis. Não copie skills globais para dentro do PEGA.
- Mudanças no código do PEGA que usam APIs Immersive exigem primeiro a skill instalada `immersive-ecosystem`; resolva manifest, lock e conteúdo instalado antes de orientar APIs. Se a skill ou a resolução do pacote não estiver disponível, interrompa orientação específica de API.
- Skills Unity/Base cujo escopo seja `Assets/_ImmersiveGames/NewScripts` não roteiam gameplay em `Assets/PEGA`; use-as apenas em trabalho explicitamente dentro do escopo Base 2.0/NewScripts/pipeline.
- Use sempre a documentação e o contrato da versão oficial mais recente como referência para planejar integração. Registre a resolução atual do consumidor para saber o que já existe e consulte README/CHANGELOG para identificar migrações; diferença de versão, sozinha, não bloqueia card ou integração. Quando necessário, trate atualização da dependência e migração documentada como etapas da própria tarefa autorizada. Não pare por falta de um “mínimo de versão”.
- Mudança de fonte do Immersive Framework exige tarefa separada no repositório dono e as skills instaladas `immersive-framework-package-general`, `immersive-framework-architecture-audit`, `immersive-framework-package-implementation` e, para Core/módulos, `immersive-framework-core-architecture`. Não copie skills globais ao PEGA.
- Não execute build/import/testes Unity, Play Mode, smoke ou batchmode. Separe checagem estática de validação que requer Unity e liste as etapas manuais.

## Calibração e observabilidade de gameplay no Unity

- Exponha no Inspector parâmetros que influenciam gameplay/balanceamento e precisam de calibração, com campos serializados, nomes legíveis, unidades (metros, graus, segundos etc.), tooltips e validação de faixas. Não serialize estado transitório apenas para expô-lo nem transforme constantes internas sem significado de design em opções de balanceamento.
- Mecânicas espaciais (como percepção) devem oferecer visualização de desenvolvimento na Scene View via Gizmos/Handles quando ela for útil para validar e calibrar alcance, direção, volume, obstrução e estado observado. A configuração geométrica deve aparecer em Edit Mode e Play Mode; a informação dinâmica, somente quando existir. Ofereça controle de visibilidade adequado a múltiplos Actors.
- Desenhos de debug devem refletir os mesmos parâmetros e resultados usados pela simulação, sem mudar decisões, atualizar conhecimento ou conceder acesso a informações ocultas. Podem revelar World State exclusivamente ao desenvolvedor, nunca ao GOAP.
- Separe código de editor do runtime (por exemplo com assembly de Editor ou diretivas apropriadas). Não exiba Gizmos e diagnósticos em builds finais, nem crie infraestrutura geral de debug quando as ferramentas nativas do Unity forem suficientes.
- Use o logger canônico do PEGA para eventos relevantes sem emitir logs por frame. Na entrega, distinga checagem estática de comprovação manual no Unity Editor/Play Mode.
