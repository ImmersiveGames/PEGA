# FOUND-01 — relatório final de validação Unity

**Data:** 2026-10-10 21:36 (snapshot local do Editor.log, America/Sao_Paulo)

**Cartão:** [FOUND-01 — Compor aplicação PEGA e validar fluxo Mission → Result](https://trello.com/c/5BvgU409)

**Revisão:** pega-production-governance / Review Delivery
**Resultado técnico:** **APROVADO**, com limites de evidência registrados abaixo. O cartão não foi concluído.

## Proveniência

- Logs/Editor.log foi lido em modo compartilhado enquanto o Unity o mantinha aberto. Snapshot: 1.737.002 bytes, SHA-256 18F7FB885C71201D87E5C511B1EB11B3DC5D6959928FA874EBA328C93A00FE08, última gravação observada 2026-10-10T21:36:57.7601166-03:00. O arquivo é ignorado pelo Git e não foi versionado. Linhas citadas referem-se a esse snapshot; o log completo não foi incluído na entrega.
- Packages/manifest.json, Packages/packages-lock.json, Library/PackageCache/com.immersive.framework@ed40a507d908/package.json e Editor.log:167 identificam Framework 1.1.0-preview.6, origem registry, no PackageCache indicado.
- O último ciclo FOUND-01 no log inicia com boot bem-sucedido na linha 9958 e contém a transição e teardown nas linhas 10066–10075. A tentativa anterior falhou por Camera Session vazia na linha 9677, antes da correção. Não há linha [ERROR] entre o boot bem-sucedido e o teardown.
- A validação manual dos Inspectors e da Scene List foi informada pelo usuário; não há captura nem relatório exportado anexado.

## Configuração e contrato

Assets/PEGA/PEGAApplication.asset aponta para Mission Route, desabilita Player Session e Progression Save, serializa playerActorSelectionDuplicatePolicy: 20, mantém startupCameraAssignments vazio, inclui o prefab PEGA_TechnicalCameraOutput e atribui PersistentContent.unity. ImmersiveFrameworkSettings.asset referencia o GameApplication PEGA. GUIDs conferem com assets e cenas próprios sob Assets/PEGA; a auditoria não encontrou dependência de conteúdo PEGA em FrameworkValidation.

No pacote resolvido, o valor 20 corresponde a PlayerActorSelectionDuplicatePolicy.UniqueAcrossJoinedSlots. O contrato rejeita Unspecified e o validador de autoria verifica a política mesmo sem Player Session. UniqueAcrossJoinedSlots é decisão específica do PEGA para futuro suporte a dois Players locais, não regra geral do Framework. Piso mínimo separado e maturidade da enumeração são unknown na documentação/changelog examinados.

FrameworkBootValidator valida Camera Session no boot; CameraSessionConfiguration.TryValidate rejeita ausência de Camera Output. Isso explica a tentativa anterior em Editor.log:9677. O asset final tem Output técnico próprio e o log registra sua inicialização em 9927. Não há Player Session, assignment de câmera para Player nem câmera de gameplay. A câmera top-down definitiva permanece no cartão #29.

## Evidência de runtime

| Evidência | Localização | Resultado |
|---|---:|---|
| Framework importado/resolvido | Editor.log:167 | 1.1.0-preview.6 no PackageCache efetivo. |
| Camera Output técnico | Editor.log:9927–9928 | Output inicializado e chamada subsequente preservada sem duplicação. |
| Sem Player Session | Editor.log:9935 | Runtime informa que a admissão local não está configurada porque o GameApplication não habilitou Player Session. |
| Mission/Preparation | Editor.log:9938 | Marcador FOUND-01 registra entrada na Mission Activity em Preparation. |
| Boot/Mission Ready | Editor.log:9958 | Boot succeeded; GameApplication PEGA, Startup Route Mission, Primary Scene MissionRoute, Mission Ready, zero bloqueios. |
| Fases diagnósticas | Editor.log:9963, 9994 | Mudanças para Assault e Escape; linha 10025 retorna manualmente a Preparation. Não demonstra gameplay ou máquina de estados. |
| Mission → Result | Editor.log:10066, 10069–10070 | Mission liberada; ActivityRequestTrigger concluiu com kind Succeeded; Result Ready; uma cena carregada e uma liberada, zero bloqueios. |
| Teardown | Editor.log:10074–10075 | Camera Output encerrado por SessionTopologyTeardown, sem bloqueio reportado no ciclo. |

Boot, fases, pedido, Result Ready, liberação e teardown aparecem no mesmo ciclo. Não comprova regras, timers ou gameplay.

## Checklist FOUND-01

Estados técnicos de evidência; a checklist operacional do Trello permanece com todos os itens INCOMPLETE.

| # | Critério | Estado | Base e limite |
|---:|---|---|---|
| 1 | Registrar importação e versão do Framework resolvido | **PASS — log/estático** | Editor.log:167, manifest, lock e pacote instalado. |
| 2 | GameApplication PEGA/Mission; Player/Progression desligados; Camera Output técnico sem assignments ou câmera de gameplay | **PASS — estático/runtime** | Asset atual, contrato de boot e inicialização em 9927; errata já consta no Trello. |
| 3 | Mission sem Players e Result distinta | **PASS — estático/runtime** | Player Session desativada, admissão não configurada (9935), Mission Ready (9958), Result Ready (10069–10070), assets distintos. |
| 4 | Persistent Content, perfis e cenas próprios no Build Profile ativo | **PASS — estático/manual reportado** | GUIDs e referências estáticas; usuário confirmou quatro cenas PEGA habilitadas na Scene List e validadores dos perfis válidos. Sem captura/nome do perfil. |
| 5 | Navegação sem dependência de FrameworkValidation | **PASS — estático/runtime** | Nenhuma referência de FrameworkValidation nos assets PEGA; boot/navegação PEGA no ciclo citado. EditorBuildSettings também contém duas cenas FrameworkValidation habilitadas, mas não são dependências do GameApplication. |
| 6 | Validar autoria e registrar avisos/limitações | **PASS — manual reportado/estático** | Usuário informa GameApplication, Route, Activities e perfis válidos; após política explícita, GameApplication ficou Valid. Sem capturas e contagem de warnings. |
| 7 | Boot em Mission sem Player e dependência de FrameworkValidation | **PASS — runtime/estático** | Editor.log:9935, 9958; assets de produção sem referências a FrameworkValidation. |
| 8 | Diagnóstico Preparation → Assault → Escape | **PASS — runtime diagnóstico** | Editor.log:9938 → 9963 → 9994; retorno manual em 10025. Não é gameplay. |
| 9 | Solicitar Result e registrar Mission → Result | **PASS — runtime** | Editor.log:10066, 10069–10070; teardown em 10074–10075. |

## Trello e reconciliação

trelloReadChecklist/list_by_card retornou a checklist Implementação e aceite, completa com nove itens, todos INCOMPLETE. O item 2 já contém o Camera Output técnico obrigatório, a ausência de Player assignments/câmera de gameplay e a remissão ao cartão #29. O comentário de errata já existe. Nenhuma escrita foi feita nesta revisão. Os nove itens estão tecnicamente prontos para conclusão operacional pelo responsável, mas o cartão permanece aberto.

As fontes de rastreabilidade são PEGA GDD/MVP.md, PEGA GDD/ADR-PREPRODUCAO.md (adendo v2) e PEGA GDD/TASKS.md. Não foram alterados GDD/MVP, Framework, dependências ou Trello. A falha anterior em 9677 foi corrigida por composição PEGA, sem alteração do Framework.

**Reconciliação documental:** a seção 9 de ADR-PREPRODUCAO.md foi atualizada nesta entrega para distinguir o registro inicial do estado final confirmado, registrar as evidências de boot, fases, transição, teardown e validadores informados, e refletir a errata já aplicada ao Trello e a política PEGA UniqueAcrossJoinedSlots. O histórico anterior foi preservado. TASKS.md mantém as limitações/proveniência explícitas no relatório. GDD.md e MVP.md não foram alterados.

## Review Delivery e validação

- git diff --check executado sem erros de whitespace; Git emitiu apenas avisos de conversão LF/CRLF em arquivos existentes.
- Nenhuma compilação, importação, teste ou Play Mode foi executado pelo agente, conforme AGENTS.md. Evidência Unity é do log local e do relato manual do usuário.
- Logs/Editor.log é local/ignorado e não versionado. O hash e as linhas identificam o snapshot examinado.
- Nenhum commit, push ou alteração do estado do cartão foi realizado. Estimativa do cartão: 10 horas humanas; esforço efetivo não mensurável pelos registros locais.

## Feedback separado para immersive-ecosystem

Recomendação: no fluxo existente de GameApplication, exigir inspeção da versão resolvida; localizar validadores reais de bootstrap e autoria; separar gates incondicionais/condicionais; seguir validações até TryValidate; mapear dependências entre subsistemas; comparar execução com documentação instalada; orientar a composição mínima legítima; registrar maturidade e piso demonstrável ou unknown.

Caso de regressão FOUND-01: Camera Session é validada no boot e TryValidate exige ao menos um Camera Output mesmo sem Player Session. O validador de autoria também rejeita Actor Selection Duplicate Policy Unspecified, inclusive sem Player Session. É necessária uma política explícita, mas UniqueAcrossJoinedSlots é escolha do PEGA. A divergência de Getting Started com o gate efetivo de boot deve ser investigada pelo owner do Framework, sem classificá-la aqui como bug. A skill compartilhada não foi editada.

## Parecer

**Aprovado tecnicamente para FOUND-01.** Os nove critérios contam com evidência estática, runtime ou validação manual reportada compatível com sua natureza. Limites: sem captura do Build Profile/relatórios de validadores e sem log bruto versionado; essas ausências reduzem reprodução independente, mas não contradizem os resultados observados e informados. A checklist e o cartão não foram concluídos pelo agente; o responsável pode fazê-lo após revisar este parecer. Não houve commit ou push.
