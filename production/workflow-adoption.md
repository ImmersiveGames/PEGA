# Adoção do fluxo Production no PEGA

## Fontes e papéis

- `project.yaml` e `production/stage.txt` dizem ao `$help` que o projeto está em Production.
- `production/sprint-status.yaml` é um registro local de status para leitura do pipeline. `sprint: null` indica que o trabalho não foi organizado em sprint com datas.
- Cada `story-*.md` guarda critérios, rastreabilidade e estado da evidência local.
- GDD/MVP/ADR/TASKS mantêm suas autoridades definidas em `AGENTS.md`. Trello continua operacional para status do cartão, responsáveis, checklists e estimativas.

## Como atualizar

1. Atualize a história local quando houver mudança de escopo ou evidência verificável. Registre fonte, data e caminho local do artefato.
2. Atualize `sprint-status.yaml` para refletir o **status local** da história. Não marque `done` apenas porque o cartão foi movido ou um checklist foi marcado.
3. Registre o estado Trello e a data de observação em campos separados da história. Se Trello, TASKS ou a evidência local divergirem, descreva a divergência e proponha reconciliação; não sincronize automaticamente.
4. Use `production/session-state/active.md` para o próximo passo concreto e as questões ainda abertas. Não escolha um único cartão ativo se as fontes não o sustentarem.

## Estados de evidência

- `NOT VERIFIED`: não há observação/evidência local suficiente.
- `REPORTED`: um comentário ou cartão relata execução; ainda não é verificação independente.
- `STATIC`: código, asset ou configuração foi inspecionado; não comprova runtime.
- `OBSERVED`: comportamento foi observado diretamente e descrito com contexto reproduzível.
- `EXECUTED`: existe resultado de execução inspecionável anexado ao registro.

Armazene futuros registros de execução em `production/evidence/<story-id>/`. Para gameplay, indique quando falta validação manual no Unity Editor/Play Mode. O pipeline não executa Unity, builds ou testes por conta própria.

## Itens sem vínculo estabelecido

O último commit local de 2026-11-23 adiciona conteúdo de lifecycle da missão, mas ainda não foi ligado a um item de TASKS/Trello (incluindo VS-15 ou DEM-05). Um item de arte ativo também foi mencionado no levantamento anterior, mas seu ID, título e escopo não foram revalidados neste bootstrap. Ambos permanecem sem mapeamento; não crie história, prioridade ou responsável por inferência.

Commits, artes e conteúdo só devem entrar no registro depois de uma ligação demonstrada a um objetivo de TASKS/cartão.
