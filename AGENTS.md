# Instruções do projeto PEGA

## Autoridade e fontes

- GDD é normativo para regras e comportamento do jogo; MVP define escopo e aceitação; ADR registra decisões de arquitetura e produção; TASKS decompõe trabalho aprovado.
- Código e pacotes resolvidos mostram implementação/API disponível, não comprovam comportamento em execução.
- Trello é fonte operacional para estado, responsáveis, checklists, estimativas e progresso. Uma tarefa só está entregue com evidência de aceitação, não apenas pelo estado do cartão.
- GanttFlow é projeção derivada. Não sobrescreva fontes normativas nem resolva divergências por inferência.
- Ao auditar, preparar, reconciliar ou revisar trabalho de produção, consulte `.agents/skills/pega-production-governance/SKILL.md`.

## Limites de alteração

- Não altere GDD, MVP, TASKS, ADR, estado/cartões/checklists Trello, GitHub ou GanttFlow durante Audit, Prepare Task, Reconcile ou Review Delivery. A Fase 1 de governança é somente leitura.
- Não exponha nem solicite credenciais. Se uma integração estiver indisponível, continue as verificações locais e identifique o que ficou sem confirmação externa.
- Não altere configurações/skills globais do Codex para habilitar esta governança.
- Mudanças no código do PEGA que usam APIs Immersive exigem primeiro a skill instalada `immersive-ecosystem`; resolva manifest, lock e conteúdo instalado antes de orientar APIs. Se a skill ou a resolução do pacote não estiver disponível, interrompa orientação específica de API.
- Regras de uso de APIs precisam de mínimo de versão demonstrado por contrato e maturidade, com evidência em documentação versionada, changelog/release e fonte resolvida quando necessário. A versão resolvida é um fato do consumidor, não um mínimo. Se o piso/maturidade não puder ser provado, reporte `unknown` e não adivinhe, atualize dependência ou use `latest` como regra.
- Mudança de fonte do Immersive Framework exige tarefa separada no repositório dono e as skills instaladas `immersive-framework-package-general`, `immersive-framework-architecture-audit`, `immersive-framework-package-implementation` e, para Core/módulos, `immersive-framework-core-architecture`. Não copie skills globais ao PEGA.
- Não execute build/import/testes Unity, Play Mode, smoke ou batchmode. Separe checagem estática de validação que requer Unity e liste as etapas manuais.
